namespace Tronox.Domain.Enums;

/// <summary>
/// Estado del procesamiento OCR de un documento (RQ04 - RF04). El OCR real es asincrono (Workers) y se
/// difiere a un slice posterior; aqui es la dimension de datos. Al subir un PDF/imagen se marca
/// Pendiente; el resto de formatos, NoAplica.
/// </summary>
public enum OcrEstadoDocumento
{
    NoAplica = 0,
    Pendiente = 1,
    Procesando = 2,
    Completado = 3,
    Error = 4
}
