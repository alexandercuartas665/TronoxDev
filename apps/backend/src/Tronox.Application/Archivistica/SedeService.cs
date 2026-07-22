using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Administracion de sedes de la entidad (RQ01 - RF01 seccion 4.1.2). Las sedes son OPCIONALES.
/// CodigoSede es unico POR TENANT. Nunca se borran: se inactivan con motivo y auditoria
/// (invariante 8), y una sede Inactiva no se ofrece al crear fondos.
/// </summary>
public sealed class SedeService : ISedeService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;

    public SedeService(IApplicationDbContext db, ITenantContext tenant, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SedeDto>> ListAsync(
        bool soloActivas = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Sedes.AsNoTracking();
        if (soloActivas)
        {
            query = query.Where(s => s.Estado == SedeEstado.Activo);
        }
        return await query
            .OrderBy(s => s.NombreSede)
            .Select(s => new SedeDto(
                s.Id, s.NombreSede, s.CodigoSede, s.SiglaSede,
                s.PaisId, s.DepartamentoId, s.CiudadId,
                s.Direccion, s.Telefono, s.CorreoSede, s.Estado))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Criterio de aceptacion de RF01 4.1.2: una sede Inactiva NO debe ofrecerse al crear fondos.
    /// El formulario de fondos consume esta lista, no ListAsync().
    /// </summary>
    public Task<IReadOnlyList<SedeDto>> ListSeleccionablesParaFondoAsync(CancellationToken cancellationToken = default)
        => ListAsync(soloActivas: true, cancellationToken);

    public async Task<SedeDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var s = await _db.Sedes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return s is null ? null : Map(s);
    }

    public async Task<ArchivisticaResult<SedeDto>> SaveAsync(
        SaveSedeRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var error = ArchivisticaRules.ValidateSede(
            request.NombreSede, request.CodigoSede, request.SiglaSede,
            request.Direccion, request.Telefono, request.CorreoSede);
        if (error is not null)
        {
            return ArchivisticaResult<SedeDto>.Invalid(error);
        }

        var codigo = request.CodigoSede.Trim().ToUpperInvariant();

        Sede entity;
        if (request.Id is long id)
        {
            var found = await _db.Sedes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (found is null)
            {
                return ArchivisticaResult<SedeDto>.NotFound("La sede no existe.");
            }
            // Unicidad DENTRO del tenant: el filtro global ya acota la consulta a este tenant.
            if (await _db.Sedes.AnyAsync(x => x.CodigoSede == codigo && x.Id != id, cancellationToken))
            {
                return ArchivisticaResult<SedeDto>.Conflict($"Ya existe una sede con el codigo '{codigo}'.");
            }

            entity = found;
            var prev = new { entity.NombreSede, entity.CodigoSede, entity.SiglaSede, entity.Direccion, entity.Estado };
            Apply(entity, request, codigo);
            _audit.Write(actorUserId, "sede.update", nameof(Sede), entity,
                previousValue: prev,
                newValue: new { entity.NombreSede, entity.CodigoSede, entity.SiglaSede, entity.Direccion, entity.Estado },
                tenantId: entity.TenantId);
        }
        else
        {
            if (_tenant.TenantId is not long tenantId)
            {
                return ArchivisticaResult<SedeDto>.Invalid("No hay tenant activo.");
            }
            if (await _db.Sedes.AnyAsync(x => x.CodigoSede == codigo, cancellationToken))
            {
                return ArchivisticaResult<SedeDto>.Conflict($"Ya existe una sede con el codigo '{codigo}'.");
            }

            entity = new Sede { TenantId = tenantId };
            Apply(entity, request, codigo);
            _db.Sedes.Add(entity);
            _audit.Write(actorUserId, "sede.create", nameof(Sede), entity,
                previousValue: null,
                newValue: new { entity.NombreSede, entity.CodigoSede, entity.SiglaSede, entity.Direccion, entity.Estado },
                tenantId: entity.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<SedeDto>.Ok(Map(entity));
    }

    public async Task<ArchivisticaResult<bool>> InactivarAsync(
        long id, string? motivo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Sedes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ArchivisticaResult<bool>.NotFound("La sede no existe.");
        }
        if (entity.Estado == SedeEstado.Inactivo)
        {
            return ArchivisticaResult<bool>.Ok(true);
        }

        entity.Estado = SedeEstado.Inactivo;
        _audit.Write(actorUserId, "sede.inactivar", nameof(Sede), entity,
            previousValue: new { Estado = SedeEstado.Activo },
            newValue: new { entity.Estado },
            tenantId: entity.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<bool>.Ok(true);
    }

    private static void Apply(Sede entity, SaveSedeRequest request, string codigo)
    {
        entity.NombreSede = request.NombreSede.Trim();
        entity.CodigoSede = codigo;
        entity.SiglaSede = request.SiglaSede.Trim().ToUpperInvariant();
        entity.PaisId = request.PaisId;
        entity.DepartamentoId = request.DepartamentoId;
        entity.CiudadId = request.CiudadId;
        entity.Direccion = request.Direccion.Trim();
        entity.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
        entity.CorreoSede = string.IsNullOrWhiteSpace(request.CorreoSede) ? null : request.CorreoSede.Trim();
        entity.Estado = request.Estado;
    }

    private static SedeDto Map(Sede s) => new(
        s.Id, s.NombreSede, s.CodigoSede, s.SiglaSede,
        s.PaisId, s.DepartamentoId, s.CiudadId,
        s.Direccion, s.Telefono, s.CorreoSede, s.Estado);
}
