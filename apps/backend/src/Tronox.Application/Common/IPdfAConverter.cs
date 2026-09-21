namespace Tronox.Application.Common;

/// <summary>
/// Convierte un PDF a PDF/A-2b para el archivado de la firma (RQ05 - RF02, Decreto 2364/2012). Calca
/// PdfAConverter del legacy: usa LibreOffice headless (soffice) como motor de conversion. Es BEST-EFFORT:
/// si soffice no esta disponible (p. ej. en local) o la conversion falla, devuelve null y el flujo de
/// firma sigue con el PDF sin convertir (la integridad la da igual el hash SHA-256). La ejecucion real
/// solo ocurre en la imagen de prod, que trae LibreOffice.
/// </summary>
public interface IPdfAConverter
{
    /// <summary>true si el binario de LibreOffice esta disponible en este entorno.</summary>
    bool Disponible { get; }

    /// <summary>Convierte a PDF/A-2b. Devuelve el PDF/A, o null si no se pudo (el caller usa el original).</summary>
    Task<byte[]?> ConvertirPdfAAsync(byte[] pdf, CancellationToken cancellationToken = default);
}
