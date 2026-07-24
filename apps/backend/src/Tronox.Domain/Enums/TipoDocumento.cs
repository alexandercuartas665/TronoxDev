namespace Tronox.Domain.Enums;

/// <summary>
/// Tipo de documento de identidad de un funcionario (RQ01 - RF06 seccion 5.6.1).
///
/// Se persiste como STRING (convencion del proyecto), no como entero: el valor viaja a pistas de
/// auditoria y reportes, donde un 0/1/2 no dice nada.
/// </summary>
public enum TipoDocumento
{
    /// <summary>Cedula de ciudadania.</summary>
    CC,
    /// <summary>Cedula de extranjeria.</summary>
    CE,
    Pasaporte,
    /// <summary>NIT (personas juridicas; RF06 lo admite para casos de contratistas).</summary>
    NIT
}
