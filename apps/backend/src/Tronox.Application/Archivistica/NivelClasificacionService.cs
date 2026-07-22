using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Administracion de los niveles de clasificacion documental del tenant (RQ01 - RF01-P.3).
/// Aislamiento por tenant via filtro global. Toda mutacion queda en la pista de auditoria,
/// auditando la ENTIDAD (no el id: en un alta el id todavia vale 0).
/// </summary>
public sealed class NivelClasificacionService : INivelClasificacionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;

    public NivelClasificacionService(IApplicationDbContext db, ITenantContext tenant, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<NivelClasificacionDto>> ListAsync(
        bool soloActivos = false, CancellationToken cancellationToken = default)
    {
        var query = _db.NivelesClasificacion.AsNoTracking();
        if (soloActivos)
        {
            query = query.Where(n => n.Activo);
        }
        // Proyeccion inline (no Map): EF debe poder traducir el Select a SQL.
        return await query
            .OrderBy(n => n.NivelOrden)
            .Select(n => new NivelClasificacionDto(
                n.Id, n.Nombre, n.Codigo, n.Descripcion, n.ColorEtiqueta, n.NivelOrden, n.Activo))
            .ToListAsync(cancellationToken);
    }

    public async Task<NivelClasificacionDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var n = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return n is null ? null : Map(n);
    }

    public async Task<ArchivisticaResult<NivelClasificacionDto>> SaveAsync(
        SaveNivelClasificacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var error = ArchivisticaRules.ValidateNivelClasificacion(
            request.Nombre, request.Codigo, request.ColorEtiqueta, request.NivelOrden, request.Descripcion);
        if (error is not null)
        {
            return ArchivisticaResult<NivelClasificacionDto>.Invalid(error);
        }

        var nombre = request.Nombre.Trim();
        var codigo = request.Codigo.Trim();
        var descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        var color = string.IsNullOrWhiteSpace(request.ColorEtiqueta) ? null : request.ColorEtiqueta.Trim().ToUpperInvariant();

        NivelClasificacion entity;
        if (request.Id is long id)
        {
            var found = await _db.NivelesClasificacion.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (found is null)
            {
                return ArchivisticaResult<NivelClasificacionDto>.NotFound("El nivel de clasificacion no existe.");
            }
            if (await _db.NivelesClasificacion.AnyAsync(x => x.Codigo == codigo && x.Id != id, cancellationToken))
            {
                return ArchivisticaResult<NivelClasificacionDto>.Conflict($"Ya existe un nivel con el codigo '{codigo}'.");
            }
            if (await _db.NivelesClasificacion.AnyAsync(x => x.NivelOrden == request.NivelOrden && x.Id != id, cancellationToken))
            {
                return ArchivisticaResult<NivelClasificacionDto>.Conflict(
                    $"Ya existe un nivel en el orden {request.NivelOrden}.");
            }

            entity = found;
            var prev = new { entity.Nombre, entity.Codigo, entity.ColorEtiqueta, entity.NivelOrden, entity.Activo };
            entity.Nombre = nombre;
            entity.Codigo = codigo;
            entity.Descripcion = descripcion;
            entity.ColorEtiqueta = color;
            entity.NivelOrden = request.NivelOrden;
            entity.Activo = request.Activo;
            _audit.Write(actorUserId, "nivel-clasificacion.update", nameof(NivelClasificacion), entity,
                previousValue: prev,
                newValue: new { entity.Nombre, entity.Codigo, entity.ColorEtiqueta, entity.NivelOrden, entity.Activo },
                tenantId: entity.TenantId);
        }
        else
        {
            if (_tenant.TenantId is not long tenantId)
            {
                return ArchivisticaResult<NivelClasificacionDto>.Invalid("No hay tenant activo.");
            }
            if (await _db.NivelesClasificacion.AnyAsync(x => x.Codigo == codigo, cancellationToken))
            {
                return ArchivisticaResult<NivelClasificacionDto>.Conflict($"Ya existe un nivel con el codigo '{codigo}'.");
            }
            if (await _db.NivelesClasificacion.AnyAsync(x => x.NivelOrden == request.NivelOrden, cancellationToken))
            {
                return ArchivisticaResult<NivelClasificacionDto>.Conflict(
                    $"Ya existe un nivel en el orden {request.NivelOrden}.");
            }

            entity = new NivelClasificacion
            {
                TenantId = tenantId,
                Nombre = nombre,
                Codigo = codigo,
                Descripcion = descripcion,
                ColorEtiqueta = color,
                NivelOrden = request.NivelOrden,
                Activo = request.Activo
            };
            _db.NivelesClasificacion.Add(entity);
            _audit.Write(actorUserId, "nivel-clasificacion.create", nameof(NivelClasificacion), entity,
                previousValue: null,
                newValue: new { entity.Nombre, entity.Codigo, entity.ColorEtiqueta, entity.NivelOrden, entity.Activo },
                tenantId: entity.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<NivelClasificacionDto>.Ok(Map(entity));
    }

    public async Task<ArchivisticaResult<bool>> SetActivoAsync(
        long id, bool activo, string? motivo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.NivelesClasificacion.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ArchivisticaResult<bool>.NotFound("El nivel de clasificacion no existe.");
        }
        if (entity.Activo == activo)
        {
            return ArchivisticaResult<bool>.Ok(true);
        }

        var prev = entity.Activo;
        entity.Activo = activo;
        _audit.Write(actorUserId, activo ? "nivel-clasificacion.activar" : "nivel-clasificacion.inactivar",
            nameof(NivelClasificacion), entity,
            previousValue: new { Activo = prev },
            newValue: new { entity.Activo },
            tenantId: entity.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<bool>.Ok(true);
    }

    private static NivelClasificacionDto Map(NivelClasificacion n) => new(
        n.Id, n.Nombre, n.Codigo, n.Descripcion, n.ColorEtiqueta, n.NivelOrden, n.Activo);
}
