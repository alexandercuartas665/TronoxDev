namespace Tronox.Application.Radicacion;

/// <summary>
/// Bandeja de radicados (RQ09 RF11, port de rad_bandeja). Lista unificada E/S/I con tabs, filtros y
/// contadores, acotada por la visibilidad del usuario (fail-closed). Solo lectura; las acciones
/// (distribuir, ver detalle) viven en sus servicios. Tenant-scoped por el filtro global.
/// </summary>
public interface IRadicacionBandejaService
{
    Task<BandejaResultDto> ListarAsync(BandejaFiltro filtro, CancellationToken ct = default);
    Task<BandejaContadoresDto> ContadoresAsync(BandejaFiltro filtro, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> TiposAsync(CancellationToken ct = default);
    /// <summary>Tipos con banderas para el asistente rad_radicar (direccion, PQRSD/Tutela, anonimo, nivel default).</summary>
    Task<IReadOnlyList<TipoRadicacionDto>> TiposAsistenteAsync(CancellationToken ct = default);
    /// <summary>Niveles de clasificacion activos para el selector de reserva del asistente.</summary>
    Task<IReadOnlyList<OpcionDto>> NivelesReservaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> DependenciasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> FuncionariosAsync(long? dependenciaId, CancellationToken ct = default);
}
