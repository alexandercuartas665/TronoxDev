using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion de la bandeja de tramites (rad_tramites). Fiel al legacy en flujo y estados, corrigiendo
/// sus quirks: visibilidad fail-closed (mias + de mi dependencia sin asignar), FK a TenantUser (no texto),
/// una transaccion por accion (un SaveChanges), LINQ parametrizado, sin MAX(REG). Fechas en UTC.
/// </summary>
public sealed class RadicacionTramitesService : IRadicacionTramitesService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly RadicacionVisibilidadService _vis;

    public RadicacionTramitesService(IApplicationDbContext db, ITenantContext tenant, RadicacionVisibilidadService vis)
    {
        _db = db;
        _tenant = tenant;
        _vis = vis;
    }

    private static readonly RadicadoEstado[] CerradosSla = { RadicadoEstado.Respondido, RadicadoEstado.Archivado };

    private async Task<IQueryable<RadicadoTarea>> VisiblesAsync(CancellationToken ct)
    {
        var userId = _tenant.UserId;
        var deps = await _vis.DependenciasDelUsuarioAsync(userId, ct);
        // Mias (funcionario = yo) + de mi dependencia sin asignar (funcionario null). Fail-closed.
        return _db.RadicadosTareas.AsNoTracking()
            .Where(t => t.Activa && t.Radicado != null
                && t.Radicado.Estado != RadicadoEstado.Anulado && t.Radicado.Estado != RadicadoEstado.Borrador
                && (t.FuncionarioId == userId || (t.FuncionarioId == null && deps.Contains(t.DependenciaId))));
    }

    public async Task<TramitesResultDto> ListarAsync(TramitesFiltro f, CancellationToken ct = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var prox4 = hoy.AddDays(4);
        var baseQ = await VisiblesAsync(ct);

        // Contadores (sobre el base visible, sin tab/q/dep/prio).
        var asig = await baseQ.CountAsync(t => t.Estado == RadicadoTareaEstado.Asignada, ct);
        var acep = await baseQ.CountAsync(t => t.Estado == RadicadoTareaEstado.Aceptada, ct);
        var abiertas = baseQ.Where(t => (t.Estado == RadicadoTareaEstado.Asignada || t.Estado == RadicadoTareaEstado.Aceptada)
            && t.Radicado!.FechaVencimiento != null && !CerradosSla.Contains(t.Radicado.Estado));
        var prox = await abiertas.CountAsync(t => t.Radicado!.FechaVencimiento >= hoy && t.Radicado.FechaVencimiento < prox4, ct);
        var venc = await abiertas.CountAsync(t => t.Radicado!.FechaVencimiento < hoy, ct);
        var cnt = new TramitesContadores(asig, acep, prox, venc);

        // Filtro por tab.
        var q = f.Tab?.ToLowerInvariant() switch
        {
            "aceptadas" => baseQ.Where(t => t.Estado == RadicadoTareaEstado.Aceptada),
            "proximas" => abiertas.Where(t => t.Radicado!.FechaVencimiento >= hoy && t.Radicado.FechaVencimiento < prox4),
            "vencidas" => abiertas.Where(t => t.Radicado!.FechaVencimiento < hoy),
            _ => baseQ.Where(t => t.Estado == RadicadoTareaEstado.Asignada)
        };

        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var s = f.Q.Trim();
            q = q.Where(t => t.Radicado!.NumeroRadicado.Contains(s)
                || (t.Radicado.RemitenteNombre != null && t.Radicado.RemitenteNombre.Contains(s))
                || (t.Radicado.Asunto != null && t.Radicado.Asunto.Contains(s)));
        }
        if (f.DependenciaId is long dep) { q = q.Where(t => t.DependenciaId == dep); }
        if (!string.IsNullOrWhiteSpace(f.Prioridad) && Enum.TryParse<RadicadoPrioridad>(f.Prioridad, true, out var prio))
        {
            q = q.Where(t => t.Prioridad == prio);
        }

        var raw = await q
            .OrderBy(t => t.Radicado!.FechaVencimiento == null ? 1 : 0)
            .ThenBy(t => t.Radicado!.FechaVencimiento)
            .ThenByDescending(t => t.FechaAsignacion)
            .Take(150)
            .Select(t => new
            {
                Tarea = t.Id, Reg = t.RadicadoId, t.Radicado!.NumeroRadicado, t.Radicado.Anonimo, t.Radicado.RemitenteNombre,
                t.Radicado.Asunto, RadEstado = t.Radicado.Estado, t.Radicado.FechaVencimiento, t.Prioridad, TareaEstado = t.Estado,
                t.FechaAsignacion, t.Instrucciones,
                TipoNombre = t.Radicado.TipoComunicacion != null ? t.Radicado.TipoComunicacion.Nombre : null,
                TipoColor = t.Radicado.TipoComunicacion != null ? t.Radicado.TipoComunicacion.Color : null,
                DepNombre = t.Dependencia != null ? t.Dependencia.Name : null,
                Funcionario = _db.TenantUsers.Where(u => u.Id == t.FuncionarioId)
                    .Select(u => (u.Nombres + " " + u.Apellidos).Trim() != "" ? (u.Nombres + " " + u.Apellidos).Trim() : u.Email).FirstOrDefault()
            })
            .ToListAsync(ct);

        var lista = raw.Select(x => new TareaItemDto(
            x.Tarea, x.Reg, x.NumeroRadicado, x.TipoNombre, x.TipoColor,
            x.Anonimo ? "Anonimo" : (string.IsNullOrWhiteSpace(x.RemitenteNombre) ? "-" : x.RemitenteNombre!),
            x.Asunto, x.DepNombre, x.Funcionario, x.Prioridad.ToString(),
            x.FechaVencimiento is null ? null : (int)(x.FechaVencimiento.Value.Date - hoy).TotalDays,
            x.RadEstado.ToString(), x.FechaAsignacion.ToString("dd/MM/yyyy HH:mm"), x.TareaEstado.ToString(), x.Instrucciones)).ToList();

        return new TramitesResultDto(lista, cnt);
    }

    public async Task<TareaResult> AceptarAsync(long tareaId, CancellationToken ct = default)
    {
        var t = await _db.RadicadosTareas.FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (t is null) { return TareaResult.Fail("Tarea no encontrada."); }
        if (!t.Activa || t.Estado != RadicadoTareaEstado.Asignada) { return TareaResult.Fail("La tarea ya fue gestionada."); }
        var r = await _db.Radicados.FirstOrDefaultAsync(x => x.Id == t.RadicadoId, ct);
        if (r is null) { return TareaResult.Fail("Radicado no encontrado."); }

        var funcionario = t.FuncionarioId ?? _tenant.UserId; // si no tenia funcionario, la toma quien acepta
        var ahora = DateTime.UtcNow;
        t.Estado = RadicadoTareaEstado.Aceptada;
        t.FechaGestion = ahora;
        t.FuncionarioId = funcionario;
        r.Estado = RadicadoEstado.EnTramite;
        r.DependenciaDestinoId = t.DependenciaId;
        r.FuncionarioAsignadoId = funcionario;
        r.FechaDistribucion = ahora;
        AddTraza(r, "ACEPTAR", ahora, "Tarea aceptada - tramite en gestion.");
        await _db.SaveChangesAsync(ct);
        return TareaResult.Success();
    }

    public async Task<TareaResult> RechazarAsync(long tareaId, string observacion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(observacion)) { return TareaResult.Fail("La observacion del rechazo es obligatoria."); }
        var t = await _db.RadicadosTareas.FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (t is null) { return TareaResult.Fail("Tarea no encontrada."); }
        if (!t.Activa) { return TareaResult.Fail("La tarea ya fue gestionada."); }
        var r = await _db.Radicados.FirstOrDefaultAsync(x => x.Id == t.RadicadoId, ct);
        if (r is null) { return TareaResult.Fail("Radicado no encontrado."); }

        var ahora = DateTime.UtcNow;
        var teniaFuncionario = t.FuncionarioId is not null;
        t.Estado = RadicadoTareaEstado.Rechazada;
        t.Activa = false;
        t.FechaGestion = ahora;
        t.Observacion = observacion.Trim();
        AddTraza(r, "RECHAZAR", ahora, $"Tarea rechazada. Observacion: {Trunc(observacion, 350)}");

        var otrasActivas = await _db.RadicadosTareas.CountAsync(x => x.RadicadoId == r.Id && x.Activa && x.Id != t.Id, ct);
        if (otrasActivas == 0 && r.Estado is not (RadicadoEstado.Respondido or RadicadoEstado.Archivado or RadicadoEstado.Anulado))
        {
            if (teniaFuncionario)
            {
                // Vuelve a la dependencia sin funcionario para que el jefe reasigne.
                r.Estado = RadicadoEstado.Distribuido;
                r.FuncionarioAsignadoId = null;
                _db.RadicadosTareas.Add(new RadicadoTarea
                {
                    TenantId = t.TenantId, RadicadoId = r.Id, DependenciaId = t.DependenciaId, FuncionarioId = null,
                    Instrucciones = t.Instrucciones, Prioridad = t.Prioridad, Estado = RadicadoTareaEstado.Asignada,
                    Activa = true, Origen = t.Origen, DistribuidoPorId = t.DistribuidoPorId, FechaAsignacion = ahora
                });
            }
            else
            {
                r.Estado = RadicadoEstado.Radicado;
                r.FuncionarioAsignadoId = null;
            }
        }
        await _db.SaveChangesAsync(ct);
        return TareaResult.Success();
    }

    public async Task<TareaResult> AsignarAsync(long tareaId, long funcionarioId, CancellationToken ct = default)
    {
        if (funcionarioId <= 0) { return TareaResult.Fail("Seleccione el funcionario."); }
        var t = await _db.RadicadosTareas.FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (t is null) { return TareaResult.Fail("Tarea no encontrada."); }
        if (!t.Activa) { return TareaResult.Fail("La tarea ya fue gestionada."); }
        var r = await _db.Radicados.FirstOrDefaultAsync(x => x.Id == t.RadicadoId, ct);
        if (r is null) { return TareaResult.Fail("Radicado no encontrado."); }

        var ahora = DateTime.UtcNow;
        t.FuncionarioId = funcionarioId; // el ESTADO de la tarea sigue Asignada (fiel al legacy)
        r.Estado = RadicadoEstado.EnTramite;
        r.DependenciaDestinoId = t.DependenciaId;
        r.FuncionarioAsignadoId = funcionarioId;
        r.FechaDistribucion = ahora;
        AddTraza(r, "DISTRIBUIR", ahora, "Funcionario asignado por el jefe de dependencia - Distribuido -> En Tramite.");
        await _db.SaveChangesAsync(ct);
        return TareaResult.Success();
    }

    private void AddTraza(Radicado r, string accion, DateTime fecha, string detalle)
        => _db.RadicadosTrazabilidad.Add(new RadicadoTrazabilidad
        {
            TenantId = r.TenantId, RadicadoId = r.Id, Accion = accion, Fecha = fecha,
            UsuarioId = _tenant.UserId, Detalle = Trunc(detalle, 1000)
        });

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
}
