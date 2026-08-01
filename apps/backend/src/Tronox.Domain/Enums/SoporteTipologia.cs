namespace Tronox.Domain.Enums;

/// <summary>
/// Soporte de un tipo documental (RQ02 - RF05 3.5.2). Determina en que medio existe el documento
/// dentro del expediente. Persistido como texto acotado.
/// </summary>
public enum SoporteTipologia
{
    /// <summary>Solo existe en medio fisico (papel u otro soporte material).</summary>
    Fisico = 0,

    /// <summary>Solo existe en medio electronico (default).</summary>
    Electronico = 1,

    /// <summary>Existe en ambos soportes (fisico y electronico).</summary>
    Hibrido = 2
}
