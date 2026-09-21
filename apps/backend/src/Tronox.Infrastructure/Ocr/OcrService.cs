using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tronox.Application.Common;
using Tronox.Application.Documentos;
using Tronox.Domain.Enums;

namespace Tronox.Infrastructure.Ocr;

/// <summary>
/// OCR con Azure Computer Vision (Read API v3.2), RQ04 - RF04. Calcado del OcrDocumentoHelper legacy:
/// resuelve la cuenta (endpoint + llave) de la config por entidad (OcrConfig, Datos de la Entidad),
/// descarga el binario del object storage, lanza el analyze, hace polling del resultado y guarda el
/// texto + OcrEstado en el documento. La llave se descifra solo en tiempo de uso.
/// </summary>
public sealed class OcrService : IOcrService
{
    private readonly HttpClient _http;
    private readonly IApplicationDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly ISecretProtector _protector;
    private readonly IAuditWriter _audit;
    private readonly ITenantContext _tenant;
    private readonly ILogger<OcrService> _log;

    public OcrService(
        HttpClient http, IApplicationDbContext db, IObjectStorage storage, ISecretProtector protector,
        IAuditWriter audit, ITenantContext tenant, ILogger<OcrService> log)
    {
        _http = http;
        _db = db;
        _storage = storage;
        _protector = protector;
        _audit = audit;
        _tenant = tenant;
        _log = log;
    }

    public async Task<(string Estado, string? Texto)> GetEstadoAsync(long docId, CancellationToken ct = default)
    {
        var d = await _db.Documentos.AsNoTracking()
            .Where(x => x.Id == docId).Select(x => new { x.OcrEstado, x.OcrTexto }).FirstOrDefaultAsync(ct);
        return (d?.OcrEstado.ToString() ?? nameof(OcrEstadoDocumento.NoAplica), d?.OcrTexto);
    }

    public async Task<OcrReprocesoResult> ReprocesarAsync(long docId, long actorUserId, CancellationToken ct = default)
    {
        // 1) Config de OCR de la entidad (endpoint + llave). Sin config activa no se ejecuta.
        var cfg = await _db.OcrConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is null || !cfg.Activo || string.IsNullOrWhiteSpace(cfg.Endpoint) || string.IsNullOrWhiteSpace(cfg.ApiKeyCifrada))
        {
            return new OcrReprocesoResult(false, "NoAplica",
                "Azure Computer Vision no esta configurado (Datos de la Entidad). El OCR no puede ejecutarse hasta configurarlo.");
        }

        // 2) Documento + binario.
        var doc = await _db.Documentos.FirstOrDefaultAsync(x => x.Id == docId && x.Estado != EstadoDocumento.Anulado, ct);
        if (doc is null) { return new OcrReprocesoResult(false, "NoAplica", "El documento no existe."); }
        if (!doc.TieneBinario || string.IsNullOrWhiteSpace(doc.RutaAlmacenamiento))
        {
            return new OcrReprocesoResult(false, doc.OcrEstado.ToString(), "El documento no tiene binario para indexar.");
        }
        var formato = (doc.Formato ?? "").Trim().ToLowerInvariant();
        if (formato is not ("pdf" or "jpg" or "jpeg" or "png" or "tif" or "tiff" or "bmp"))
        {
            return new OcrReprocesoResult(false, doc.OcrEstado.ToString(), "El formato del documento no admite OCR.");
        }

        // 3) Estado Procesando (persistido para que el visor lo refleje).
        doc.OcrEstado = OcrEstadoDocumento.Procesando;
        await _db.SaveChangesAsync(ct);

        try
        {
            byte[] bytes;
            await using (var s = await _storage.GetAsync(doc.RutaAlmacenamiento, ct))
            {
                if (s is null) { throw new InvalidOperationException("No se pudo leer el binario del almacenamiento."); }
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var texto = await LeerTextoAzureAsync(cfg.Endpoint!.Trim(), _protector.Unprotect(cfg.ApiKeyCifrada!), bytes, ct);

            doc.OcrEstado = OcrEstadoDocumento.Completado;
            doc.OcrTexto = texto;
            _audit.Write(actorUserId, "documento.ocr", nameof(Tronox.Domain.Entities.Documento), doc,
                previousValue: null, newValue: new { doc.OcrEstado, Longitud = texto?.Length ?? 0 }, tenantId: _tenant.TenantId);
            await _db.SaveChangesAsync(ct);
            return new OcrReprocesoResult(true, doc.OcrEstado.ToString());
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "OCR fallo para documento {DocId}", docId);
            doc.OcrEstado = OcrEstadoDocumento.Error;
            await _db.SaveChangesAsync(CancellationToken.None);
            return new OcrReprocesoResult(false, doc.OcrEstado.ToString(), "No se pudo ejecutar el OCR: " + ex.Message);
        }
    }

    /// <summary>Azure Computer Vision Read API v3.2: POST analyze -&gt; polling analyzeResults -&gt; texto.</summary>
    private async Task<string> LeerTextoAzureAsync(string endpoint, string key, byte[] bytes, CancellationToken ct)
    {
        var baseUrl = endpoint.EndsWith('/') ? endpoint : endpoint + "/";
        using var analyze = new HttpRequestMessage(HttpMethod.Post, baseUrl + "vision/v3.2/read/analyze")
        {
            Content = new ByteArrayContent(bytes)
        };
        analyze.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        analyze.Headers.Add("Ocp-Apim-Subscription-Key", key);

        using var analyzeResp = await _http.SendAsync(analyze, ct);
        if (!analyzeResp.IsSuccessStatusCode)
        {
            var body = await analyzeResp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Azure respondio {(int)analyzeResp.StatusCode}: {Recorta(body)}");
        }
        if (analyzeResp.Headers.Location is null && !analyzeResp.Headers.TryGetValues("Operation-Location", out _))
        {
            throw new InvalidOperationException("Azure no devolvio la ubicacion del resultado (Operation-Location).");
        }
        var opLocation = analyzeResp.Headers.TryGetValues("Operation-Location", out var vals)
            ? vals.First()
            : analyzeResp.Headers.Location!.ToString();

        // Polling: hasta ~30s (30 intentos x 1s).
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(1000, ct);
            using var poll = new HttpRequestMessage(HttpMethod.Get, opLocation);
            poll.Headers.Add("Ocp-Apim-Subscription-Key", key);
            using var pollResp = await _http.SendAsync(poll, ct);
            var json = await pollResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
            if (status is "succeeded") { return ExtraerTexto(doc.RootElement); }
            if (status is "failed") { throw new InvalidOperationException("Azure reporto el analisis como fallido."); }
        }
        throw new TimeoutException("El OCR no termino a tiempo (timeout de 30s).");
    }

    private static string ExtraerTexto(JsonElement root)
    {
        var sb = new System.Text.StringBuilder();
        if (root.TryGetProperty("analyzeResult", out var ar) && ar.TryGetProperty("readResults", out var pages))
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (!page.TryGetProperty("lines", out var lines)) { continue; }
                foreach (var line in lines.EnumerateArray())
                {
                    if (line.TryGetProperty("text", out var t)) { sb.AppendLine(t.GetString()); }
                }
            }
        }
        return sb.ToString().Trim();
    }

    private static string Recorta(string s) => s.Length <= 300 ? s : s[..300];
}
