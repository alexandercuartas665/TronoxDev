namespace Tronox.Application.Documentos;

/// <summary>Resultado de disparar el OCR de un documento (RF04).</summary>
public sealed record OcrReprocesoResult(bool Ok, string Estado, string? Error = null);

/// <summary>
/// OCR de documentos con Azure Computer Vision (Read API), RQ04 - RF04. La cuenta (endpoint + llave)
/// se lee de la config por entidad (<c>OcrConfig</c> en Datos de la Entidad); la llave se descifra en
/// tiempo de uso. Descarga el binario del object storage, extrae el texto y lo guarda en el documento.
/// </summary>
public interface IOcrService
{
    /// <summary>Reprocesa el OCR de un documento: valida config + binario, llama a Azure y guarda el texto.</summary>
    Task<OcrReprocesoResult> ReprocesarAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Estado + texto OCR actuales del documento (para el visor: op=ocr).</summary>
    Task<(string Estado, string? Texto)> GetEstadoAsync(long docId, CancellationToken cancellationToken = default);
}
