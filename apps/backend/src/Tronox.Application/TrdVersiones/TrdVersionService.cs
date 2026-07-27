using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.TrdVersiones;

/// <summary>
/// Versiones de la Tabla de Retencion Documental (RQ02 - RF01). El aislamiento por tenant lo
/// garantiza el filtro global.
///
/// Reglas de negocio implementadas aqui:
/// 1. codigo_version UNICO POR TENANT (RF01 3.1.4-1).
/// 2. Solo UNA version Vigente por tenant (RF01 3.1.4-2): al activar una, la Vigente anterior pasa
///    AUTOMATICAMENTE a Historico (3.1.4-3). Ademas lo respalda un indice unico parcial en BD.
/// 3. Maquina de estados (RF01 3.1.2): editar/activar/descartar solo desde EnConstruccion.
/// 4. Nunca hay borrado fisico (invariante 8): una version se descarta pasando a Inactivo.
/// 5. Toda alta y cambio de estado queda en la pista de auditoria (RF01 3.1.4-7), auditando la
///    ENTIDAD (no el id: en las altas el id todavia vale 0).
/// </summary>
public sealed class TrdVersionService : ITrdVersionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public TrdVersionService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Consulta ----

    public async Task<IReadOnlyList<TrdVersionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var versiones = await _db.TrdVersiones.AsNoTracking()
            // Vigente primero, luego En Construccion, luego el resto; dentro de cada grupo, la mas
            // reciente arriba.
            .OrderBy(v => v.Estado == TrdVersionEstado.Vigente ? 0
                : v.Estado == TrdVersionEstado.EnConstruccion ? 1 : 2)
            .ThenByDescending(v => v.FechaVigenciaDesde)
            .ThenByDescending(v => v.Id)
            .ToListAsync(cancellationToken);
        return versiones.Select(ToDto).ToList();
    }

    public async Task<TrdVersionDto?> GetAsync(long versionId, CancellationToken cancellationToken = default)
    {
        var version = await _db.TrdVersiones.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        return version is null ? null : ToDto(version);
    }

    public async Task<TrdVersionKpisDto> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        var estados = await _db.TrdVersiones.AsNoTracking().Select(v => v.Estado).ToListAsync(cancellationToken);
        return new TrdVersionKpisDto(
            Total: estados.Count,
            HayVigente: estados.Any(e => e == TrdVersionEstado.Vigente),
            EnConstruccion: estados.Count(e => e == TrdVersionEstado.EnConstruccion),
            Historicas: estados.Count(e => e == TrdVersionEstado.Historico));
    }

    // ---- CRUD ----

    public async Task<TrdVersionResult<TrdVersionDto>> CreateAsync(
        SaveTrdVersionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return TrdVersionResult<TrdVersionDto>.Invalid("No hay tenant activo.");
        }
        var validation = await ValidateAsync(request, versionId: null, cancellationToken);
        if (validation is not null)
        {
            return validation.To<TrdVersionDto>();
        }

        var version = new TrdVersion { TenantId = tenantId, Estado = TrdVersionEstado.EnConstruccion };
        Apply(version, request);
        _db.TrdVersiones.Add(version);
        _audit.Write(actorUserId, "trdversion.create", nameof(TrdVersion), version,
            previousValue: null,
            newValue: Snapshot(version),
            tenantId: version.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdVersionResult<TrdVersionDto>.Ok(ToDto(version));
    }

    public async Task<TrdVersionResult<TrdVersionDto>> UpdateAsync(
        long versionId, SaveTrdVersionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var version = await _db.TrdVersiones.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return TrdVersionResult<TrdVersionDto>.NotFound("La version de TRD no existe.");
        }
        var estadoError = TrdVersionRules.CanEditar(version.Estado);
        if (estadoError is not null)
        {
            return TrdVersionResult<TrdVersionDto>.Invalid(estadoError);
        }
        var validation = await ValidateAsync(request, versionId, cancellationToken);
        if (validation is not null)
        {
            return validation.To<TrdVersionDto>();
        }

        var prev = Snapshot(version);
        Apply(version, request);
        _audit.Write(actorUserId, "trdversion.update", nameof(TrdVersion), version,
            previousValue: prev,
            newValue: Snapshot(version),
            tenantId: version.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdVersionResult<TrdVersionDto>.Ok(ToDto(version));
    }

    public async Task<TrdVersionResult<TrdVersionDto>> ActivarAsync(
        long versionId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var version = await _db.TrdVersiones.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return TrdVersionResult<TrdVersionDto>.NotFound("La version de TRD no existe.");
        }
        var estadoError = TrdVersionRules.CanActivar(version.Estado);
        if (estadoError is not null)
        {
            return TrdVersionResult<TrdVersionDto>.Invalid(estadoError);
        }

        // Se DEGRADA primero la Vigente anterior (si existe) y se guarda, ANTES de promover la
        // nueva. Hacerlo en dos SaveChanges evita que el indice unico parcial "una sola Vigente por
        // tenant" se viole a mitad de un unico batch (Postgres verifica la unicidad por sentencia).
        var vigenteAnterior = await _db.TrdVersiones
            .FirstOrDefaultAsync(v => v.Estado == TrdVersionEstado.Vigente && v.Id != versionId, cancellationToken);
        if (vigenteAnterior is not null)
        {
            vigenteAnterior.Estado = TrdVersionEstado.Historico;
            _audit.Write(actorUserId, "trdversion.historico", nameof(TrdVersion), vigenteAnterior,
                previousValue: new { Estado = TrdVersionEstado.Vigente },
                newValue: new { vigenteAnterior.Estado },
                tenantId: vigenteAnterior.TenantId,
                reason: $"Reemplazada automaticamente por la version {version.CodigoVersion} (RF01 3.1.4-3).");
            await _db.SaveChangesAsync(cancellationToken);
        }

        version.Estado = TrdVersionEstado.Vigente;
        _audit.Write(actorUserId, "trdversion.activar", nameof(TrdVersion), version,
            previousValue: new { Estado = TrdVersionEstado.EnConstruccion },
            newValue: new { version.Estado },
            tenantId: version.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdVersionResult<TrdVersionDto>.Ok(ToDto(version));
    }

    public async Task<TrdVersionResult<TrdVersionDto>> DescartarAsync(
        long versionId, long actorUserId, string? motivo = null, CancellationToken cancellationToken = default)
    {
        var version = await _db.TrdVersiones.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return TrdVersionResult<TrdVersionDto>.NotFound("La version de TRD no existe.");
        }
        var estadoError = TrdVersionRules.CanDescartar(version.Estado);
        if (estadoError is not null)
        {
            return TrdVersionResult<TrdVersionDto>.Invalid(estadoError);
        }

        version.Estado = TrdVersionEstado.Inactivo;
        _audit.Write(actorUserId, "trdversion.descartar", nameof(TrdVersion), version,
            previousValue: new { Estado = TrdVersionEstado.EnConstruccion },
            newValue: new { version.Estado },
            tenantId: version.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdVersionResult<TrdVersionDto>.Ok(ToDto(version));
    }

    // ---- Validacion / mapeo ----

    private async Task<TrdVersionResult<bool>?> ValidateAsync(
        SaveTrdVersionRequest request, long? versionId, CancellationToken cancellationToken)
    {
        var error = TrdVersionRules.ValidateVersion(
            request.CodigoVersion, request.Descripcion, request.ActoAdministrativo, request.FechaVigenciaDesde);
        if (error is not null)
        {
            return TrdVersionResult<bool>.Invalid(error);
        }

        // codigo_version UNICO POR TENANT (RF01 3.1.4-1), case-insensitive.
        var codigo = request.CodigoVersion.Trim();
        var codigoUpper = codigo.ToUpperInvariant();
        var dup = await _db.TrdVersiones.AsNoTracking().AnyAsync(
            v => v.CodigoVersion.ToUpper() == codigoUpper && (versionId == null || v.Id != versionId),
            cancellationToken);
        if (dup)
        {
            return TrdVersionResult<bool>.Conflict($"Ya existe una version con el codigo '{codigo}' en la entidad.");
        }
        return null;
    }

    private static void Apply(TrdVersion version, SaveTrdVersionRequest request)
    {
        version.CodigoVersion = request.CodigoVersion.Trim();
        version.Descripcion = Normalize(request.Descripcion);
        version.ActoAdministrativo = Normalize(request.ActoAdministrativo);
        version.FechaVigenciaDesde = request.FechaVigenciaDesde;
        version.FechaAprobacion = request.FechaAprobacion;
        version.FechaConvalidacion = request.FechaConvalidacion;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TrdVersionDto ToDto(TrdVersion v) => new(
        v.Id, v.CodigoVersion, v.Descripcion, v.ActoAdministrativo,
        v.FechaVigenciaDesde, v.FechaAprobacion, v.FechaConvalidacion, v.Estado);

    private static object Snapshot(TrdVersion v) => new
    {
        v.CodigoVersion, v.Descripcion, v.ActoAdministrativo,
        v.FechaVigenciaDesde, v.FechaAprobacion, v.FechaConvalidacion, v.Estado
    };
}
