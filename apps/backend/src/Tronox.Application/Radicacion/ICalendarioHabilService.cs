namespace Tronox.Application.Radicacion;

/// <summary>
/// Calendario habil de la entidad (RQ01, legacy ctrlCalendarioHabil). Resuelve dias habiles (segun los
/// dias de la semana configurados y sin festivos) para el calculo de terminos SLA de radicacion.
/// Tenant-scoped. Siembra los festivos de Colombia por anio y admite dias propios (Local/Institucional).
/// </summary>
public interface ICalendarioHabilService
{
    Task<bool> EsHabilAsync(DateOnly fecha, CancellationToken ct = default);

    /// <summary>Primer dia habil &gt;= fecha.</summary>
    Task<DateOnly> ProximoHabilAsync(DateOnly fecha, CancellationToken ct = default);

    /// <summary>Suma <paramref name="dias"/> dias habiles a partir de <paramref name="inicio"/> (exclusivo).</summary>
    Task<DateOnly> SumarDiasHabilesAsync(DateOnly inicio, int dias, CancellationToken ct = default);

    // ---- Configuracion (dias habiles de la semana + jornada) ----

    /// <summary>Config actual del tenant; devuelve el default (Lun-Vie, 08:00-17:00) si aun no existe.</summary>
    Task<CalendarioConfigDto> ObtenerConfigAsync(CancellationToken ct = default);

    /// <summary>Crea o actualiza la config del tenant. Devuelve Invalid si la jornada es incoherente.</summary>
    Task<CalendarioGuardarResult> GuardarConfigAsync(CalendarioConfigDto config, CancellationToken ct = default);

    // ---- Festivos ----

    Task<IReadOnlyList<DiaFestivoDto>> ListarAsync(int anio, CancellationToken ct = default);
    Task<int> SembrarAnioAsync(int anio, CancellationToken ct = default);
    Task<DiaFestivoDto?> AgregarAsync(DateOnly fecha, string nombre, string tipo = "Local", CancellationToken ct = default);
    Task<bool> EliminarAsync(long id, CancellationToken ct = default);
}

/// <summary>Dias habiles de la semana + jornada laboral de la entidad.</summary>
public sealed record CalendarioConfigDto(
    bool Lunes, bool Martes, bool Miercoles, bool Jueves, bool Viernes, bool Sabado, bool Domingo,
    string JornadaInicio, string JornadaFin)
{
    public static CalendarioConfigDto Default => new(
        Lunes: true, Martes: true, Miercoles: true, Jueves: true, Viernes: true, Sabado: false, Domingo: false,
        JornadaInicio: "08:00", JornadaFin: "17:00");
}

public enum CalendarioGuardarResult { Ok, JornadaInvalida }

public sealed record DiaFestivoDto(long Id, DateOnly Fecha, string Nombre, bool EsNacional, string Tipo);
