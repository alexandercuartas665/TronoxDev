namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de un rol de permisos (RQ01 - RF05). Un rol Inactivo NO aporta permisos en la
/// resolucion efectiva: sigue existiendo y conserva su matriz y sus asignaciones, pero deja de
/// conceder (fail-closed, invariante 10). Se persiste como texto acotado.
/// </summary>
public enum RolEstado
{
    Activo = 0,
    Inactivo
}
