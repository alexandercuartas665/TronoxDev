namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de una sede fisica del tenant (RQ01 - RF01 seccion 4.1.2).
/// Una sede Inactiva sigue existiendo (nunca se borra) pero NO se ofrece al crear fondos.
/// </summary>
public enum SedeEstado
{
    Activo = 0,
    Inactivo = 1
}
