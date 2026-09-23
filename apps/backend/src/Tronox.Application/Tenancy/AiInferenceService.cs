using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tronox.Application.Admin;
using Tronox.Application.Common;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Tenancy;

/// <summary>
/// Motor de inferencia de agentes (RQ16, port de ECOREX adaptado a TRONOX). Arma el prompt
/// (base + enrutador + recursos + estado de cache + ultimos eventos), corre el bucle de
/// function calling sobre los toolsets registrados y extrae datos cache. Se omiten a proposito
/// las piezas atadas a WhatsApp (cierre/reactivacion, entrega multimedia, vision inline).
/// </summary>
public sealed class AiInferenceService : IAiInferenceService
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secretProtector;
    private readonly IAiProviderClient _client;
    private readonly IAiUsageService _usage;
    private readonly IAiAgentCacheService _cache;
    private readonly IReadOnlyList<IAgentToolset> _toolsets;
    private readonly TimeProvider _clock;
    private readonly IReadOnlyCollection<string> _allToolNames;

    private const int MaxToolRounds = 6;

    // Zona horaria por defecto (America/Bogota = UTC-5). Ancla temporal del prompt.
    private static readonly TimeSpan TenantOffset = TimeSpan.FromHours(-5);

    public AiInferenceService(IApplicationDbContext db, ISecretProtector secretProtector, IAiProviderClient client,
        IAiUsageService usage, IAiAgentCacheService cache, IEnumerable<IAgentToolset> toolsets, TimeProvider clock)
    {
        _db = db;
        _secretProtector = secretProtector;
        _client = client;
        _usage = usage;
        _cache = cache;
        _toolsets = toolsets.ToList();
        _clock = clock;
        _allToolNames = _toolsets.SelectMany(t => t.GetSpecs()).Select(s => s.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task<AiChatResult> TestChatAsync(long agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null,
        long actorUserId = 0, CancellationToken cancellationToken = default)
        => RunCoreAsync(agentId, agentId, turns, systemPromptOverride, autonomous: true, actorUserId, cancellationToken);

    public Task<AiChatResult> RespondAsync(long agentId, long sessionId, IReadOnlyList<AiChatTurn> turns, bool autonomous,
        long actorUserId, CancellationToken cancellationToken = default)
        => RunCoreAsync(agentId, sessionId, turns, null, autonomous, actorUserId, cancellationToken);

    private async Task<AiChatResult> RunCoreAsync(long agentId, long sessionId, IReadOnlyList<AiChatTurn> turns,
        string? systemPromptOverride, bool autonomous, long actorUserId, CancellationToken cancellationToken)
    {
        var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
        if (agent is null) { return new AiChatResult(false, null, "El agente no existe."); }

        var providerCfg = await _db.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == agent.Provider, cancellationToken);
        if (providerCfg is null || !providerCfg.IsEnabled || string.IsNullOrWhiteSpace(providerCfg.ApiKeyEncrypted))
        {
            return new AiChatResult(false, null, $"El proveedor {agent.Provider} no esta habilitado en la plataforma.");
        }

        string apiKey;
        try { apiKey = _secretProtector.Unprotect(providerCfg.ApiKeyEncrypted); }
        catch { return new AiChatResult(false, null, "La API key del proveedor no se pudo descifrar. Vuelve a guardarla en Servidores de IA."); }

        var meta = AiProviderCatalog.For(agent.Provider);
        var model = !string.IsNullOrWhiteSpace(agent.Model) ? agent.Model!
            : !string.IsNullOrWhiteSpace(providerCfg.Model) ? providerCfg.Model!
            : meta.DefaultModel;

        if (turns.Count == 0) { return new AiChatResult(false, null, "Escribe un mensaje para probar el agente."); }

        var quota = await _usage.GetQuotaAsync(cancellationToken);
        if (quota.Exceeded && quota.Hard)
        {
            return new AiChatResult(false, null, $"Alcanzaste el limite de tokens de IA de tu plan este mes ({quota.MonthlyLimitTokens:N0}).");
        }

        var resources = await _db.AiAgentResources.AsNoTracking()
            .Where(r => r.AgentId == agentId).OrderBy(r => r.SortOrder)
            .Select(r => new AiChatAttachment(r.Name, r.ResourceType, r.FileUrl, r.FileName, r.Detail))
            .ToListAsync(cancellationToken);

        var cacheFields = await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId).OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .Select(f => new CacheFieldInfo(f.FieldKey, f.Label, f.Description, f.IsUpdatable))
            .ToListAsync(cancellationToken);

        var cacheValues = await _db.AiAgentCacheValues.AsNoTracking()
            .Where(v => v.AgentId == agentId && v.SessionId == sessionId)
            .ToDictionaryAsync(v => v.FieldKey, v => v.Value, cancellationToken);

        var systemPrompt = await BuildSystemPrompt(agentId, systemPromptOverride ?? agent.SystemPrompt, resources, cacheFields, cacheValues, turns, autonomous, cancellationToken);

        var debugPrompts = new List<AiDebugPrompt>
        {
            new("Prompt principal del agente (enrutador + recursos + estado de cache)", DateTimeOffset.UtcNow, systemPrompt)
        };

        var disabledTools = ParseDisabledTools(agent.DisabledToolsJson);
        var (result, sessionCompleted) = await RunToolLoopAsync(
            agent.Provider, apiKey, providerCfg.BaseUrl, model, systemPrompt, turns, autonomous, actorUserId, disabledTools, debugPrompts, cancellationToken);

        if (result.Ok)
        {
            await _usage.RecordAsync(agent.Id, agent.Provider, model, result.InputTokens, result.OutputTokens, "agente", true, cancellationToken);
        }

        if (result.Ok && cacheFields.Count > 0 && !string.IsNullOrWhiteSpace(result.Text))
        {
            try
            {
                await ExtractAndStoreCacheUpdatesAsync(agentId, sessionId, agent.Provider, apiKey, providerCfg.BaseUrl, model,
                    cacheFields, cacheValues, turns, result.Text!, debugPrompts, cancellationToken);
            }
            catch { /* la extraccion no debe romper la respuesta */ }
        }

        // Cierre por herramienta: limpia la cache de la sesion (deja al agente listo para una nueva).
        if (sessionCompleted)
        {
            try { await _cache.ClearValuesAsync(agentId, sessionId, actorUserId, cancellationToken); }
            catch { /* limpiar la cache no debe romper la respuesta */ }
        }

        // Bitacora de atencion: persiste el rastro (entrada, prompts, herramientas, respuesta). Best-effort.
        try { await PersistRunLogsAsync(agent.TenantId, agentId, sessionId, turns, debugPrompts, result, cancellationToken); }
        catch { /* la bitacora nunca debe romper la respuesta */ }

        if (result.Ok && !string.IsNullOrEmpty(result.Text))
        {
            var (cleanText, attachments) = ExtractAttachments(result.Text!, resources);
            cleanText = StripToolCallArtifacts(cleanText, _allToolNames);
            return result with { Text = cleanText, Attachments = attachments, DebugPrompts = debugPrompts };
        }

        return result with { DebugPrompts = debugPrompts };
    }

    private string BuildDateContextLine()
    {
        var now = _clock.GetUtcNow().ToOffset(TenantOffset);
        var dia = SpanishDay(now.DayOfWeek);
        return $"FECHA Y HORA ACTUAL: hoy es {dia} {now:yyyy-MM-dd}, {now:HH:mm} (zona America/Bogota). " +
               "Usa SIEMPRE esta fecha como referencia para calcular dias relativos y para el parametro fecha " +
               "(AAAA-MM-DD) de las herramientas. NUNCA uses un anio distinto al actual.";
    }

    private static string SpanishDay(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miercoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sabado",
        _ => "domingo"
    };

    private async Task<(AiChatResult Result, bool SessionCompleted)> RunToolLoopAsync(
        AiProvider provider, string apiKey, string? baseUrl, string model, string systemPrompt,
        IReadOnlyList<AiChatTurn> turns, bool autonomous, long actorUserId, ISet<string> disabledTools,
        List<AiDebugPrompt> debugPrompts, CancellationToken ct)
    {
        var specs = new List<AiToolSpec>();
        var ownerByTool = new Dictionary<string, IAgentToolset>(StringComparer.OrdinalIgnoreCase);
        foreach (var ts in _toolsets)
        {
            foreach (var spec in ts.GetSpecs())
            {
                if (disabledTools.Contains(spec.Name) || ownerByTool.ContainsKey(spec.Name)) { continue; }
                ownerByTool[spec.Name] = ts;
                specs.Add(spec);
            }
        }

        var messages = new List<AiToolMessage>();
        foreach (var t in turns)
        {
            var role = string.Equals(t.Role, "model", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            messages.Add(new AiToolMessage(role, t.Text));
        }

        var totalIn = 0;
        var totalOut = 0;
        var sessionCompleted = false;
        string? lastText = null;

        for (var round = 1; round <= MaxToolRounds; round++)
        {
            var completion = await _client.CompleteWithToolsAsync(provider, apiKey, baseUrl, model, systemPrompt, messages, specs, ct);
            totalIn += completion.InputTokens;
            totalOut += completion.OutputTokens;

            if (!completion.Ok)
            {
                return (new AiChatResult(false, null, completion.Error, totalIn, totalOut), sessionCompleted);
            }

            if (completion.ToolCalls.Count == 0)
            {
                return (new AiChatResult(true, completion.Text ?? lastText, null, totalIn, totalOut), sessionCompleted);
            }

            lastText = completion.Text;
            var callsLog = new StringBuilder();
            foreach (var c in completion.ToolCalls) { callsLog.AppendLine($"-> {c.Name}({c.ArgumentsJson})"); }
            debugPrompts.Add(new AiDebugPrompt(
                $"IA solicito herramientas (ronda {round})",
                DateTimeOffset.UtcNow,
                (string.IsNullOrWhiteSpace(completion.Text) ? "" : completion.Text + "\n\n") + callsLog.ToString().TrimEnd()));

            messages.Add(new AiToolMessage("assistant", completion.Text, completion.ToolCalls));

            foreach (var call in completion.ToolCalls)
            {
                var owner = ownerByTool.GetValueOrDefault(call.Name);
                var exec = owner is null
                    ? new AgentToolResult(JsonSerializer.Serialize(new { ok = false, error = $"Herramienta no disponible o deshabilitada: {call.Name}" }))
                    : await owner.ExecuteAsync(call.Name, call.ArgumentsJson, actorUserId, autonomous, ct);
                if (exec.SessionCompleted) { sessionCompleted = true; }

                debugPrompts.Add(new AiDebugPrompt(
                    $"Herramienta ejecutada: {call.Name}",
                    DateTimeOffset.UtcNow,
                    $"Argumentos:\n{call.ArgumentsJson}",
                    exec.Json));

                messages.Add(new AiToolMessage("tool", exec.Json, null, call.Id, call.Name));
            }
        }

        return (new AiChatResult(true, lastText ?? "Estoy procesando tu solicitud, dame un momento.", null, totalIn, totalOut), sessionCompleted);
    }

    private async Task<string> BuildSystemPrompt(
        long agentId, string basePrompt, IReadOnlyList<AiChatAttachment> resources,
        IReadOnlyList<CacheFieldInfo> cacheFields, IReadOnlyDictionary<string, string?> cacheValues,
        IReadOnlyList<AiChatTurn> turns, bool autonomous, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BuildDateContextLine());
        sb.AppendLine();
        sb.AppendLine("REGLA CRITICA DE SALIDA: usa las herramientas SOLO por el mecanismo de funciones, NUNCA las escribas como texto. " +
            "El destinatario JAMAS debe ver nombres de funciones, llamadas ni JSON interno. Tu respuesta es unicamente el mensaje natural.");
        if (!autonomous)
        {
            sb.AppendLine();
            sb.AppendLine("MODO SUGERENCIA: no puedes confirmar acciones por ti mismo. Cuando corresponda, usa la herramienta para REGISTRAR la solicitud (quedara PENDIENTE de que un humano la confirme) e informalo. No afirmes que ya quedo confirmada.");
        }
        sb.AppendLine();
        sb.Append(ExpandResourceRefs(basePrompt, resources));

        var prompts = await _db.AiAgentPrompts.AsNoTracking()
            .Where(p => p.AgentId == agentId).OrderBy(p => p.SortOrder)
            .Select(p => new { p.Name, p.Rule, p.Body }).ToListAsync(ct);
        if (prompts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Enrutador de prompts: evalua el mensaje entrante y, si coincide alguna regla, sigue PRIMERO las instrucciones del prompt correspondiente (ademas del comportamiento base). Si ninguna aplica, usa el comportamiento base.");
            foreach (var p in prompts)
            {
                sb.AppendLine();
                sb.AppendLine($"### Prompt \"{p.Name}\"");
                sb.AppendLine($"Regla (cuando usarlo): {(string.IsNullOrWhiteSpace(p.Rule) ? "(sin regla; usar a criterio)" : p.Rule)}");
                sb.AppendLine($"Instrucciones: {ExpandResourceRefs(p.Body, resources)}");
            }
        }

        if (resources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Recursos disponibles. Cuando debas entregar el contenido de un recurso, NO lo reescribas: incluye en tu respuesta el marcador [[enviar: Nombre exacto del recurso]]. El sistema agrega el contenido tal cual.");
            foreach (var r in resources)
            {
                var kind = r.ResourceType == AgentResourceType.Text ? "Texto" : r.ResourceType.ToString();
                var desc = string.IsNullOrWhiteSpace(r.Detail) ? "archivo" : r.Detail;
                sb.AppendLine($"- ({kind}) {r.Name}: {desc}  -> entregar con [[enviar: {r.Name}]]");
            }
        }

        var captured = cacheFields
            .Where(f => cacheValues.TryGetValue(f.FieldKey, out var v) && !string.IsNullOrWhiteSpace(v))
            .ToList();
        if (captured.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("### Datos que ya conocemos (estado de la cache)");
            sb.AppendLine("Ya estan capturados. NO vuelvas a pedirlos y usalos para avanzar.");
            foreach (var f in captured)
            {
                sb.AppendLine($"- {f.FieldKey}: {cacheValues[f.FieldKey]}");
            }
        }

        if (turns.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("### Ultimos eventos (lo mas reciente al final)");
            var lastN = turns.Count > 5 ? turns.Skip(turns.Count - 5).ToList() : turns.ToList();
            foreach (var t in lastN)
            {
                var who = string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase) ? "Entrada" : "Agente";
                sb.AppendLine($"- {who}: {BuildTurnLine(t.Text, t.Attachments)}");
            }
        }

        return sb.ToString();
    }

    private async Task ExtractAndStoreCacheUpdatesAsync(
        long agentId, long sessionId, AiProvider provider, string apiKey, string? baseUrl, string model,
        IReadOnlyList<CacheFieldInfo> fields, IReadOnlyDictionary<string, string?> currentValues,
        IReadOnlyList<AiChatTurn> originalTurns, string botResponse, List<AiDebugPrompt> debugPrompts, CancellationToken ct)
    {
        var lastUser = originalTurns.LastOrDefault(t => string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase))?.Text ?? "";

        var sysSb = new StringBuilder();
        sysSb.AppendLine("Eres un extractor de datos. NO debes responder al destinatario.");
        sysSb.AppendLine("Lee la conversacion y devuelve un JSON plano con los campos que puedas inferir CON CERTEZA.");
        sysSb.AppendLine("Reglas:");
        sysSb.AppendLine("- NO inventes datos. Si no esta claro, NO incluyas el campo.");
        sysSb.AppendLine("- Si un campo ya tiene valor y no cambia, NO lo incluyas.");
        sysSb.AppendLine("- NO incluyas el valor literal \"PENDIENTE\".");
        sysSb.AppendLine("- Responde UNICAMENTE el JSON, sin markdown.");
        sysSb.AppendLine();
        sysSb.AppendLine("### Campos a capturar");
        foreach (var f in fields)
        {
            sysSb.AppendLine($"- {f.FieldKey}: {(string.IsNullOrWhiteSpace(f.Description) ? f.Label : f.Description)}");
        }
        sysSb.AppendLine();
        sysSb.AppendLine("### Estado actual de la cache");
        var anyKnown = false;
        foreach (var f in fields)
        {
            if (currentValues.TryGetValue(f.FieldKey, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                sysSb.AppendLine($"- {f.FieldKey} = {v}");
                anyKnown = true;
            }
        }
        if (!anyKnown) { sysSb.AppendLine("(vacio)"); }
        sysSb.AppendLine();
        sysSb.AppendLine("Si no hay nada nuevo, responde {}");

        var transcript = new StringBuilder();
        transcript.AppendLine("### Transcripcion de la conversacion");
        foreach (var t in originalTurns)
        {
            var who = string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase) ? "Entrada" : "Agente";
            transcript.AppendLine($"{who}: {BuildTurnLine(t.Text, t.Attachments)}");
        }
        transcript.AppendLine($"Agente (respuesta actual): {botResponse}");

        var userTurn = new AiChatTurn("user", transcript.ToString() + "\n\nDevuelve el JSON con los campos que puedas inferir CON CERTEZA.");

        var extractorSystemPrompt = sysSb.ToString();
        var extractorEntry = new AiDebugPrompt(
            $"Extractor de datos (ultima entrada: \"{Truncate(lastUser, 60)}\")",
            DateTimeOffset.UtcNow,
            extractorSystemPrompt + "\n\n---\n" + userTurn.Text);
        debugPrompts.Add(extractorEntry);
        var extractorIndex = debugPrompts.Count - 1;

        AiChatResult ext;
        try
        {
            ext = await _client.CompleteAsync(provider, apiKey, baseUrl, model, extractorSystemPrompt, new[] { userTurn }, ct);
        }
        catch (Exception callEx)
        {
            debugPrompts[extractorIndex] = extractorEntry with { Response = $"[Llamada fallida] {callEx.GetType().Name}: {callEx.Message}" };
            return;
        }

        debugPrompts[extractorIndex] = extractorEntry with { Response = ext.Ok ? (ext.Text ?? "(sin texto)") : $"[Sin Ok] {ext.Error}" };
        if (!ext.Ok || string.IsNullOrWhiteSpace(ext.Text)) { return; }

        await _usage.RecordAsync(agentId, provider, model, ext.InputTokens, ext.OutputTokens, "cache", true, ct);

        var json = StripJsonFromMarkdown(ext.Text!);
        Dictionary<string, JsonElement>? parsed;
        try { parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json); }
        catch { return; }
        if (parsed is null || parsed.Count == 0) { return; }

        var fieldKeys = fields.Select(f => f.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valEl) in parsed)
        {
            if (!fieldKeys.Contains(key)) { continue; }
            string? v = valEl.ValueKind switch
            {
                JsonValueKind.String => valEl.GetString(),
                JsonValueKind.Number => valEl.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Array => string.Join(", ", valEl.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                JsonValueKind.Object => valEl.GetRawText(),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(v)) { continue; }
            if (string.Equals(v.Trim(), "PENDIENTE", StringComparison.OrdinalIgnoreCase)) { continue; }
            try { await _cache.SetValueAsync(new SetAgentCacheValueRequest(agentId, sessionId, key, v.Trim(), "inference"), ct); }
            catch { /* la falla de un campo no aborta el resto */ }
        }
    }

    private static string StripJsonFromMarkdown(string text)
    {
        var t = text.Trim();
        t = Regex.Replace(t, @"^```(?:json)?\s*\n?", "", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\n?```\s*$", "");
        var i = t.IndexOf('{');
        var j = t.LastIndexOf('}');
        if (i >= 0 && j > i) { t = t.Substring(i, j - i + 1); }
        return t.Trim();
    }

    private static string ExpandResourceRefs(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{")) { return text; }
        return Regex.Replace(text, @"\{\{\s*([^}]+?)\s*\}\}", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is null) { return m.Value; }
            return $"el recurso \"{res.Name}\" (entregalo EXACTO incluyendo el marcador [[enviar: {res.Name}]])";
        });
    }

    private static (string, IReadOnlyList<AiChatAttachment>) ExtractAttachments(string text, IReadOnlyList<AiChatAttachment> resources)
    {
        var attachments = new List<AiChatAttachment>();
        var clean = Regex.Replace(text, @"\[\[\s*enviar\s*:\s*([^\]]+?)\s*\]\]", m =>
        {
            var res = FindResource(resources, m.Groups[1].Value);
            if (res is not null && attachments.All(a => a.Name != res.Name)) { attachments.Add(res); }
            return string.Empty;
        }, RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"[ \t]+\n", "\n").Trim();
        return (clean, attachments);
    }

    private static string StripToolCallArtifacts(string text, IReadOnlyCollection<string> toolNames)
    {
        if (string.IsNullOrEmpty(text) || toolNames.Count == 0) { return text; }
        var clean = text;
        foreach (var name in toolNames)
        {
            var pattern = $@"`?\b{Regex.Escape(name)}\s*\([^)]*\)`?";
            clean = Regex.Replace(clean, pattern, string.Empty, RegexOptions.IgnoreCase);
        }
        clean = Regex.Replace(clean, @"```[a-zA-Z]*\s*```", string.Empty);
        clean = Regex.Replace(clean, @"[ \t]+\n", "\n");
        clean = Regex.Replace(clean, @"\n{3,}", "\n\n");
        return clean.Trim();
    }

    private static AiChatAttachment? FindResource(IReadOnlyList<AiChatAttachment> resources, string name)
    {
        var key = Normalize(name);
        return resources.FirstOrDefault(r => Normalize(r.Name) == key);
    }

    private static string Normalize(string s)
    {
        var n = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) { sb.Append(c); }
        }
        return sb.ToString();
    }

    // Persiste el rastro de una corrida del agente en la bitacora (ai_agent_run_logs).
    private async Task PersistRunLogsAsync(long tenantId, long agentId, long sessionId, IReadOnlyList<AiChatTurn> turns,
        List<AiDebugPrompt> debugPrompts, AiChatResult result, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = new List<Domain.Entities.AiAgentRunLog>();
        void Add(AiAgentRunLogKind kind, string title, string? content, string? response = null) =>
            rows.Add(new Domain.Entities.AiAgentRunLog
            {
                TenantId = tenantId, AgentId = agentId, ConversationId = sessionId,
                OccurredAt = now, Kind = kind, Title = title, Content = content, Response = response
            });

        var lastUser = turns.LastOrDefault(t => string.Equals(t.Role, "user", StringComparison.OrdinalIgnoreCase))?.Text;
        if (!string.IsNullOrWhiteSpace(lastUser)) { Add(AiAgentRunLogKind.Inbound, "Entrada recibida", lastUser); }

        foreach (var d in debugPrompts)
        {
            var kind = d.Title.StartsWith("Herramienta ejecutada", StringComparison.OrdinalIgnoreCase)
                ? AiAgentRunLogKind.Tool : AiAgentRunLogKind.Prompt;
            Add(kind, d.Title, d.Content, d.Response);
        }

        if (result.Ok) { Add(AiAgentRunLogKind.Reply, "Respuesta del agente", result.Text); }
        else { Add(AiAgentRunLogKind.Error, "Error de atencion", result.Error); }

        _db.AiAgentRunLogs.AddRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    // Herramientas que el agente tiene DESHABILITADAS (AiAgent.DisabledToolsJson). Vacio = todas habilitadas.
    private static HashSet<string> ParseDisabledTools(string? json)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) { return set; }
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is not null) { foreach (var t in list) { if (!string.IsNullOrWhiteSpace(t)) { set.Add(t.Trim()); } } }
        }
        catch { /* json invalido: no deshabilitamos nada */ }
        return set;
    }

    private sealed record CacheFieldInfo(string FieldKey, string Label, string? Description, bool IsUpdatable);

    private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "...");

    private static string BuildTurnLine(string text, IReadOnlyList<AiChatAttachment>? attachments)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(text)) { sb.Append(text.Trim()); }
        if (attachments is { Count: > 0 })
        {
            foreach (var a in attachments)
            {
                if (sb.Length > 0) { sb.AppendLine(); }
                var desc = string.IsNullOrWhiteSpace(a.Detail) ? a.ResourceType.ToString() : a.Detail!.Trim();
                sb.Append($"[envio el recurso \"{a.Name}\" ({a.ResourceType}). Contenido: {desc}]");
            }
        }
        return sb.Length == 0 ? "(turno vacio)" : sb.ToString();
    }
}
