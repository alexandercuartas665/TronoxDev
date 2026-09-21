namespace Tronox.Application.Radicacion;

/// <summary>
/// Configuracion del modulo de Radicacion (RQ09 RF01): consecutivos + alertas SLA (singleton por
/// tenant), notificaciones por evento y bitacora de migracion historica. La primera lectura CREA la
/// config y SIEMBRA los 13 tipos de comunicacion base + los eventos de notificacion. Tenant-scoped.
/// </summary>
public interface IRadicacionConfigService
{
    /// <summary>Config del tenant; la crea (y siembra base) si no existe. Incluye la sigla de la Entidad.</summary>
    Task<RadicacionConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Guarda consecutivos (inicio editable solo antes del primer radicado) + parametros SLA.</summary>
    Task<RadicacionResult<RadicacionConfigDto>> SaveConfigAsync(SaveRadicacionConfigRequest request, long actorUserId, CancellationToken cancellationToken = default);

    // ---- RF01-5 Notificaciones ----
    Task<IReadOnlyList<NotificacionRadicacionDto>> ListNotificacionesAsync(CancellationToken cancellationToken = default);
    Task<RadicacionResult<NotificacionRadicacionDto>> SaveNotificacionAsync(long id, SaveNotificacionRadicacionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    // ---- RF01-6 Migracion historica (la corrida real es integracion posterior) ----
    Task<IReadOnlyList<MigracionRadicadosDto>> ListMigracionesAsync(CancellationToken cancellationToken = default);
}
