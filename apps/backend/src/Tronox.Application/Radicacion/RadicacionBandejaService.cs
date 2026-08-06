using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion de la bandeja (rad_bandeja). LINQ parametrizado + filtro de visibilidad fail-closed;
/// nunca SQL concatenado. Fechas en UTC (columnas timestamptz). Los quirks del legacy (paginacion falsa
/// por TOP, seleccion masiva decorativa) se conservan solo donde son fieles al comportamiento visible.
/// </summary>
public sealed class RadicacionBandejaService : IRadicacionBandejaService
{
    private readonly IApplicationDbContext _db;
    private readonly RadicacionVisibilidadService _vis;

    public RadicacionBandejaService(IApplicationDbContext db, RadicacionVisibilidadService vis)
    {
        _db = db;
        _vis = vis;
    }

    private static readonly RadicadoEstado[] Cerrados =
    {
        RadicadoEstado.Respondido, RadicadoEstado.Archivado, RadicadoEstado.Anulado, RadicadoEstado.Borrador
    };

    public async Task<BandejaResultDto> ListarAsync(BandejaFiltro f, CancellationToken ct = default)
    {
        var q = await BaseAsync(f, ct);
        var top = f.Top <= 0 ? 5000 : f.Top;

        var items = await q
            .OrderByDescending(r => r.FechaRadicacion)
            .Take(top)
            .Select(r => new
            {
                r.Id, r.NumeroRadicado, r.Tipo, r.FechaRadicacion, r.Canal, r.Anonimo, r.RemitenteNombre,
                r.Asunto, r.Estado, r.FechaVencimiento,
                TipoNombre = r.TipoComunicacion != null ? r.TipoComunicacion.Nombre : null,
                TipoColor = r.TipoComunicacion != null ? r.TipoComunicacion.Color : null,
                EsPqrsd = r.TipoComunicacion != null && r.TipoComunicacion.EsPqrsd,
                EsTutela = r.TipoComunicacion != null && r.TipoComunicacion.EsTutela,
                DepNombre = r.DependenciaDestino != null ? r.DependenciaDestino.Name : null,
                FuncNombre = _db.TenantUsers.Where(u => u.Id == r.FuncionarioAsignadoId)
                    .Select(u => (u.Nombres + " " + u.Apellidos).Trim() != "" ? (u.Nombres + " " + u.Apellidos).Trim() : u.Email)
                    .FirstOrDefault(),
                RespondeA = r.RadicadoRelacionado != null ? r.RadicadoRelacionado.NumeroRadicado : null,
                NumSalidas = _db.Radicados.Count(s => s.RadicadoRelacionadoId == r.Id && s.Tipo == RadicadoTipo.Salida)
            })
            .ToListAsync(ct);

        var hoy = DateTime.UtcNow.Date;
        var rows = items.Select(x => new BandejaItemDto(
            x.Id, x.NumeroRadicado, x.Tipo, x.FechaRadicacion.ToString("dd/MM/yyyy HH:mm"),
            x.TipoNombre, x.TipoColor, x.Canal.ToString(),
            x.Anonimo ? "Anonimo" : (string.IsNullOrWhiteSpace(x.RemitenteNombre) ? "-" : x.RemitenteNombre!),
            x.Asunto,
            string.IsNullOrEmpty(x.DepNombre) ? "Sin asignar" : x.DepNombre!,
            string.IsNullOrWhiteSpace(x.FuncNombre) ? null : x.FuncNombre,
            x.Estado.ToString(),
            x.FechaVencimiento is null ? null : (int)(x.FechaVencimiento.Value.Date - hoy).TotalDays,
            x.EsPqrsd, x.EsTutela, x.RespondeA, x.NumSalidas)).ToList();

        var total = await q.CountAsync(ct);
        return new BandejaResultDto(rows, total);
    }

    public async Task<BandejaContadoresDto> ContadoresAsync(BandejaFiltro f, CancellationToken ct = default)
    {
        // Contadores: mismos filtros (sin tab) + la visibilidad. Cada tab es su propio predicado.
        var baseQ = (await BaseSinTabAsync(f, ct));
        var hoy = DateTime.UtcNow.Date;
        var prox4 = hoy.AddDays(4);

        var todos = await baseQ.CountAsync(ct);
        var pqrsd = await baseQ.CountAsync(r => r.TipoComunicacion != null && r.TipoComunicacion.EsPqrsd, ct);
        var tutelas = await baseQ.CountAsync(r => r.TipoComunicacion != null && r.TipoComunicacion.EsTutela, ct);
        var sindist = await baseQ.CountAsync(r => r.FuncionarioAsignadoId == null && r.Estado == RadicadoEstado.Radicado, ct);
        var abiertos = baseQ.Where(r => !Cerrados.Contains(r.Estado) && r.FechaVencimiento != null);
        var prox = await abiertos.CountAsync(r => r.FechaVencimiento >= hoy && r.FechaVencimiento < prox4, ct);
        var venc = await abiertos.CountAsync(r => r.FechaVencimiento < hoy, ct);
        return new BandejaContadoresDto(todos, pqrsd, tutelas, sindist, prox, venc);
    }

    public async Task<IReadOnlyList<OpcionDto>> TiposAsync(CancellationToken ct = default)
        => await _db.TiposComunicacion.AsNoTracking().Where(t => t.Activo)
            .OrderBy(t => t.Nombre).Select(t => new OpcionDto(t.Id, t.Nombre)).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> DependenciasAsync(CancellationToken ct = default)
        => await _db.OrgUnits.AsNoTracking()
            .Where(o => o.Classifier == OrgUnitClassifier.Dependencia && !o.IsArchived)
            .OrderBy(o => o.Name).Select(o => new OpcionDto(o.Id, o.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<OpcionDto>> FuncionariosAsync(long? dependenciaId, CancellationToken ct = default)
        => await _db.TenantUsers.AsNoTracking()
            .Where(u => u.Status == Domain.Enums.PlatformUserStatus.Active)
            .OrderBy(u => u.Nombres).Select(u => new OpcionDto(u.Id,
                (u.Nombres + " " + u.Apellidos).Trim() != "" ? (u.Nombres + " " + u.Apellidos).Trim() : u.Email))
            .ToListAsync(ct);

    // ---- Query base: visibilidad + filtros + tab. ----
    private async Task<IQueryable<Radicado>> BaseAsync(BandejaFiltro f, CancellationToken ct)
        => AplicarTab(await BaseSinTabAsync(f, ct), f.Tab);

    private async Task<IQueryable<Radicado>> BaseSinTabAsync(BandejaFiltro f, CancellationToken ct)
    {
        var vis = await _vis.FiltroActualAsync(ct);
        var q = _db.Radicados.AsNoTracking().Where(vis);

        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim();
            q = q.Where(r => r.NumeroRadicado.Contains(b)
                || (r.RemitenteNombre != null && r.RemitenteNombre.Contains(b))
                || (r.Asunto != null && r.Asunto.Contains(b)));
        }
        if (f.Desde is DateOnly d) { var dt = DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc); q = q.Where(r => r.FechaRadicacion >= dt); }
        if (f.Hasta is DateOnly h) { var ht = DateTime.SpecifyKind(h.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(1); q = q.Where(r => r.FechaRadicacion < ht); }
        if (f.Direccion is RadicadoTipo dir) { q = q.Where(r => r.Tipo == dir); }
        if (f.TipoComunicacionId is long tid) { q = q.Where(r => r.TipoComunicacionId == tid); }
        if (f.Estado is RadicadoEstado est) { q = q.Where(r => r.Estado == est); }
        if (f.Canal is RadicadoCanal can) { q = q.Where(r => r.Canal == can); }
        if (f.DependenciaId is long dep) { q = q.Where(r => r.DependenciaDestinoId == dep); }

        var hoy = DateTime.UtcNow.Date;
        if (!string.IsNullOrEmpty(f.Sla))
        {
            q = f.Sla.ToUpperInvariant() switch
            {
                "VENCIDO" => q.Where(r => r.FechaVencimiento != null && r.FechaVencimiento < hoy),
                "PROXIMO" => q.Where(r => r.FechaVencimiento != null && r.FechaVencimiento >= hoy && r.FechaVencimiento < hoy.AddDays(4)),
                "VIGENTE" => q.Where(r => r.FechaVencimiento == null || r.FechaVencimiento >= hoy.AddDays(4)),
                _ => q
            };
        }
        if (!string.IsNullOrWhiteSpace(f.Funcionario))
        {
            var fn = f.Funcionario.Trim();
            q = q.Where(r => _db.TenantUsers.Any(u => u.Id == r.FuncionarioAsignadoId
                && (u.Nombres + " " + u.Apellidos + " " + u.Email).Contains(fn)));
        }
        return q;
    }

    private IQueryable<Radicado> AplicarTab(IQueryable<Radicado> q, string? tab)
    {
        var hoy = DateTime.UtcNow.Date;
        return (tab ?? "todos").ToLowerInvariant() switch
        {
            "pqrsd" => q.Where(r => r.TipoComunicacion != null && r.TipoComunicacion.EsPqrsd),
            "tutelas" => q.Where(r => r.TipoComunicacion != null && r.TipoComunicacion.EsTutela),
            "sindist" => q.Where(r => r.FuncionarioAsignadoId == null && r.Estado == RadicadoEstado.Radicado),
            "prox" => q.Where(r => !Cerrados.Contains(r.Estado) && r.FechaVencimiento != null
                && r.FechaVencimiento >= hoy && r.FechaVencimiento < hoy.AddDays(4)),
            "venc" => q.Where(r => !Cerrados.Contains(r.Estado) && r.FechaVencimiento != null && r.FechaVencimiento < hoy),
            _ => q
        };
    }
}
