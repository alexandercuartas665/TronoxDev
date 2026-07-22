namespace Tronox.Domain.Enums;

/// <summary>
/// Estado del fondo documental (RQ01 - RF02).
/// Cerrado es SOLO LECTURA: no admite nada nuevo colgando de el, pero si consulta y
/// exportacion sin limite. Ningun estado implica borrado fisico (invariante 8).
/// </summary>
public enum FondoEstado
{
    Activo = 0,
    Inactivo = 1,

    /// <summary>
    /// Fondo cerrado. Requiere FechaCierre (posterior a FechaApertura) y bloquea toda
    /// creacion de subfondos, series y expedientes que cuelguen de el.
    /// </summary>
    Cerrado = 2
}
