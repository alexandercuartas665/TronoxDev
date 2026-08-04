namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de una tarea de validacion (RQ04 - RF12). Pendiente hasta que el asignado responde. Devolver
/// y Rechazar exigen comentario. La respuesta es traza INMUTABLE y NO cambia el estado del documento.
/// </summary>
public enum EstadoValidacion
{
    Pendiente = 0,
    Aprobado = 1,
    Devuelto = 2,
    Rechazado = 3
}
