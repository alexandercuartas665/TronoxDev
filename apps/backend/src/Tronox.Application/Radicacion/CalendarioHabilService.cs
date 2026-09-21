using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del calendario habil. Un dia es habil si su dia de la semana esta marcado como habil en
/// la config del tenant (por defecto Lun-Vie) y no es festivo. Los festivos se cargan de dias_festivos
/// (sembrada con FestivosColombia). Tenant-scoped por el filtro global de EF.
/// </summary>
public sealed class CalendarioHabilService : ICalendarioHabilService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public CalendarioHabilService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private async Task<HashSet<DateOnly>> FestivosAsync(int anioDesde, int anioHasta, CancellationToken ct)
    {
        var desde = new DateOnly(anioDesde, 1, 1);
        var hasta = new DateOnly(anioHasta, 12, 31);
        var fs = await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta).Select(f => f.Fecha).ToListAsync(ct);
        return fs.ToHashSet();
    }

    /// <summary>Config del tenant o el default en memoria (sin persistir) para los calculos de habiles.</summary>
    private async Task<CalendarioHabilConfig> ConfigModelAsync(CancellationToken ct)
    {
        var cfg = await _db.CalendariosHabiles.AsNoTracking().FirstOrDefaultAsync(ct);
        return cfg ?? new CalendarioHabilConfig();
    }

    private static bool DiaMarcadoHabil(CalendarioHabilConfig c, DateOnly f) => f.DayOfWeek switch
    {
        DayOfWeek.Monday => c.Lunes,
        DayOfWeek.Tuesday => c.Martes,
        DayOfWeek.Wednesday => c.Miercoles,
        DayOfWeek.Thursday => c.Jueves,
        DayOfWeek.Friday => c.Viernes,
        DayOfWeek.Saturday => c.Sabado,
        DayOfWeek.Sunday => c.Domingo,
        _ => false
    };

    public async Task<bool> EsHabilAsync(DateOnly fecha, CancellationToken ct = default)
    {
        var cfg = await ConfigModelAsync(ct);
        if (!DiaMarcadoHabil(cfg, fecha)) { return false; }
        var fest = await FestivosAsync(fecha.Year, fecha.Year, ct);
        return !fest.Contains(fecha);
    }

    public async Task<DateOnly> ProximoHabilAsync(DateOnly fecha, CancellationToken ct = default)
    {
        var cfg = await ConfigModelAsync(ct);
        var fest = await FestivosAsync(fecha.Year, fecha.Year + 1, ct);
        var f = fecha;
        var guard = 0;
        while ((!DiaMarcadoHabil(cfg, f) || fest.Contains(f)) && guard++ < 3650) { f = f.AddDays(1); }
        return f;
    }

    public async Task<DateOnly> SumarDiasHabilesAsync(DateOnly inicio, int dias, CancellationToken ct = default)
    {
        var cfg = await ConfigModelAsync(ct);
        var fest = await FestivosAsync(inicio.Year, inicio.Year + 2, ct);
        var f = inicio;
        var restantes = dias;
        var guard = 0;
        while (restantes > 0 && guard++ < 3650)
        {
            f = f.AddDays(1);
            if (DiaMarcadoHabil(cfg, f) && !fest.Contains(f)) { restantes--; }
        }
        return f;
    }

    // ---- Configuracion ----

    public async Task<CalendarioConfigDto> ObtenerConfigAsync(CancellationToken ct = default)
    {
        var c = await _db.CalendariosHabiles.AsNoTracking().FirstOrDefaultAsync(ct);
        if (c is null) { return CalendarioConfigDto.Default; }
        return new CalendarioConfigDto(c.Lunes, c.Martes, c.Miercoles, c.Jueves, c.Viernes, c.Sabado, c.Domingo,
            NormalizarHora(c.JornadaInicio, "08:00"), NormalizarHora(c.JornadaFin, "17:00"));
    }

    public async Task<CalendarioGuardarResult> GuardarConfigAsync(CalendarioConfigDto config, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        if (!JornadaValida(config.JornadaInicio, config.JornadaFin)) { return CalendarioGuardarResult.JornadaInvalida; }

        var c = await _db.CalendariosHabiles.FirstOrDefaultAsync(ct);
        if (c is null)
        {
            c = new CalendarioHabilConfig { TenantId = tenantId };
            _db.CalendariosHabiles.Add(c);
        }
        c.Lunes = config.Lunes; c.Martes = config.Martes; c.Miercoles = config.Miercoles;
        c.Jueves = config.Jueves; c.Viernes = config.Viernes; c.Sabado = config.Sabado; c.Domingo = config.Domingo;
        c.JornadaInicio = NormalizarHora(config.JornadaInicio, "08:00");
        c.JornadaFin = NormalizarHora(config.JornadaFin, "17:00");
        c.Activo = true;
        await _db.SaveChangesAsync(ct);
        return CalendarioGuardarResult.Ok;
    }

    // ---- Festivos ----

    public async Task<IReadOnlyList<DiaFestivoDto>> ListarAsync(int anio, CancellationToken ct = default)
    {
        var desde = new DateOnly(anio, 1, 1);
        var hasta = new DateOnly(anio, 12, 31);
        return await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta).OrderBy(f => f.Fecha)
            .Select(f => new DiaFestivoDto(f.Id, f.Fecha, f.Nombre, f.EsNacional, f.Tipo)).ToListAsync(ct);
    }

    public async Task<int> SembrarAnioAsync(int anio, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var desde = new DateOnly(anio, 1, 1);
        var hasta = new DateOnly(anio, 12, 31);
        var existentes = (await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta).Select(f => f.Fecha).ToListAsync(ct)).ToHashSet();

        var creados = 0;
        foreach (var (fecha, nombre) in FestivosColombia.Calcular(anio))
        {
            if (existentes.Contains(fecha)) { continue; }
            _db.DiasFestivos.Add(new DiaFestivo { TenantId = tenantId, Fecha = fecha, Nombre = nombre, EsNacional = true, Tipo = "Nacional" });
            creados++;
        }
        if (creados > 0) { await _db.SaveChangesAsync(ct); }
        return creados;
    }

    public async Task<DiaFestivoDto?> AgregarAsync(DateOnly fecha, string nombre, string tipo = "Local", CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        if (string.IsNullOrWhiteSpace(nombre)) { return null; }
        if (await _db.DiasFestivos.AnyAsync(f => f.Fecha == fecha, ct)) { return null; }
        var t = tipo is "Local" or "Institucional" ? tipo : "Local";
        var d = new DiaFestivo { TenantId = tenantId, Fecha = fecha, Nombre = nombre.Trim(), EsNacional = false, Tipo = t };
        _db.DiasFestivos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new DiaFestivoDto(d.Id, d.Fecha, d.Nombre, d.EsNacional, d.Tipo);
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct = default)
    {
        var d = await _db.DiasFestivos.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (d is null) { return false; }
        _db.DiasFestivos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Helpers ----

    private static bool JornadaValida(string inicio, string fin)
        => TimeSpan.TryParse(inicio, out var hi) && TimeSpan.TryParse(fin, out var hf) && hi < hf;

    private static string NormalizarHora(string? valor, string valorDefault)
        => TimeSpan.TryParse(valor, out var ts) ? ts.ToString(@"hh\:mm") : valorDefault;
}
