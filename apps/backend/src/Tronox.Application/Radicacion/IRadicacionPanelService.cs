namespace Tronox.Application.Radicacion;

/// <summary>
/// Servicio del Panel de Control de radicacion (RQ09 RF12-1). Calcula el dashboard (KPIs, series,
/// actividad) de la sucursal actual (tenant) para un rango de fechas. Solo LECTURA. Tenant-scoped por
/// el filtro global de EF. Replica la operacion "dashboard" del legacy rad_panel_op.ashx.
/// </summary>
public interface IRadicacionPanelService
{
    /// <summary>
    /// Dashboard del panel. <paramref name="desde"/>/<paramref name="hasta"/> acotan las series y (por
    /// fidelidad al legacy) NO los KPIs, que son siempre "actuales/hoy". Rango por defecto: ultimos 30
    /// dias; si hasta &lt; desde se ajusta hasta = desde.
    /// </summary>
    Task<RadicacionDashboardDto> GetDashboardAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken cancellationToken = default);
}
