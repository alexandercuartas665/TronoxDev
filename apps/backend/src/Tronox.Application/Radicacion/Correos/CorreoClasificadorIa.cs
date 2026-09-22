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

    // System prompt del clasificador (fijo del sistema, calca PromptClasificador de VISAL).
    private const string PromptClasificador = """
Eres un clasificador de PQRS-F (Peticiones, Quejas, Reclamos, Sugerencias, Felicitaciones) de una entidad
publica colombiana. Recibes un correo electronico (remitente, asunto y cuerpo). Determina si el correo es
una PQRS-F de un ciudadano y, si lo es, extrae sus datos.

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

    public async Task<ClasificacionCorreoResult> ClasificarAsync(string? remitente, string? asunto, string cuerpo, CancellationToken ct = default)
    {
        var cfg = await _db.AiProviderConfigs.AsNoTracking()
            .Where(c => c.IsEnabled && c.ApiKeyEncrypted != null)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(ct);
        if (cfg is null) { return ClasificacionCorreoResult.Fail("No hay un proveedor de IA habilitado."); }

        string apiKey;
        try { apiKey = _secret.Unprotect(cfg.ApiKeyEncrypted!); }
        catch { return ClasificacionCorreoResult.Fail("La API key del proveedor no se pudo descifrar."); }

        var meta = AiProviderCatalog.For(cfg.Provider);
        var model = string.IsNullOrWhiteSpace(cfg.Model) ? meta.DefaultModel : cfg.Model!;
        var body = cuerpo.Length > MaxBodyParaIa ? cuerpo[..MaxBodyParaIa] : cuerpo;
        var userPrompt = $"De: {remitente}\nAsunto: {asunto}\n\nCuerpo:\n{body}";

        var turns = new List<AiChatTurn> { new("user", userPrompt) };
        AiChatResult ia;
        try
        {
            ia = await _ai.CompleteAsync(cfg.Provider, apiKey, cfg.BaseUrl ?? meta.DefaultBaseUrl,
                model, PromptClasificador, turns, ct);
        }
        catch (Exception ex) { return ClasificacionCorreoResult.Fail($"Fallo la IA: {ex.Message}"); }

        // Registra el consumo (best-effort).
        try { await _usage.RecordAsync(null, cfg.Provider, model, ia.InputTokens, ia.OutputTokens, "correos-pqr", ia.Ok, ct); }
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
