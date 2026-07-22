using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Fondos documentales (RQ01 - RF02). Aislamiento por tenant via filtro global.
///
/// Reglas de negocio implementadas aqui (la validacion pura vive en ArchivisticaRules):
/// 1. Multiples fondos por tenant.
/// 2. Un fondo Cerrado es de SOLO LECTURA (EnsureAdmiteAltasAsync es la puerta que deben cruzar
///    subfondos, series y expedientes); consultar y exportar sigue permitido sin limite.
/// 3. CodigoFondo unico DENTRO del tenant (no global).
/// 4. No se permite ELIMINAR un fondo con dependencias: se sugiere Inactivar o Cerrar.
/// 5. Toda creacion y modificacion queda en la pista de auditoria (auditando la ENTIDAD).
/// </summary>
public sealed class FondoService : IFondoService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;

    public FondoService(IApplicationDbContext db, ITenantContext tenant, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<FondoDto>> ListAsync(CancellationToken cancellationToken = default)
        => await _db.Fondos.AsNoTracking()
            .OrderBy(f => f.CodigoFondo)
            .Select(f => new FondoDto(
                f.Id, f.CodigoFondo, f.NombreFondo, f.Descripcion,
                f.SedeId, f.Sede != null ? f.Sede.NombreSede : null,
                f.TipoFondo, f.Estado, f.FechaApertura, f.FechaCierre, f.EntidadOrigen))
            .ToListAsync(cancellationToken);

    public async Task<FondoDto?> GetAsync(long id, CancellationToken cancellationToken = default)
        => await _db.Fondos.AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new FondoDto(
                f.Id, f.CodigoFondo, f.NombreFondo, f.Descripcion,
                f.SedeId, f.Sede != null ? f.Sede.NombreSede : null,
                f.TipoFondo, f.Estado, f.FechaApertura, f.FechaCierre, f.EntidadOrigen))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ArchivisticaResult<FondoDto>> SaveAsync(
        SaveFondoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var error = ArchivisticaRules.ValidateFondo(
            request.CodigoFondo, request.NombreFondo, request.Descripcion,
            request.TipoFondo, request.Estado,
            request.FechaApertura, request.FechaCierre, request.EntidadOrigen);
        if (error is not null)
        {
            return ArchivisticaResult<FondoDto>.Invalid(error);
        }

        // La sede es opcional (fondo transversal si va en null), pero si se indica debe existir
        // en el tenant y estar Activa: una sede Inactiva no se ofrece al crear fondos.
        if (request.SedeId is long sedeId)
        {
            var sede = await _db.Sedes.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sedeId, cancellationToken);
            if (sede is null)
            {
                return ArchivisticaResult<FondoDto>.NotFound("La sede indicada no existe.");
            }
            if (sede.Estado == SedeEstado.Inactivo)
            {
                return ArchivisticaResult<FondoDto>.Invalid(
                    $"La sede '{sede.NombreSede}' esta Inactiva y no puede asignarse a un fondo.");
            }
        }

        var codigo = request.CodigoFondo.Trim().ToUpperInvariant();

        Fondo entity;
        if (request.Id is long id)
        {
            var found = await _db.Fondos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (found is null)
            {
                return ArchivisticaResult<FondoDto>.NotFound("El fondo no existe.");
            }
            // Un fondo Cerrado se puede REABRIR (cambiando su estado) pero no se le pueden
            // modificar los datos manteniendolo cerrado: solo lectura mientras siga Cerrado.
            if (found.Estado == FondoEstado.Cerrado && request.Estado == FondoEstado.Cerrado)
            {
                return ArchivisticaResult<FondoDto>.Invalid(ArchivisticaRules.FondoCerradoSoloLectura);
            }
            // Unicidad DENTRO del tenant: el filtro global acota la consulta a este tenant, asi
            // que el mismo codigo en OTRO tenant no colisiona.
            if (await _db.Fondos.AnyAsync(x => x.CodigoFondo == codigo && x.Id != id, cancellationToken))
            {
                return ArchivisticaResult<FondoDto>.Conflict($"Ya existe un fondo con el codigo '{codigo}' en la entidad.");
            }

            entity = found;
            var prev = new { entity.CodigoFondo, entity.NombreFondo, entity.SedeId, entity.TipoFondo, entity.Estado };
            Apply(entity, request, codigo);
            _audit.Write(actorUserId, "fondo.update", nameof(Fondo), entity,
                previousValue: prev,
                newValue: new { entity.CodigoFondo, entity.NombreFondo, entity.SedeId, entity.TipoFondo, entity.Estado },
                tenantId: entity.TenantId);
        }
        else
        {
            if (_tenant.TenantId is not long tenantId)
            {
                return ArchivisticaResult<FondoDto>.Invalid("No hay tenant activo.");
            }
            if (await _db.Fondos.AnyAsync(x => x.CodigoFondo == codigo, cancellationToken))
            {
                return ArchivisticaResult<FondoDto>.Conflict($"Ya existe un fondo con el codigo '{codigo}' en la entidad.");
            }

            entity = new Fondo { TenantId = tenantId };
            Apply(entity, request, codigo);
            _db.Fondos.Add(entity);
            _audit.Write(actorUserId, "fondo.create", nameof(Fondo), entity,
                previousValue: null,
                newValue: new { entity.CodigoFondo, entity.NombreFondo, entity.SedeId, entity.TipoFondo, entity.Estado },
                tenantId: entity.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<FondoDto>.Ok(await ReloadAsync(entity.Id, cancellationToken));
    }

    public async Task<ArchivisticaResult<FondoDto>> CerrarAsync(
        long id, DateOnly fechaCierre, string? motivo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Fondos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ArchivisticaResult<FondoDto>.NotFound("El fondo no existe.");
        }
        if (entity.Estado == FondoEstado.Cerrado)
        {
            return ArchivisticaResult<FondoDto>.Invalid("El fondo ya esta Cerrado.");
        }

        var error = ArchivisticaRules.ValidateFondo(
            entity.CodigoFondo, entity.NombreFondo, entity.Descripcion,
            entity.TipoFondo, FondoEstado.Cerrado, entity.FechaApertura, fechaCierre, entity.EntidadOrigen);
        if (error is not null)
        {
            return ArchivisticaResult<FondoDto>.Invalid(error);
        }

        var prev = new { entity.Estado, entity.FechaCierre };
        entity.Estado = FondoEstado.Cerrado;
        entity.FechaCierre = fechaCierre;
        _audit.Write(actorUserId, "fondo.cerrar", nameof(Fondo), entity,
            previousValue: prev,
            newValue: new { entity.Estado, entity.FechaCierre },
            tenantId: entity.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<FondoDto>.Ok(await ReloadAsync(entity.Id, cancellationToken));
    }

    public async Task<ArchivisticaResult<FondoDto>> InactivarAsync(
        long id, string? motivo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Fondos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ArchivisticaResult<FondoDto>.NotFound("El fondo no existe.");
        }
        if (entity.Estado == FondoEstado.Inactivo)
        {
            return ArchivisticaResult<FondoDto>.Ok(await ReloadAsync(entity.Id, cancellationToken));
        }

        var prev = entity.Estado;
        entity.Estado = FondoEstado.Inactivo;
        _audit.Write(actorUserId, "fondo.inactivar", nameof(Fondo), entity,
            previousValue: new { Estado = prev },
            newValue: new { entity.Estado },
            tenantId: entity.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<FondoDto>.Ok(await ReloadAsync(entity.Id, cancellationToken));
    }

    /// <summary>
    /// Regla 4 de RF02 + invariante 8: los fondos NO se eliminan. Si hay dependencias el mensaje
    /// las nombra; si no las hay, igualmente se rechaza y se dirige a Inactivar (nunca hay
    /// borrado fisico de un fondo).
    /// </summary>
    public async Task<ArchivisticaResult<bool>> DeleteAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Fondos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return ArchivisticaResult<bool>.NotFound("El fondo no existe.");
        }

        var dependencias = await DescribirDependenciasAsync(id, cancellationToken);
        if (dependencias is not null)
        {
            return ArchivisticaResult<bool>.Invalid(ArchivisticaRules.FondoNoEliminable(dependencias));
        }

        return ArchivisticaResult<bool>.Invalid(
            "Los fondos documentales no se eliminan. Inactivelo o cierrelo segun corresponda.");
    }

    /// <summary>
    /// PUNTO DE EXTENSION de la regla 4: enumera lo que cuelga del fondo. Hoy solo existen los
    /// subfondos; cuando entren dependencias, series y expedientes (RQ02/RQ03) se suman aqui
    /// como conteos adicionales y el mensaje se compone solo.
    /// </summary>
    private async Task<string?> DescribirDependenciasAsync(long fondoId, CancellationToken cancellationToken)
    {
        var partes = new List<string>();

        var subfondos = await _db.Subfondos.CountAsync(s => s.FondoId == fondoId, cancellationToken);
        if (subfondos > 0)
        {
            partes.Add($"{subfondos} subfondo(s)");
        }

        // TODO(RQ02/RQ03): sumar aqui series documentales y expedientes cuando existan sus tablas.

        return partes.Count == 0 ? null : string.Join(", ", partes);
    }

    /// <summary>
    /// Puerta unica de la regla "fondo Cerrado = solo lectura". La cruza todo modulo que quiera
    /// crear algo colgando del fondo (subfondos hoy; series y expedientes cuando existan).
    /// </summary>
    public async Task<ArchivisticaResult<bool>> EnsureAdmiteAltasAsync(
        long fondoId, CancellationToken cancellationToken = default)
    {
        var estado = await _db.Fondos.AsNoTracking()
            .Where(f => f.Id == fondoId)
            .Select(f => (FondoEstado?)f.Estado)
            .FirstOrDefaultAsync(cancellationToken);

        if (estado is not FondoEstado actual)
        {
            return ArchivisticaResult<bool>.NotFound("El fondo no existe.");
        }
        if (ArchivisticaRules.EsSoloLectura(actual))
        {
            return ArchivisticaResult<bool>.Invalid(ArchivisticaRules.FondoCerradoSoloLectura);
        }
        return ArchivisticaResult<bool>.Ok(true);
    }

    private static void Apply(Fondo entity, SaveFondoRequest request, string codigo)
    {
        entity.CodigoFondo = codigo;
        entity.NombreFondo = request.NombreFondo.Trim();
        entity.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        // SedeId null es un valor con significado: fondo transversal a toda la entidad.
        entity.SedeId = request.SedeId;
        entity.TipoFondo = request.TipoFondo;
        entity.Estado = request.Estado;
        entity.FechaApertura = request.FechaApertura;
        entity.FechaCierre = request.FechaCierre;
        entity.EntidadOrigen = string.IsNullOrWhiteSpace(request.EntidadOrigen) ? null : request.EntidadOrigen.Trim();
    }

    private async Task<FondoDto> ReloadAsync(long id, CancellationToken cancellationToken)
        => (await GetAsync(id, cancellationToken))!;
}
