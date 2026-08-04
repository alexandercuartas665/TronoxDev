using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Validaciones;

/// <summary>
/// Tareas de validacion (RQ04 - RF11/RF12). El aislamiento por tenant lo da el filtro global. La
/// validacion NUNCA toca <see cref="Documento.Estado"/>: es traza paralela. Solo el asignado responde;
/// solo el creador pudo solicitarla sobre un documento que ve.
/// </summary>
public sealed class ValidacionService : IValidacionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public ValidacionService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Solicitar (RF11) ----

    public async Task<ValidacionResult<long>> SolicitarAsync(
        SolicitarValidacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.AsNoTracking()
            .Include(d => d.NivelClasificacion)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentoId, cancellationToken);
        if (doc is null || !await PuedeVerDocumentoAsync(doc, actorUserId, cancellationToken))
        {
            return ValidacionResult<long>.NotFound("El documento no existe.");
        }

        var asignado = await _db.TenantUsers.AsNoTracking()
            .Where(u => u.Id == request.UsuarioAsignadoId)
            .Select(u => new { u.Id, u.Nombres, u.Apellidos, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (asignado is null) { return ValidacionResult<long>.NotFound("El usuario asignado no existe."); }

        var nombreAsignado = string.IsNullOrWhiteSpace(asignado.Nombres) && string.IsNullOrWhiteSpace(asignado.Apellidos)
            ? asignado.Email
            : $"{asignado.Nombres} {asignado.Apellidos}".Trim();

        var tenantId = _tenantContext.TenantId!.Value;
        var tarea = new DocumentoValidacion
        {
            TenantId = tenantId,
            DocumentoId = doc.Id,
            Tipo = request.Tipo,
            UsuarioAsignadoId = asignado.Id,
            NombreAsignado = nombreAsignado,
            Estado = EstadoValidacion.Pendiente,
            Prioridad = request.Prioridad,
            FechaLimite = request.FechaLimite,
            Instrucciones = string.IsNullOrWhiteSpace(request.Instrucciones) ? null : request.Instrucciones.Trim()
        };
        _db.DocumentoValidaciones.Add(tarea);
        _audit.Write(actorUserId, $"documento.solicitar_{request.Tipo}".ToLowerInvariant(), nameof(DocumentoValidacion), tarea,
            previousValue: null, newValue: new { tarea.DocumentoId, tarea.Tipo, tarea.UsuarioAsignadoId }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ValidacionResult<long>.Ok(tarea.Id);
    }

    // ---- Bandejas (RF12) ----

    public async Task<IReadOnlyList<TareaItemDto>> ListarPendientesAsync(
        long actorUserId, TipoValidacion? tipo = null, CancellationToken cancellationToken = default)
    {
        var query = _db.DocumentoValidaciones.AsNoTracking()
            .Include(v => v.Documento!).ThenInclude(d => d.Expediente)
            .Where(v => v.UsuarioAsignadoId == actorUserId && v.Estado == EstadoValidacion.Pendiente);
        if (tipo is TipoValidacion tp) { query = query.Where(v => v.Tipo == tp); }

        var rows = await query.OrderBy(v => v.Prioridad == PrioridadTarea.Urgente ? 0 : 1).ThenBy(v => v.FechaLimite)
            .ToListAsync(cancellationToken);
        var solicitantes = await ResolverNombresAsync(rows.Select(v => v.CreatedBy), cancellationToken);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        return rows.Select(v => new TareaItemDto(
            v.Id, v.DocumentoId, v.Documento?.Nombre ?? "",
            v.Documento?.Expediente?.Codigo,
            v.Tipo, v.Prioridad, v.Estado, v.FechaLimite,
            ValidacionRules.DiasRestantes(v.FechaLimite, hoy),
            v.CreatedBy is long cb && solicitantes.TryGetValue(cb, out var n) ? n : null,
            v.CreatedAt, v.Instrucciones)).ToList();
    }

    public async Task<IReadOnlyList<TareaHistorialDto>> ListarHistorialAsync(long actorUserId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.DocumentoValidaciones.AsNoTracking()
            .Include(v => v.Documento!).ThenInclude(d => d.Expediente)
            .Where(v => v.UsuarioAsignadoId == actorUserId && v.Estado != EstadoValidacion.Pendiente)
            .OrderByDescending(v => v.FechaRespuesta)
            .ToListAsync(cancellationToken);
        var solicitantes = await ResolverNombresAsync(rows.Select(v => v.CreatedBy), cancellationToken);

        return rows.Select(v => new TareaHistorialDto(
            v.Id, v.Documento?.Nombre ?? "", v.Documento?.Expediente?.Codigo,
            v.Tipo, v.CreatedBy is long cb && solicitantes.TryGetValue(cb, out var n) ? n : null,
            v.Estado, v.Comentarios, v.FechaRespuesta)).ToList();
    }

    public async Task<TareaContadoresDto> GetContadoresAsync(long actorUserId, CancellationToken cancellationToken = default)
    {
        var porTipo = await _db.DocumentoValidaciones.AsNoTracking()
            .Where(v => v.UsuarioAsignadoId == actorUserId && v.Estado == EstadoValidacion.Pendiente)
            .GroupBy(v => v.Tipo)
            .Select(g => new { Tipo = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var rev = porTipo.FirstOrDefault(x => x.Tipo == TipoValidacion.Revision)?.Count ?? 0;
        var apr = porTipo.FirstOrDefault(x => x.Tipo == TipoValidacion.Aprobacion)?.Count ?? 0;
        return new TareaContadoresDto(rev + apr, rev, apr);
    }

    public async Task<ValidacionResult<TareaItemDto>> GetDetalleAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var v = await _db.DocumentoValidaciones.AsNoTracking()
            .Include(x => x.Documento!).ThenInclude(d => d.Expediente)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (v is null || (v.UsuarioAsignadoId != actorUserId && v.CreatedBy != actorUserId))
        {
            return ValidacionResult<TareaItemDto>.NotFound("La tarea no existe.");
        }
        var solicitantes = await ResolverNombresAsync([v.CreatedBy], cancellationToken);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return ValidacionResult<TareaItemDto>.Ok(new TareaItemDto(
            v.Id, v.DocumentoId, v.Documento?.Nombre ?? "", v.Documento?.Expediente?.Codigo,
            v.Tipo, v.Prioridad, v.Estado, v.FechaLimite, ValidacionRules.DiasRestantes(v.FechaLimite, hoy),
            v.CreatedBy is long cb && solicitantes.TryGetValue(cb, out var n) ? n : null, v.CreatedAt, v.Instrucciones));
    }

    // ---- Responder (RF12) ----

    public async Task<ValidacionResult<bool>> ResponderAsync(
        long id, EstadoValidacion nuevoEstado, string? comentario, long actorUserId, CancellationToken cancellationToken = default)
    {
        var err = ValidacionRules.ValidateRespuesta(nuevoEstado, comentario);
        if (err is not null) { return ValidacionResult<bool>.Invalid(err); }

        var v = await _db.DocumentoValidaciones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (v is null) { return ValidacionResult<bool>.NotFound("La tarea no existe."); }
        if (v.UsuarioAsignadoId != actorUserId) { return ValidacionResult<bool>.Forbidden("Esta tarea no te esta asignada."); }
        if (v.Estado != EstadoValidacion.Pendiente) { return ValidacionResult<bool>.Conflict("La tarea ya fue respondida."); }

        v.Estado = nuevoEstado;
        v.Comentarios = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        v.FechaRespuesta = DateTime.UtcNow;
        // La validacion NO toca Documento.Estado (RF11 CA-1): es traza paralela.
        _audit.Write(actorUserId, $"documento.responder_{v.Tipo}".ToLowerInvariant(), nameof(DocumentoValidacion), v,
            previousValue: new { Estado = "Pendiente" }, newValue: new { Estado = nuevoEstado.ToString(), Comentario = v.Comentarios },
            tenantId: v.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ValidacionResult<bool>.Ok(true);
    }

    // ---- Apoyo formulario de solicitud ----

    public async Task<IReadOnlyList<UsuarioAsignableDto>> GetUsuariosAsignablesAsync(long actorUserId, CancellationToken cancellationToken = default)
        => await _db.TenantUsers.AsNoTracking()
            .Where(u => u.Id != actorUserId)
            .OrderBy(u => u.Nombres).ThenBy(u => u.Apellidos)
            .Select(u => new UsuarioAsignableDto(u.Id,
                string.IsNullOrWhiteSpace(u.Nombres) && string.IsNullOrWhiteSpace(u.Apellidos)
                    ? u.Email : (u.Nombres + " " + u.Apellidos).Trim()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentoSolicitarDto>> GetDocumentosParaSolicitarAsync(
        long actorUserId, string? texto = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Documentos.AsNoTracking()
            .Where(d => d.CreatedBy == actorUserId && d.Estado != EstadoDocumento.Anulado);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            query = query.Where(d => d.Nombre.ToLower().Contains(t));
        }
        return await query.OrderByDescending(d => d.CreatedAt).Take(50)
            .Select(d => new DocumentoSolicitarDto(d.Id, d.Nombre, d.Estado))
            .ToListAsync(cancellationToken);
    }

    // ---- Helpers ----

    private async Task<bool> PuedeVerDocumentoAsync(Documento doc, long actorUserId, CancellationToken cancellationToken)
    {
        if (doc.Estado == EstadoDocumento.Borrador) { return doc.CreatedBy == actorUserId; }
        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        return (doc.NivelClasificacion?.NivelOrden ?? 0) <= nivelMax;
    }

    private async Task<int> ResolveNivelMaxOrdenAsync(long actorUserId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var ordenes = await _db.UsuariosRoles.AsNoTracking()
            .Where(ur => ur.TenantUserId == actorUserId
                         && (ur.VigenteDesde == null || ur.VigenteDesde <= now)
                         && (ur.VigenteHasta == null || ur.VigenteHasta > now))
            .Select(ur => (int?)ur.Rol!.NivelAccesoMaximo!.NivelOrden)
            .ToListAsync(cancellationToken);
        return ordenes.Count == 0 ? 0 : ordenes.Max(o => o ?? 0);
    }

    private async Task<Dictionary<long, string>> ResolverNombresAsync(IEnumerable<long?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) { return []; }
        var users = await _db.TenantUsers.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombres, u.Apellidos, u.Email })
            .ToListAsync(cancellationToken);
        return users.ToDictionary(u => u.Id,
            u => string.IsNullOrWhiteSpace(u.Nombres) && string.IsNullOrWhiteSpace(u.Apellidos)
                ? u.Email : $"{u.Nombres} {u.Apellidos}".Trim());
    }
}
