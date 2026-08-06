using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del Panel de Control (RQ09 RF12-1). Replica milimetrica de la operacion "dashboard"
/// del legacy rad_panel_op.ashx.vb (AccDashboard), adaptada a Tronox: LINQ parametrizado (no SQL
/// concatenado), filtro por tenant via el filtro global de EF (no SUCURSAL manual), y dependencias por
/// FK a OrgUnit. Quirks del legacy CONSERVADOS a proposito (decision del usuario): los KPIs son
/// "actuales/hoy" e ignoran el rango; la actividad son los ultimos 6 sin filtrar por rango.
/// </summary>
public sealed class RadicacionPanelService : IRadicacionPanelService
{
    private readonly IApplicationDbContext _db;

    public RadicacionPanelService(IApplicationDbContext db) => _db = db;

    // Estados "cerrados" (no abiertos): el resto cuenta como activo/en tramite. Fiel al legacy.
    private static readonly RadicadoEstado[] Cerrados =
    {
        RadicadoEstado.Respondido, RadicadoEstado.Archivado, RadicadoEstado.Anulado, RadicadoEstado.Borrador
    };

    public async Task<RadicacionDashboardDto> GetDashboardAsync(
        DateOnly? desde, DateOnly? hasta, CancellationToken cancellationToken = default)
    {
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);

        // Rango: default ultimos 30 dias (fiel al servidor legacy). Hasta inclusive -> < hasta+1dia.
        var d = desde?.ToDateTime(TimeOnly.MinValue) ?? hoy.AddDays(-29);
        var h = hasta?.ToDateTime(TimeOnly.MinValue) ?? hoy;
        if (h < d) { h = d; }
        var hExcl = h.AddDays(1);

        var rad = _db.Radicados.AsNoTracking();
        var enRango = rad.Where(r => r.FechaRadicacion >= d && r.FechaRadicacion < hExcl);

        // ================= KPIs (siempre "hoy"/actual; NO respetan el rango) =================
        var hoyQ = rad.Where(r => r.Estado != RadicadoEstado.Borrador
                                  && r.FechaRadicacion >= hoy && r.FechaRadicacion < manana);
        var hoyTot = await hoyQ.CountAsync(cancellationToken);
        var hoyE = await hoyQ.CountAsync(r => r.Tipo == RadicadoTipo.Entrada, cancellationToken);
        var hoyS = await hoyQ.CountAsync(r => r.Tipo == RadicadoTipo.Salida, cancellationToken);
        var hoyI = await hoyQ.CountAsync(r => r.Tipo == RadicadoTipo.Interno, cancellationToken);

        var sinDistribuir = await rad.CountAsync(
            r => r.Tipo == RadicadoTipo.Entrada && r.DependenciaDestinoId == null
                 && !Cerrados.Contains(r.Estado), cancellationToken);

        var abiertosConVenc = rad.Where(r => !Cerrados.Contains(r.Estado) && r.FechaVencimiento != null);
        var prox = await abiertosConVenc.CountAsync(
            r => r.FechaVencimiento >= hoy && r.FechaVencimiento < hoy.AddDays(4), cancellationToken);
        var venc = await abiertosConVenc.CountAsync(r => r.FechaVencimiento < hoy, cancellationToken);

        var tutelas = await rad.CountAsync(
            r => !Cerrados.Contains(r.Estado) && r.TipoComunicacion != null && r.TipoComunicacion.EsTutela,
            cancellationToken);

        // Correos pendientes por buzon (try/catch fiel: si algo falla -> 0, como el legacy).
        int correos = 0;
        var correosDetalle = "Pendientes en buzones";
        try
        {
            var pend = await _db.CorreosRecibidos.AsNoTracking()
                .Where(c => c.Estado == CorreoRevisionEstado.Pendiente)
                .GroupBy(c => c.BuzonEmail)
                .Select(g => new { Buzon = g.Key, N = g.Count() })
                .OrderByDescending(x => x.N)
                .ToListAsync(cancellationToken);
            correos = pend.Sum(x => x.N);
            if (pend.Count > 0)
            {
                correosDetalle = string.Join(" · ", pend.Take(2).Select(x =>
                {
                    var local = x.Buzon;
                    if (!string.IsNullOrEmpty(local) && local.Contains('@')) { local = local[..local.IndexOf('@')]; }
                    return $"{x.N} en {(string.IsNullOrEmpty(local) ? "—" : local)}";
                }));
            }
        }
        catch { correos = 0; }

        var kpi = new KpiRadicacionDto(hoyTot, hoyE, hoyS, hoyI, sinDistribuir, prox, venc, tutelas, correos, correosDetalle);

        // ================= Series (respetan el rango) =================
        var porDiaRaw = await enRango
            .GroupBy(r => r.FechaRadicacion.Date)
            .Select(g => new { K = g.Key, V = g.Count() })
            .OrderBy(x => x.K)
            .ToListAsync(cancellationToken);
        var porDia = porDiaRaw.Select(x => new SerieItemDto(x.K.ToString("yyyy-MM-dd"), x.V)).ToList();

        var porTipoRaw = await enRango
            .GroupBy(r => new { Nombre = r.TipoComunicacion!.Nombre, Color = r.TipoComunicacion!.Color })
            .Select(g => new { g.Key.Nombre, g.Key.Color, V = g.Count() })
            .OrderByDescending(x => x.V).Take(8)
            .ToListAsync(cancellationToken);
        var porTipo = porTipoRaw
            .Select(x => new SerieItemDto(x.Nombre ?? "(Sin tipo)", x.V, x.Color ?? "#405189")).ToList();

        var porCanalRaw = await enRango
            .GroupBy(r => r.Canal).Select(g => new { K = g.Key, V = g.Count() })
            .ToListAsync(cancellationToken);
        var porCanal = porCanalRaw.Select(x => new SerieItemDto(x.K.ToString(), x.V)).ToList();

        var porEstadoRaw = await enRango
            .GroupBy(r => r.Estado).Select(g => new { K = g.Key, V = g.Count() })
            .ToListAsync(cancellationToken);
        var porEstado = porEstadoRaw.Select(x => new SerieItemDto(x.K.ToString(), x.V)).ToList();

        var porDepRaw = await enRango
            .GroupBy(r => r.DependenciaDestino!.Name)
            .Select(g => new { K = g.Key, V = g.Count() })
            .OrderByDescending(x => x.V).Take(8)
            .ToListAsync(cancellationToken);
        var porDependencia = porDepRaw
            .Select(x => new SerieItemDto(x.K ?? "(Sin distribuir)", x.V)).ToList();

        // ---- SLA + tiempo promedio de respuesta: usa la primera traza "RESPONDIDO" por radicado ----
        var conVenc = await enRango.Where(r => r.FechaVencimiento != null)
            .Select(r => new { r.Id, r.FechaRadicacion, Venc = r.FechaVencimiento!.Value })
            .ToListAsync(cancellationToken);
        var respondidos = await _db.RadicadosTrazabilidad.AsNoTracking()
            .Where(t => t.Accion == "RESPONDIDO")
            .GroupBy(t => t.RadicadoId)
            .Select(g => new { RadicadoId = g.Key, Fr = g.Min(t => t.Fecha) })
            .ToListAsync(cancellationToken);
        var frPorRad = respondidos.ToDictionary(x => x.RadicadoId, x => x.Fr);

        int aTiempo = 0, vencidos = 0;
        var promedioAcc = new Dictionary<DateTime, List<double>>();
        foreach (var r in conVenc)
        {
            if (frPorRad.TryGetValue(r.Id, out var fr))
            {
                if (fr.Date <= r.Venc.Date) { aTiempo++; } else { vencidos++; }
                var dias = Math.Max(0, (fr.Date - r.FechaRadicacion.Date).TotalDays);
                if (!promedioAcc.TryGetValue(fr.Date, out var list)) { promedioAcc[fr.Date] = list = new(); }
                list.Add(dias);
            }
            else
            {
                if (r.Venc.Date >= hoy) { aTiempo++; } else { vencidos++; }
            }
        }
        var sla = new SlaDto(aTiempo, vencidos);
        var promedio = promedioAcc.OrderBy(kv => kv.Key)
            .Select(kv => new SerieItemDto(kv.Key.ToString("yyyy-MM-dd"), Math.Round(kv.Value.Average(), 1)))
            .ToList();

        // ================= Actividad reciente (ultimos 6, NO filtra rango) =================
        var actRaw = await rad.Where(r => r.Estado != RadicadoEstado.Borrador)
            .OrderByDescending(r => r.FechaRadicacion)
            .Take(6)
            .Select(r => new
            {
                r.Id, r.NumeroRadicado, r.FechaRadicacion,
                TipoNombre = r.TipoComunicacion != null ? r.TipoComunicacion.Nombre : null,
                TipoColor = r.TipoComunicacion != null ? r.TipoComunicacion.Color : null,
                r.Canal, r.Anonimo, r.RemitenteNombre, r.Estado, r.FechaVencimiento
            })
            .ToListAsync(cancellationToken);
        var actividad = actRaw.Select(r => new ActividadRadicadoDto(
            r.Id,
            r.NumeroRadicado,
            r.FechaRadicacion.ToString("dd/MM/yyyy HH:mm"),
            r.TipoNombre ?? "—",
            r.TipoColor,
            r.Canal.ToString(),
            r.Anonimo ? "Anonimo" : (string.IsNullOrWhiteSpace(r.RemitenteNombre) ? "—" : r.RemitenteNombre!),
            r.Estado.ToString(),
            r.FechaVencimiento is null ? null : (int)(r.FechaVencimiento.Value.Date - hoy).TotalDays))
            .ToList();

        return new RadicacionDashboardDto(
            kpi, porDia, porTipo, porCanal, porEstado, porDependencia, promedio, sla, actividad,
            d.ToString("yyyy-MM-dd"), h.ToString("yyyy-MM-dd"));
    }
}
