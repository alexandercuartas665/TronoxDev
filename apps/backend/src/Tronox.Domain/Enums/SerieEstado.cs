namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de una serie o subserie del catalogo documental (RQ02 - RF02).
/// No hay borrado fisico (invariante 8): una serie en uso o ya asignada a la TRD se INACTIVA,
/// nunca se elimina. Una serie Inactiva no se ofrece para nuevas asignaciones en RF04.
/// </summary>
public enum SerieEstado
{
    Activo = 0,
    Inactivo = 1
}
