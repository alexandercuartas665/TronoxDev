namespace Tronox.Application.Common;

/// <summary>Datos de la cajita visual de firma (RQ05 - RF03-B), calca DatosCajita del legacy.</summary>
public sealed record CajitaFirma(
    string Nombre,
    string? Cargo,
    string? Dependencia,
    string Fecha,
    string VerificarUrl);

/// <summary>
/// Estampa la cajita visual de firma en un PDF (RQ05 - RF03-B). En el primer slice es una cajita de
/// texto al pie derecho (nombre/cargo/dependencia/fecha + leyenda "Firmado electronicamente - TRONOX").
/// El QR, PDF/A y el sellado XMP quedan para el modulo RQ05 completo. Best-effort: si falla devuelve el
/// PDF original (la firma no se frena por la cajita, calca el try/catch del legacy).
/// </summary>
public interface IPdfSignatureStamper
{
    byte[] EstamparCajita(byte[] pdf, CajitaFirma datos);
}
