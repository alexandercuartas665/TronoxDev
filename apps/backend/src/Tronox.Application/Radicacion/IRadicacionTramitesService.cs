namespace Tronox.Application.Radicacion;

/// <summary>
/// Bandeja de Tramites / Mis Tareas (RQ09 RF07-3, port de rad_tramites). El funcionario gestiona las tareas
/// que le distribuyeron: aceptar, rechazar, y (si es jefe) asignar funcionario a una tarea sin asignar.
/// Visibilidad fail-closed: cada usuario ve SUS tareas + las de su dependencia sin asignar (invariante 10),
/// no la sucursal entera como el legacy. Redirigir usa el servicio de distribucion existente.
/// </summary>
public interface IRadicacionTramitesService
{
    Task<TramitesResultDto> ListarAsync(TramitesFiltro filtro, CancellationToken ct = default);
    /// <summary>Aceptar una tarea ASIGNADA (la toma quien acepta si no tenia funcionario). Radicado -> En Tramite.</summary>
    Task<TareaResult> AceptarAsync(long tareaId, CancellationToken ct = default);
    /// <summary>Rechazar con observacion obligatoria. Si no quedan tareas activas, devuelve el radicado.</summary>
    Task<TareaResult> RechazarAsync(long tareaId, string observacion, CancellationToken ct = default);
    /// <summary>Jefe asigna funcionario a una tarea sin asignar. Distribuido -> En Tramite.</summary>
    Task<TareaResult> AsignarAsync(long tareaId, long funcionarioId, CancellationToken ct = default);
}
