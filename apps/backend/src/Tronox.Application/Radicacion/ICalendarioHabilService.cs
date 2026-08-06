namespace Tronox.Application.Radicacion;

/// <summary>
/// Calendario habil de la entidad (RQ01). Resuelve dias habiles (no sabado/domingo ni festivo) para el
/// calculo de terminos SLA de radicacion. Tenant-scoped. Siembra los festivos de Colombia por anio.
/// </summary>
public interface ICalendarioHabilService
{
    Task<bool> EsHabilAsync(DateOnly fecha, CancellationToken ct = default);

    /// <summary>Primer dia habil &gt;= fecha.</summary>
    Task<DateOnly> ProximoHabilAsync(DateOnly fecha, CancellationToken ct = default);

    /// <summary>Suma <paramref name="dias"/> dias habiles a partir de <paramref name="inicio"/> (exclusivo).</summary>
    Task<DateOnly> SumarDiasHabilesAsync(DateOnly inicio, int dias, CancellationToken ct = default);

    Task<IReadOnlyList<DiaFestivoDto>> ListarAsync(int anio, CancellationToken ct = default);
    Task<int> SembrarAnioAsync(int anio, CancellationToken ct = default);
    Task<DiaFestivoDto?> AgregarAsync(DateOnly fecha, string nombre, CancellationToken ct = default);
    Task<bool> EliminarAsync(long id, CancellationToken ct = default);
}

public sealed record DiaFestivoDto(long Id, DateOnly Fecha, string Nombre, bool EsNacional);
