namespace Tronox.Domain.Enums;

/// <summary>
/// Naturaleza de la entidad titular del tenant (RQ01 - RF01 seccion 4.1.1).
///
/// NO es cosmetico: si la entidad es Publica, la spec vuelve OBLIGATORIOS el codigo DIVIPOLA
/// y el codigo de fondo AGN (criterio de aceptacion 4 de RF01), y RF04 muestra el codigo DAFP
/// del cargo. La regla vive en EntidadRules, no en la UI.
/// </summary>
public enum TipoEntidad
{
    Publica = 0,
    Privada = 1,
    Mixta = 2
}
