using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del calendario habil. Un dia es habil si no es sabado/domingo y no es festivo del
/// tenant. Los festivos se cargan de la tabla dias_festivos (sembrada con FestivosColombia). Tenant-scoped
/// por el filtro global de EF.
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

    private static bool EsFinDeSemana(DateOnly f) => f.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public async Task<bool> EsHabilAsync(DateOnly fecha, CancellationToken ct = default)
    {
        if (EsFinDeSemana(fecha)) { return false; }
        var fest = await FestivosAsync(fecha.Year, fecha.Year, ct);
        return !fest.Contains(fecha);
    }

    public async Task<DateOnly> ProximoHabilAsync(DateOnly fecha, CancellationToken ct = default)
    {
        var fest = await FestivosAsync(fecha.Year, fecha.Year + 1, ct);
        var f = fecha;
        while (EsFinDeSemana(f) || fest.Contains(f)) { f = f.AddDays(1); }
        return f;
    }

    public async Task<DateOnly> SumarDiasHabilesAsync(DateOnly inicio, int dias, CancellationToken ct = default)
    {
        var fest = await FestivosAsync(inicio.Year, inicio.Year + 2, ct);
        var f = inicio;
        var restantes = dias;
        var guard = 0;
        while (restantes > 0 && guard++ < 3650)
        {
            f = f.AddDays(1);
            if (!EsFinDeSemana(f) && !fest.Contains(f)) { restantes--; }
        }
        return f;
    }

    public async Task<IReadOnlyList<DiaFestivoDto>> ListarAsync(int anio, CancellationToken ct = default)
    {
        var desde = new DateOnly(anio, 1, 1);
        var hasta = new DateOnly(anio, 12, 31);
        return await _db.DiasFestivos.AsNoTracking()
            .Where(f => f.Fecha >= desde && f.Fecha <= hasta).OrderBy(f => f.Fecha)
            .Select(f => new DiaFestivoDto(f.Id, f.Fecha, f.Nombre, f.EsNacional)).ToListAsync(ct);
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
            _db.DiasFestivos.Add(new DiaFestivo { TenantId = tenantId, Fecha = fecha, Nombre = nombre, EsNacional = true });
            creados++;
        }
        if (creados > 0) { await _db.SaveChangesAsync(ct); }
        return creados;
    }

    public async Task<DiaFestivoDto?> AgregarAsync(DateOnly fecha, string nombre, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        if (string.IsNullOrWhiteSpace(nombre)) { return null; }
        if (await _db.DiasFestivos.AnyAsync(f => f.Fecha == fecha, ct)) { return null; }
        var d = new DiaFestivo { TenantId = tenantId, Fecha = fecha, Nombre = nombre.Trim(), EsNacional = false };
        _db.DiasFestivos.Add(d);
        await _db.SaveChangesAsync(ct);
        return new DiaFestivoDto(d.Id, d.Fecha, d.Nombre, d.EsNacional);
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct = default)
    {
        var d = await _db.DiasFestivos.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (d is null) { return false; }
        _db.DiasFestivos.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
