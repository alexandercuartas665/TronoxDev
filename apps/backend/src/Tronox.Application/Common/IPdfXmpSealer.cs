namespace Tronox.Application.Common;

/// <summary>Datos que el sellado XMP escribe en el PDF/A (RQ05 - RF02/RF03), calca DatosSellado del legacy.</summary>
public sealed record DatosSellado(
    long Tenant,
    long DocumentoId,
    string FirmantePrincipal,
    int TotalFirmantes,
    DateTimeOffset Timestamp,
    string TipoFirma,
    string VerificarUrl);

/// <summary>Resultado del sellado XMP: el PDF/A con el bloque tronox: + el hash byte-range calculado.</summary>
public sealed record SelladoResultado(byte[] Pdf, string Hash);

/// <summary>
/// Sellado XMP length-neutral con hash byte-range excluido (RQ05 - RF02/RF03), port de FirmaSelladoHelper.
/// Inserta un bloque de metadatos <c>tronox:</c> dentro del xpacket XMP de un PDF/A CONSUMIENDO su padding
/// (el tamano y los offsets del PDF no cambian), y calcula un SHA-256 que EXCLUYE todo el paquete XMP, de
/// modo que guardar el hash dentro del propio XMP no lo auto-invalida (patron PAdES). El verificador
/// reaplica <see cref="RecalcularHash"/> para comparar. Requiere un PDF/A con xpacket + padding (lo
/// produce la conversion LibreOffice). Best-effort: devuelve null si el PDF no trae la estructura o no
/// hay padding suficiente (el caller sella con el SHA-256 del PDF completo).
/// </summary>
public interface IPdfXmpSealer
{
    /// <summary>Sella el PDF/A: inserta el bloque tronox: y devuelve el PDF + su hash byte-range. null si no se puede.</summary>
    SelladoResultado? Sellar(byte[] pdfA, DatosSellado datos);

    /// <summary>Recalcula el hash byte-range (excluyendo el xpacket) de un PDF ya sellado. null si no aplica.</summary>
    string? RecalcularHash(byte[] pdf);
}
