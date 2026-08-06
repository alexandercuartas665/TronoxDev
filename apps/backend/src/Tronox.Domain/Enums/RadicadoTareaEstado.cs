namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de una tarea de distribucion (RAD_TAREAS.ESTADO). Ciclo: Asignada -> Aceptada/Rechazada/
/// Reasignada. La vigencia real la marca la columna aparte Activa (bit), no este enum.
/// </summary>
public enum RadicadoTareaEstado
{
    Asignada,
    Aceptada,
    Rechazada,
    Reasignada
}
