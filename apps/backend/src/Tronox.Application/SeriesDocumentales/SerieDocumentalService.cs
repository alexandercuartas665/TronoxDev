using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.SeriesDocumentales;

/// <summary>
/// Catalogo de Series y Subseries (RQ02 - RF02). El aislamiento por tenant lo garantiza el filtro
/// global; la logica de arbol (ciclos) es PURA (SerieRules) sobre el mapa de series del tenant.
///
/// Reglas de negocio implementadas aqui:
/// 1. codigo UNICO POR NIVEL (mismo padre) dentro del tenant, NO global (RF02 criterio 2).
/// 2. nombre UNICO ENTRE HERMANOS bajo el mismo padre (paridad con el legacy doc_catalogoTRD:
///    ExisteNombreDuplicadoEnMismoPadre).
/// 3. Nunca hay borrado fisico (invariante 8): se INACTIVA. Inactivar exige no tener subseries
///    ACTIVAS. Una serie Inactiva no se ofrece en RF04.
/// 4. Validacion de ciclos FAIL-CLOSED (un arbol ya corrupto se reporta como ciclo).
/// 5. Toda alta, edicion e inactivacion queda en la pista de auditoria (RF02 criterio 6),
///    auditando la ENTIDAD (no el id: en las altas el id todavia vale 0).
///
/// Cuando exista RF04 (construccion de la TRD) se sumara aqui la guarda "no eliminar una serie
/// asignada a la TRD" (RF02 criterio 3); hoy no aplica porque no hay borrado fisico ni tabla TRD.
/// </summary>
public sealed class SerieDocumentalService : ISerieDocumentalService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public SerieDocumentalService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Arbol / consulta ----

    public async Task<IReadOnlyList<SerieNodeDto>> GetTreeAsync(
        bool includeInactivas = false, CancellationToken cancellationToken = default)
    {
        var series = await _db.SeriesDocumentales.AsNoTracking()
            .Where(s => includeInactivas || s.Estado == SerieEstado.Activo)
            .OrderBy(s => s.Codigo).ThenBy(s => s.Nombre)
            .ToListAsync(cancellationToken);

        var byParent = series
            .GroupBy(s => s.ParentId)
            .ToDictionary(g => g.Key ?? 0, g => g.ToList());

        List<SerieNodeDto> BuildChildren(long? parentId)
        {
            if (!byParent.TryGetValue(parentId ?? 0, out var children))
            {
                return [];
            }
            return children.Select(s => ToNode(s, BuildChildren(s.Id))).ToList();
        }

        // Raices: sin padre O con padre fuera del conjunto visible (ej. padre inactivo cuando
        // includeInactivas = false): asi ninguna subserie visible queda huerfana.
        var visibleIds = series.Select(s => s.Id).ToHashSet();
        return series
            .Where(s => s.ParentId is null || !visibleIds.Contains(s.ParentId.Value))
            .Select(s => ToNode(s, BuildChildren(s.Id)))
            .ToList();
    }

    public async Task<SerieKpisDto> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        var series = await _db.SeriesDocumentales.AsNoTracking()
            .Select(s => new { s.ParentId, s.Estado })
            .ToListAsync(cancellationToken);

        return new SerieKpisDto(
            Series: series.Count(s => s.ParentId is null),
            Subseries: series.Count(s => s.ParentId is not null),
            Activas: series.Count(s => s.Estado == SerieEstado.Activo),
            Inactivas: series.Count(s => s.Estado == SerieEstado.Inactivo));
    }

    // ---- CRUD ----

    public async Task<SerieResult<SerieDto>> CreateAsync(
        SaveSerieRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return SerieResult<SerieDto>.Invalid("No hay tenant activo.");
        }
        var validation = await ValidateAsync(request, serieId: null, cancellationToken);
        if (validation is not null)
        {
            return validation.To<SerieDto>();
        }

        var serie = new SerieDocumental { TenantId = tenantId, Estado = SerieEstado.Activo };
        Apply(serie, request);
        _db.SeriesDocumentales.Add(serie);
        // Forma PREFERENTE de auditoria: la ENTIDAD, no el id (en un alta el id vale 0 hasta que
        // EF lo materializa durante SaveChanges).
        _audit.Write(actorUserId, "serie.create", nameof(SerieDocumental), serie,
            previousValue: null,
            newValue: Snapshot(serie),
            tenantId: serie.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return SerieResult<SerieDto>.Ok(await ToDtoAsync(serie, cancellationToken));
    }

    public async Task<SerieResult<SerieDto>> UpdateAsync(
        long serieId, SaveSerieRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var serie = await _db.SeriesDocumentales.FirstOrDefaultAsync(s => s.Id == serieId, cancellationToken);
        if (serie is null)
        {
            return SerieResult<SerieDto>.NotFound("La serie no existe.");
        }
        var validation = await ValidateAsync(request, serieId, cancellationToken);
        if (validation is not null)
        {
            return validation.To<SerieDto>();
        }

        if (request.ParentId != serie.ParentId)
        {
            var cycle = await ValidateParentMoveAsync(serieId, request.ParentId, cancellationToken);
            if (cycle is not null)
            {
                return cycle.To<SerieDto>();
            }
        }

        var prev = Snapshot(serie);
        Apply(serie, request);
        _audit.Write(actorUserId, "serie.update", nameof(SerieDocumental), serie,
            previousValue: prev,
            newValue: Snapshot(serie),
            tenantId: serie.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return SerieResult<SerieDto>.Ok(await ToDtoAsync(serie, cancellationToken));
    }

    public async Task<SerieResult<bool>> SetEstadoAsync(
        long serieId, bool activar, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var serie = await _db.SeriesDocumentales.FirstOrDefaultAsync(s => s.Id == serieId, cancellationToken);
        if (serie is null)
        {
            return SerieResult<bool>.NotFound("La serie no existe.");
        }

        // Invariante 8: nunca borrado fisico. Inactivar una serie con subseries ACTIVAS dejaria
        // subseries visibles colgando de un padre inactivo: se inactivan primero.
        if (!activar && await _db.SeriesDocumentales.AnyAsync(
                s => s.ParentId == serieId && s.Estado == SerieEstado.Activo, cancellationToken))
        {
            return SerieResult<bool>.Invalid(
                "No se puede inactivar la serie: tiene subseries activas. Inactivelas primero.");
        }

        var nuevo = activar ? SerieEstado.Activo : SerieEstado.Inactivo;
        if (serie.Estado == nuevo)
        {
            return SerieResult<bool>.Ok(true);
        }
        var prev = serie.Estado;
        serie.Estado = nuevo;
        _audit.Write(actorUserId, activar ? "serie.reactivar" : "serie.inactivar", nameof(SerieDocumental), serie,
            previousValue: new { Estado = prev },
            newValue: new { serie.Estado },
            tenantId: serie.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return SerieResult<bool>.Ok(true);
    }

    // ---- Validacion ----

    /// <summary>Validacion completa del alta/edicion. Null = valido.</summary>
    private async Task<SerieResult<bool>?> ValidateAsync(
        SaveSerieRequest request, long? serieId, CancellationToken cancellationToken)
    {
        // 1. Reglas PURAS (sin base de datos).
        var error = SerieRules.ValidateSerie(request.Codigo, request.Nombre, request.Descripcion);
        if (error is not null)
        {
            return SerieResult<bool>.Invalid(error);
        }

        // 2. El padre debe existir DENTRO del tenant (el filtro global lo garantiza).
        if (request.ParentId is long parentId
            && !await _db.SeriesDocumentales.AnyAsync(s => s.Id == parentId, cancellationToken))
        {
            return SerieResult<bool>.NotFound("La serie padre no existe.");
        }

        // 3. Codigo UNICO POR NIVEL (mismo padre) dentro del tenant (RF02 criterio 2). No global:
        //    el mismo codigo puede repetirse bajo padres distintos.
        var codigo = request.Codigo.Trim();
        var codigoUpper = codigo.ToUpperInvariant();
        var codigoDup = await _db.SeriesDocumentales.AsNoTracking().AnyAsync(
            s => s.ParentId == request.ParentId
                 && s.Codigo.ToUpper() == codigoUpper
                 && (serieId == null || s.Id != serieId),
            cancellationToken);
        if (codigoDup)
        {
            return SerieResult<bool>.Conflict(
                $"Ya existe una serie con el codigo '{codigo}' en el mismo nivel.");
        }

        // 4. Nombre UNICO ENTRE HERMANOS (paridad con el legacy ExisteNombreDuplicadoEnMismoPadre).
        var nombre = request.Nombre.Trim();
        var nombreUpper = nombre.ToUpperInvariant();
        var nombreDup = await _db.SeriesDocumentales.AsNoTracking().AnyAsync(
            s => s.ParentId == request.ParentId
                 && s.Nombre.ToUpper() == nombreUpper
                 && (serieId == null || s.Id != serieId),
            cancellationToken);
        if (nombreDup)
        {
            return SerieResult<bool>.Conflict(
                $"Ya existe una serie con el nombre '{nombre}' en el mismo nivel.");
        }

        return null;
    }

    private async Task<SerieResult<bool>?> ValidateParentMoveAsync(
        long serieId, long? newParentId, CancellationToken cancellationToken)
    {
        var parentById = await _db.SeriesDocumentales.AsNoTracking()
            .Select(s => new { s.Id, s.ParentId })
            .ToDictionaryAsync(s => s.Id, s => s.ParentId, cancellationToken);

        if (SerieRules.WouldCreateCycle(serieId, newParentId, parentById))
        {
            return SerieResult<bool>.Invalid(
                "El padre seleccionado crearia un ciclo: una serie no puede ser su propio ancestro.");
        }
        return null;
    }

    // ---- Mapeo ----

    private static void Apply(SerieDocumental serie, SaveSerieRequest request)
    {
        serie.Codigo = request.Codigo.Trim();
        serie.Nombre = request.Nombre.Trim();
        serie.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        serie.ParentId = request.ParentId;
    }

    private static SerieNodeDto ToNode(SerieDocumental s, IReadOnlyList<SerieNodeDto> children)
        => new(s.Id, s.ParentId, s.Codigo, s.Nombre, s.Descripcion, s.Estado, children);

    private static object Snapshot(SerieDocumental s) => new
    {
        s.Codigo, s.Nombre, s.Descripcion, s.ParentId, s.Estado
    };

    private async Task<SerieDto> ToDtoAsync(SerieDocumental s, CancellationToken cancellationToken)
    {
        string? parentNombre = s.ParentId is long pid
            ? await _db.SeriesDocumentales.AsNoTracking()
                .Where(p => p.Id == pid).Select(p => p.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new SerieDto(s.Id, s.ParentId, parentNombre, s.Codigo, s.Nombre, s.Descripcion, s.Estado);
    }
}
