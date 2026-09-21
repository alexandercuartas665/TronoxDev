namespace Tronox.Application.Radicacion;

/// <summary>
/// Configuracion PQR (RQ09 RF01, port de las secciones Prioridades + Portal Web de rad_config, que TRONOX
/// no tenia). Pantalla aparte de "Configuracion Radicacion". Tenant-scoped. Siembra las 3 prioridades base.
/// </summary>
public interface IConfiguracionPqrService
{
    Task<IReadOnlyList<PrioridadDto>> ListarPrioridadesAsync(CancellationToken ct = default);
    Task<PrioridadDto> CrearPrioridadAsync(CancellationToken ct = default);
    Task GuardarPrioridadesAsync(IReadOnlyList<PrioridadDto> prioridades, CancellationToken ct = default);
    /// <summary>Inactiva una prioridad no base (invariante 8: no se elimina fisicamente).</summary>
    Task<bool> InactivarPrioridadAsync(long id, CancellationToken ct = default);

    Task<PortalConfigDto> GetPortalConfigAsync(CancellationToken ct = default);
    Task GuardarPortalConfigAsync(PortalConfigDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<TipoWebDto>> ListarTiposWebAsync(CancellationToken ct = default);
    Task ToggleTipoWebAsync(long tipoId, bool habilitado, CancellationToken ct = default);
}
