namespace Tronox.Domain.Enums;

/// <summary>
/// Tipo de firma electronica (RQ05 - FIR_FIRMAS.TIPO_FIRMA). En el primer slice solo se ejecuta la
/// firma <see cref="Electronica"/> (firma directa, sin certificado). <see cref="DigitalCertificada"/>
/// queda como valor del contrato para el circuito con certificado (diferido al modulo RQ05 completo).
/// </summary>
public enum TipoFirma
{
    Electronica = 0,
    DigitalCertificada = 1
}
