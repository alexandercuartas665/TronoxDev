namespace Tronox.Domain.Enums;

/// <summary>
/// Tipo de tarea de validacion de un documento (RQ04 - RF11). Son ACCIONES que dejan metadato, no
/// estados del documento ni compuertas del flujo (no bloquean el archivado).
/// </summary>
public enum TipoValidacion
{
    Revision = 0,
    Aprobacion = 1
}
