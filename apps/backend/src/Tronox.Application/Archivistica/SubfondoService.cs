using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Subfondos documentales (RQ01 - RF02 seccion 5.2.2). Opcionales: un fondo puede no tener
/// ninguno. CodigoSubfondo es unico DENTRO DEL FONDO, no dentro del tenant.
///
/// Crear un subfondo cruza IFondoService.EnsureAdmiteAltasAsync: es la primera pieza real que
/// ejerce la regla "fondo Cerrado = solo lectura".
/// </summary>
public sealed class SubfondoService : ISubfondoService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;
    private readonly IFondoService _fondos;

    public SubfondoService(IApplicationDbContext db, ITenantContext tenant, IAuditWriter audit, IFondoService fondos)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _fondos = fondos;
    }

    public async Task<IReadOnlyList<SubfondoDto>> ListAsync(long fondoId, CancellationToken cancellationToken = default)
        => await _db.Subfondos.AsNoTracking()
            .Where(s => s.FondoId == fondoId)
            .OrderBy(s => s.CodigoSubfondo)
            .Select(s => new SubfondoDto(s.Id, s.FondoId, s.CodigoSubfondo, s.NombreSubfondo, s.Estado))
            .ToListAsync(cancellationToken);

    public async Task<SubfondoDto?> GetAsync(long id, CancellationToken cancellationToken = default)
        => await _db.Subfondos.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SubfondoDto(s.Id, s.FondoId, s.CodigoSubfondo, s.NombreSubfondo, s.Estado))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ArchivisticaResult<SubfondoDto>> SaveAsync(
        SaveSubfondoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var error = ArchivisticaRules.ValidateSubfondo(request.CodigoSubfondo, request.NombreSubfondo);
        if (error is not null)
        {
            return ArchivisticaResult<SubfondoDto>.Invalid(error);
        }

        var codigo = request.CodigoSubfondo.Trim().ToUpperInvariant();

        Subfondo entity;
        if (request.Id is long id)
        {
            var found = await _db.Subfondos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (found is null)
            {
                return ArchivisticaResult<SubfondoDto>.NotFound("El subfondo no existe.");
            }
            // Unicidad del codigo DENTRO del fondo (no del tenant).
            if (await _db.Subfondos.AnyAsync(
                    x => x.FondoId == found.FondoId && x.CodigoSubfondo == codigo && x.Id != id, cancellationToken))
            {
                return ArchivisticaResult<SubfondoDto>.Conflict(
                    $"Ya existe un subfondo con el codigo '{codigo}' en este fondo.");
            }

            entity = found;
            var prev = new { entity.CodigoSubfondo, entity.NombreSubfondo, entity.Estado };
            entity.CodigoSubfondo = codigo;
            entity.NombreSubfondo = request.NombreSubfondo.Trim();
            entity.Estado = request.Estado;
            _audit.Write(actorUserId, "subfondo.update", nameof(Subfondo), entity,
                previousValue: prev,
                newValue: new { entity.CodigoSubfondo, entity.NombreSubfondo, entity.Estado },
                tenantId: entity.TenantId);
        }
        else
        {
            if (_tenant.TenantId is not long tenantId)
            {
                return ArchivisticaResult<SubfondoDto>.Invalid("No hay tenant activo.");
            }

            // Un fondo Cerrado no admite nada nuevo colgando de el (regla 2 de RF02).
            var admite = await _fondos.EnsureAdmiteAltasAsync(request.FondoId, cancellationToken);
            if (!admite.IsOk)
            {
                return new ArchivisticaResult<SubfondoDto>(admite.Status, null, admite.Error);
            }

            if (await _db.Subfondos.AnyAsync(
                    x => x.FondoId == request.FondoId && x.CodigoSubfondo == codigo, cancellationToken))
            {
                return ArchivisticaResult<SubfondoDto>.Conflict(
                    $"Ya existe un subfondo con el codigo '{codigo}' en este fondo.");
            }

            entity = new Subfondo
            {
                TenantId = tenantId,
                FondoId = request.FondoId,
                CodigoSubfondo = codigo,
                NombreSubfondo = request.NombreSubfondo.Trim(),
                Estado = request.Estado
            };
            _db.Subfondos.Add(entity);
            _audit.Write(actorUserId, "subfondo.create", nameof(Subfondo), entity,
                previousValue: null,
                newValue: new { entity.FondoId, entity.CodigoSubfondo, entity.NombreSubfondo, entity.Estado },
                tenantId: entity.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<SubfondoDto>.Ok(
            new SubfondoDto(entity.Id, entity.FondoId, entity.CodigoSubfondo, entity.NombreSubfondo, entity.Estado));
    }
}
