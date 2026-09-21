namespace Tronox.Application.Common;

/// <summary>
/// Estampa la cajita visual de firma sobre un documento IMAGEN (RQ05 - RF03-B, "estampa en imagenes").
/// Cuando el documento firmado no es PDF sino una imagen (jpg/png/tif/bmp), se dibuja la cajita (texto +
/// grafo) en una banda al pie de la imagen. No aplica PDF/A ni XMP (eso es solo PDF): la integridad la da
/// el SHA-256 del resultado. Best-effort: si falla, devuelve la imagen original.
/// </summary>
public interface IImageSignatureStamper
{
    /// <summary>true si el formato indicado es una imagen que este estampador soporta.</summary>
    bool Soporta(string? formato);

    /// <summary>Estampa la cajita en la imagen y devuelve los bytes resultantes (mismo formato si es posible).</summary>
    byte[] EstamparCajita(byte[] imagen, string? formato, CajitaFirma datos);
}
