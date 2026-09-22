using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion de la distribucion (rad_tramites). Fiel al flujo legacy pero corrigiendo sus quirks:
/// una sola transaccion (un SaveChanges), FK a OrgUnit/TenantUser (no texto), sin SQL concatenado, sin
/// fuga de excepciones (resultado tipado). La notificacion (campana/email) se deja como punto de
/// extension best-effort para cuando se integre el bus de notificaciones (RQ04).
/// </summary>
public sealed class RadicacionDistribucionService : IRadicacionDistribucionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public RadicacionDistribucionService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private static readonly RadicadoEstado[] NoDistribuibles =
    {
        RadicadoEstado.Respondido, RadicadoEstado.Archivado, RadicadoEstado.Anulado, RadicadoEstado.Borrador
    };

    public async Task<DistribuirResult> DistribuirAsync(DistribuirRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return DistribuirResult.Fail("Sesion no valida."); }
        if (req.DependenciaId <= 0) { return DistribuirResult.Fail("La dependencia destino es obligatoria."); }

        var radicado = await _db.Radicados.FirstOrDefaultAsync(r => r.Id == req.RadicadoId, ct);
        if (radicado is null) { return DistribuirResult.Fail("Radicado no encontrado."); }
        if (NoDistribuibles.Contains(radicado.Estado))
        {
            return DistribuirResult.Fail($"El radicado esta en estado {radicado.Estado} y no admite distribucion.");
        }

        var dep = await _db.OrgUnits.FirstOrDefaultAsync(
            o => o.Id == req.DependenciaId && o.Classifier == OrgUnitClassifier.Dependencia, ct);
        if (dep is null) { return DistribuirResult.Fail("La dependencia destino no es valida."); }

        var ahora = DateTime.UtcNow;
        var userId = _tenant.UserId;

        // Reasignacion: si ya hay tareas activas, exige justificacion y las cierra.
        var activas = await _db.RadicadosTareas.Where(t => t.RadicadoId == req.RadicadoId && t.Activa).ToListAsync(ct);
        if (activas.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(req.Justificacion))
            {
                return DistribuirResult.Fail("La reasignacion requiere justificacion obligatoria (RF07-2).");
            }
            foreach (var t in activas)
            {
                t.Estado = RadicadoTareaEstado.Reasignada;
                t.Activa = false;
                t.FechaGestion = ahora;
                t.Observacion = req.Justificacion;
            }
            AddTraza(tenantId.Value, req.RadicadoId, "REASIGNAR", userId, ahora,
                $"Reasignacion de la distribucion. Justificacion: {Trunc(req.Justificacion, 350)}");
        }

        string? funcNombre = null;
        if (req.FuncionarioId is long fid)
        {
            funcNombre = await _db.TenantUsers.Where(u => u.Id == fid)
                .Select(u => (u.Nombres + " " + u.Apellidos).Trim() != "" ? (u.Nombres + " " + u.Apellidos).Trim() : u.Email)
                .FirstOrDefaultAsync(ct);
        }

        // Crear la tarea nueva.
        _db.RadicadosTareas.Add(new RadicadoTarea
        {
            TenantId = tenantId.Value,
            RadicadoId = req.RadicadoId,
            DependenciaId = req.DependenciaId,
            FuncionarioId = req.FuncionarioId,
            Instrucciones = string.IsNullOrWhiteSpace(req.Instrucciones) ? null : req.Instrucciones,
            Prioridad = req.Prioridad,
            Estado = RadicadoTareaEstado.Asignada,
            Activa = true,
            Origen = "Distribucion",
            DistribuidoPorId = userId,
            FechaAsignacion = ahora
        });

        var detalleTraza = req.FuncionarioId is null
            ? $"Tarea generada para {dep.Name} - pendiente de asignar funcionario (jefe de dependencia). Prioridad {req.Prioridad}."
            : $"Tarea generada para {dep.Name} - funcionario {funcNombre}. Prioridad {req.Prioridad}.";
        AddTraza(tenantId.Value, req.RadicadoId, "DISTRIBUIR", userId, ahora, detalleTraza);

        // Actualizar el estado del radicado.
        var nuevoEstado = req.FuncionarioId is not null ? RadicadoEstado.EnTramite : RadicadoEstado.Distribuido;
        radicado.Estado = nuevoEstado;
        radicado.DependenciaDestinoId = req.DependenciaId;
        radicado.FuncionarioAsignadoId = req.FuncionarioId;
        radicado.FechaDistribucion = ahora;
        radicado.Prioridad = req.Prioridad;

        await _db.SaveChangesAsync(ct); // atomico: tareas + trazas + estado en una transaccion.

        // TODO (RQ04): encolar notificacion campana + email al funcionario (best-effort, fuera de la tx).
        return DistribuirResult.Success(nuevoEstado);
    }

    public async Task<DistribuirResult> DistribuirCircularAsync(DistribuirCircularRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return DistribuirResult.Fail("Sesion no valida."); }
        if (req.Destinos is not { Count: > 0 }) { return DistribuirResult.Fail("Agregue al menos una dependencia destinataria."); }

        var radicado = await _db.Radicados.FirstOrDefaultAsync(r => r.Id == req.RadicadoId, ct);
        if (radicado is null) { return DistribuirResult.Fail("Radicado no encontrado."); }
        if (NoDistribuibles.Contains(radicado.Estado))
        {
            return DistribuirResult.Fail($"El radicado esta en estado {radicado.Estado} y no admite distribucion.");
        }

        var ahora = DateTime.UtcNow;
        var userId = _tenant.UserId;

        // Deduplica por dependencia (fiel al legacy: una sola entrada por dependencia) conservando el orden.
        var vistos = new HashSet<long>();
        var destinos = req.Destinos.Where(d => d.DependenciaId > 0 && vistos.Add(d.DependenciaId)).ToList();
        if (destinos.Count == 0) { return DistribuirResult.Fail("Agregue al menos una dependencia destinataria."); }

        // Valida todas las dependencias antes de escribir nada.
        var depIds = destinos.Select(d => d.DependenciaId).ToList();
        var deps = await _db.OrgUnits
            .Where(o => depIds.Contains(o.Id) && o.Classifier == OrgUnitClassifier.Dependencia)
            .ToDictionaryAsync(o => o.Id, o => o.Name, ct);
        if (deps.Count != destinos.Count) { return DistribuirResult.Fail("Una o mas dependencias destino no son validas."); }

        foreach (var d in destinos)
        {
            _db.RadicadosTareas.Add(new RadicadoTarea
            {
                TenantId = tenantId.Value,
                RadicadoId = req.RadicadoId,
                DependenciaId = d.DependenciaId,
                FuncionarioId = d.FuncionarioId,
                Instrucciones = string.IsNullOrWhiteSpace(req.Instrucciones) ? null : req.Instrucciones,
                Prioridad = req.Prioridad,
                Estado = RadicadoTareaEstado.Asignada,
                Activa = true,
                Origen = "Interna",
                DistribuidoPorId = userId,
                FechaAsignacion = ahora
            });
        }

        // Cabecera: primer destino. El funcionario de cabecera solo con un unico destino (legacy).
        var primero = destinos[0];
        var unico = destinos.Count == 1;
        var funcCabecera = unico ? primero.FuncionarioId : null;
        radicado.DependenciaDestinoId = primero.DependenciaId;
        radicado.FuncionarioAsignadoId = funcCabecera;
        radicado.FechaDistribucion = ahora;
        radicado.Prioridad = req.Prioridad;
        var nuevoEstado = funcCabecera is not null ? RadicadoEstado.EnTramite : RadicadoEstado.Distribuido;
        radicado.Estado = nuevoEstado;

        var detalle = destinos.Count > 1
            ? $"Circular interna: {destinos.Count} tareas generadas ({string.Join(", ", destinos.Select(x => deps[x.DependenciaId]))})."
            : $"Tarea generada para {deps[primero.DependenciaId]}. Prioridad {req.Prioridad}.";
        AddTraza(tenantId.Value, req.RadicadoId, "DISTRIBUIR", userId, ahora, detalle);

        await _db.SaveChangesAsync(ct); // atomico: N tareas + traza + estado en una transaccion.
        return DistribuirResult.Success(nuevoEstado);
    }

    private void AddTraza(long tenantId, long radicadoId, string accion, long? usuarioId, DateTime fecha, string detalle)
        => _db.RadicadosTrazabilidad.Add(new RadicadoTrazabilidad
        {
            TenantId = tenantId,
            RadicadoId = radicadoId,
            Accion = accion,
            Fecha = fecha,
            UsuarioId = usuarioId,
            Detalle = Trunc(detalle, 1000)
        });

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
}
