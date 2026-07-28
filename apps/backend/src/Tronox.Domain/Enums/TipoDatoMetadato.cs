namespace Tronox.Domain.Enums;

/// <summary>
/// Tipo de dato de un metadato de expediente o documento (RQ02 - RF04 3.4.1 paso 6 / RF05 3.5.3).
/// Determina el control de UI con que se diligencia y como se valida.
/// </summary>
public enum TipoDatoMetadato
{
    /// <summary>Input de texto libre.</summary>
    TextoCorto = 0,

    /// <summary>Area de texto.</summary>
    TextoLargo = 1,

    /// <summary>Solo numeros.</summary>
    Numerico = 2,

    /// <summary>Date picker.</summary>
    Fecha = 3,

    /// <summary>Desplegable alimentado por una lista del Administrador de Listas (RF03).</summary>
    Lista = 4,

    /// <summary>Selector Si / No.</summary>
    Booleano = 5
}
