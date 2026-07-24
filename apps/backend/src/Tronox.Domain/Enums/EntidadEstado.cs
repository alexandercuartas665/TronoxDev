namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de la entidad (RQ01 - RF01 seccion 4.1.1). La entidad NUNCA se elimina
/// (criterio de aceptacion 8 de RF01 e invariante 8): solo cambia de estado.
/// </summary>
public enum EntidadEstado
{
    Activo = 0,
    Inactivo = 1,
    Suspendido = 2
}
