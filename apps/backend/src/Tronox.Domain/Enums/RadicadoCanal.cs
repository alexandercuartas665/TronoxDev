namespace Tronox.Domain.Enums;

/// <summary>
/// Canal de ingreso/salida del radicado. Espejo del legacy RAD_RADICADOS.CANAL. Migracion = cargado
/// desde el sistema anterior (no radicado en vivo por ventanilla).
/// </summary>
public enum RadicadoCanal
{
    Presencial,
    Web,
    Correo,
    Interno,
    Migracion
}
