using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Admin;
using Tronox.Application.Common;
using Tronox.Application.Tenancy;

namespace Tronox.Application.Radicacion.Correos;

/// <summary>
/// Implementacion del clasificador PQRS con IA. Toma el primer proveedor global habilitado, descifra su
/// API key, invoca IAiProviderClient con el prompt clasificador (JSON PQRS-F), parsea la respuesta y
/// registra el consumo de tokens (source "correos-pqr"). Best-effort: si no hay proveedor o falla, Ok=false.
/// </summary>
public sealed class CorreoClasificadorIa : ICorreoClasificadorIa
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IAiProviderClient _ai;
    private readonly IAiUsageService _usage;

    public CorreoClasificadorIa(IApplicationDbContext db, ISecretProtector secret, IAiProviderClient ai, IAiUsageService usage)
    {
        _db = db;
        _secret = secret;
        _ai = ai;
        _usage = usage;
    }

    private const int MaxBodyParaIa = 8000;

    // Comportamiento POR DEFECTO del clasificador (editable via un agente de IA). Calca VISAL.
    private const string PromptComportamientoDefault = """
Eres un clasificador de PQRS-F (Peticiones, Quejas, Reclamos, Sugerencias, Felicitaciones) de una entidad
publica colombiana. Recibes un correo electronico (remitente, asunto y cuerpo). Determina si el correo es
una PQRS-F de un ciudadano y, si lo es, extrae sus datos.
""";

    // Contrato de SALIDA (FIJO del sistema): se anexa SIEMPRE al comportamiento del agente para que la
    // respuesta sea parseable, sin importar como el tenant edite el prompt de su agente clasificador.
    private const string ContratoJson = """
Responde SOLO con un JSON puro (sin explicaciones, sin ```), con estas claves exactas:
{
  "es_pqr": true|false,
  "tipo": "Peticion" | "Queja" | "Reclamo" | "Sugerencia" | "Felicitacion",
  "servicio": "",
  "descripcion": "",
  "nombres": "",
  "identificacion": "",
  "celular": "",
  "email": "",
  "atributo_calidad": "accesibilidad|oportunidad|seguridad|pertinencia|continuidad"
}

Reglas:
- Si el correo NO es una PQRS-F (spam, notificaciones automaticas, publicidad, boletines, correos internos),
  responde {"es_pqr": false}.
- No inventes datos: si un dato no aparece en el correo, deja la cadena vacia "".
- "descripcion" es un resumen breve y neutral del asunto de la peticion.
""";

    public async Task<ClasificacionCorreoResult> ClasificarAsync(string? remitente, string? asunto, string cuerpo, long? agentId = null, CancellationToken ct = default)
    {
        // Resolucion del motor: si el buzon apunta a un agente, se usa SU proveedor/modelo/comportamiento;
        // si no, se cae al primer proveedor habilitado con el comportamiento por defecto (compat. VISAL).
        Domain.Enums.AiProvider provider;
        string? cfgModel, cfgBaseUrl, cfgApiKeyEnc;
        string comportamiento;
        long? usageAgentId;

        if (agentId is long aid)
        {
            var agent = await _db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid, ct);
            if (agent is null) { return ClasificacionCorreoResult.Fail("El agente clasificador ya no existe."); }
            if (!agent.IsActive) { return ClasificacionCorreoResult.Fail("El agente clasificador esta apagado."); }
            var acfg = await _db.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == agent.Provider, ct);
            if (acfg is null || !acfg.IsEnabled || acfg.ApiKeyEncrypted is null)
            {
                return ClasificacionCorreoResult.Fail($"El proveedor {agent.Provider} del agente no esta habilitado en la plataforma.");
            }
            provider = agent.Provider;
            cfgModel = string.IsNullOrWhiteSpace(agent.Model) ? acfg.Model : agent.Model;
            cfgBaseUrl = acfg.BaseUrl;
            cfgApiKeyEnc = acfg.ApiKeyEncrypted;
            comportamiento = string.IsNullOrWhiteSpace(agent.SystemPrompt) ? PromptComportamientoDefault : agent.SystemPrompt;
            usageAgentId = agent.Id;
        }
        else
        {
            var cfg = await _db.AiProviderConfigs.AsNoTracking()
                .Where(c => c.IsEnabled && c.ApiKeyEncrypted != null)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync(ct);
            if (cfg is null) { return ClasificacionCorreoResult.Fail("No hay un proveedor de IA habilitado."); }
            provider = cfg.Provider;
            cfgModel = cfg.Model;
            cfgBaseUrl = cfg.BaseUrl;
            cfgApiKeyEnc = cfg.ApiKeyEncrypted;
            comportamiento = PromptComportamientoDefault;
            usageAgentId = null;
        }

        string apiKey;
        try { apiKey = _secret.Unprotect(cfgApiKeyEnc!); }
        catch { return ClasificacionCorreoResult.Fail("La API key del proveedor no se pudo descifrar."); }

        var meta = AiProviderCatalog.For(provider);
        var model = string.IsNullOrWhiteSpace(cfgModel) ? meta.DefaultModel : cfgModel!;
        var systemPrompt = comportamiento + "\n\n" + ContratoJson;
        var body = cuerpo.Length > MaxBodyParaIa ? cuerpo[..MaxBodyParaIa] : cuerpo;
        var userPrompt = $"De: {remitente}\nAsunto: {asunto}\n\nCuerpo:\n{body}";

        var turns = new List<AiChatTurn> { new("user", userPrompt) };
        AiChatResult ia;
        try
        {
            ia = await _ai.CompleteAsync(provider, apiKey, cfgBaseUrl ?? meta.DefaultBaseUrl,
                model, systemPrompt, turns, ct);
        }
        catch (Exception ex) { return ClasificacionCorreoResult.Fail($"Fallo la IA: {ex.Message}"); }

        // Registra el consumo (best-effort), atribuido al agente si lo hay.
        try { await _usage.RecordAsync(usageAgentId, provider, model, ia.InputTokens, ia.OutputTokens, "correos-pqr", ia.Ok, ct); }
        catch { /* no romper por el registro de uso */ }

        if (!ia.Ok || string.IsNullOrWhiteSpace(ia.Text))
        {
            return new ClasificacionCorreoResult(false, false, null, null, null, null, null, null, null, null,
                ia.Text, ia.InputTokens, ia.OutputTokens, ia.Error ?? "La IA no devolvio texto.");
        }

        return Parse(ia.Text, ia.InputTokens, ia.OutputTokens);
    }

    private static ClasificacionCorreoResult Parse(string text, int inTok, int outTok)
    {
        // Extrae el primer objeto {...} (tolera fences ```json y prosa alrededor).
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return new ClasificacionCorreoResult(false, false, null, null, null, null, null, null, null, null,
                text, inTok, outTok, "La IA no devolvio un JSON valido.");
        }
        var json = text.Substring(start, end - start + 1);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? S(params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                    {
                        var s = v.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) { return s.Trim(); }
                    }
                }
                return null;
            }
            var esPqr = EsVerdadero(root);
            return new ClasificacionCorreoResult(
                true, esPqr,
                S("tipo", "tipo_pqrs", "tipo_pqrs_f"),
                S("servicio", "servicio_pqrs"),
                S("descripcion", "resumen"),
                S("nombres", "nombre", "nombre_completo"),
                S("identificacion", "documento", "cedula"),
                S("celular", "telefono"),
                S("email", "correo"),
                S("atributo_calidad"),
                json, inTok, outTok, null);
        }
        catch
        {
            return new ClasificacionCorreoResult(false, false, null, null, null, null, null, null, null, null,
                json, inTok, outTok, "El JSON de la IA no se pudo parsear.");
        }
    }

    private static bool EsVerdadero(JsonElement root)
    {
        if (!root.TryGetProperty("es_pqr", out var v)) { return false; }
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => v.GetString()?.Trim().ToLowerInvariant() is "si" or "sí" or "true" or "1",
            _ => false
        };
    }
}
