using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Trd;

/// <summary>
/// Construccion de la TRD (RQ02 - RF04). El aislamiento por tenant lo garantiza el filtro global.
///
/// Reglas de negocio (RF04 3.4.4 + RF01 3.1.3):
/// 1. No se asigna nada si la version es Historico/Inactivo (3.4.4-1).
/// 2. El CCD se genera automaticamente (dependencia + serie) y no es editable (3.4.4-2).
/// 3. Una serie puede ir a varias dependencias con reglas distintas (3.4.2/3.4.4-3).
/// 4. No se asigna la misma serie dos veces a la misma dependencia en la misma version (3.4.4-4):
///    indice unico (version, dependencia, serie).
/// 5. Sobre una version Vigente solo se agregan series y se editan procedimiento/metadatos; NO se
///    tocan tiempos/disposicion/clasificacion ni se eliminan series (RF01 3.1.3).
/// 6. Solo se asignan series ACTIVAS del catalogo (3.4.1 paso 3).
/// 7. Auditoria de toda alta/edicion/cambio de estado (3.4.4-6).
/// </summary>
public sealed class TrdConstruccionService : ITrdConstruccionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public TrdConstruccionService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Consulta ----

    public async Task<TrdVersionCabeceraDto?> GetVersionAsync(long versionId, CancellationToken cancellationToken = default)
    {
        var v = await _db.TrdVersiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId, cancellationToken);
        return v is null ? null : new TrdVersionCabeceraDto(v.Id, v.CodigoVersion, v.Estado);
    }

    public async Task<IReadOnlyList<DependenciaTrdResumenDto>> GetDependenciasResumenAsync(
        long versionId, CancellationToken cancellationToken = default)
    {
        var dependencias = await _db.OrgUnits.AsNoTracking()
            .Where(u => u.Classifier == OrgUnitClassifier.Dependencia && !u.IsArchived)
            .OrderBy(u => u.Codigo).ThenBy(u => u.Name)
            .Select(u => new { u.Id, u.Codigo, u.Name })
            .ToListAsync(cancellationToken);

        var asignaciones = await _db.TrdAsignaciones.AsNoTracking()
            .Where(a => a.TrdVersionId == versionId)
            .Select(a => new { a.DependenciaOrgUnitId, a.IsArchived })
            .ToListAsync(cancellationToken);
        var porDep = asignaciones.GroupBy(a => a.DependenciaOrgUnitId)
            .ToDictionary(g => g.Key, g => (Total: g.Count(), Activas: g.Count(a => !a.IsArchived)));

        return dependencias.Select(d =>
        {
            porDep.TryGetValue(d.Id, out var c);
            return new DependenciaTrdResumenDto(d.Id, d.Codigo ?? "", d.Name, c.Total, c.Activas);
        }).ToList();
    }

    public async Task<IReadOnlyList<TrdAsignacionDto>> GetAsignacionesAsync(
        long versionId, long dependenciaId, bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var asignaciones = await _db.TrdAsignaciones.AsNoTracking()
            .Include(a => a.Serie)
            .Include(a => a.Dependencia)
            .Include(a => a.NivelClasificacion)
            .Include(a => a.Metadatos)
            .Where(a => a.TrdVersionId == versionId && a.DependenciaOrgUnitId == dependenciaId
                        && (includeArchived || !a.IsArchived))
            .OrderBy(a => a.CodigoCcd)
            .ToListAsync(cancellationToken);

        var listaNombres = await ListaNombresAsync(asignaciones, cancellationToken);
        return asignaciones.Select(a => ToDto(a, listaNombres)).ToList();
    }

    // ---- Asignaciones ----

    public async Task<TrdResult<TrdAsignacionDto>> AddAsignacionAsync(
        long versionId, AddAsignacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var version = await _db.TrdVersiones.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null) { return TrdResult<TrdAsignacionDto>.NotFound("La version de TRD no existe."); }
        if (!TrdConstruccionRules.PermiteAgregar(version.Estado))
        {
            return TrdResult<TrdAsignacionDto>.Invalid(TrdConstruccionRules.MensajeNoEditable);
        }

        var dependencia = await _db.OrgUnits.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.DependenciaId, cancellationToken);
        if (dependencia is null || dependencia.Classifier != OrgUnitClassifier.Dependencia)
        {
            return TrdResult<TrdAsignacionDto>.NotFound("La dependencia no existe.");
        }
        if (dependencia.IsArchived)
        {
            return TrdResult<TrdAsignacionDto>.Invalid("La dependencia esta archivada.");
        }

        var serie = await _db.SeriesDocumentales.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SerieId, cancellationToken);
        if (serie is null) { return TrdResult<TrdAsignacionDto>.NotFound("La serie no existe."); }
        if (serie.Estado != SerieEstado.Activo)
        {
            return TrdResult<TrdAsignacionDto>.Invalid("Solo se pueden asignar series Activas del catalogo (RF04 3.4.1).");
        }

        var nivel = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.NivelClasificacionId, cancellationToken);
        if (nivel is null) { return TrdResult<TrdAsignacionDto>.NotFound("El nivel de clasificacion no existe."); }

        var reglasError = TrdConstruccionRules.ValidateReglas(request.TiempoGestion, request.TiempoCentral, request.Procedimiento);
        if (reglasError is not null) { return TrdResult<TrdAsignacionDto>.Invalid(reglasError); }

        // RF04 3.4.4-4: la misma serie no se asigna dos veces a la misma dependencia en la version.
        var dup = await _db.TrdAsignaciones.AsNoTracking().AnyAsync(
            a => a.TrdVersionId == versionId && a.DependenciaOrgUnitId == request.DependenciaId
                 && a.SerieDocumentalId == request.SerieId, cancellationToken);
        if (dup)
        {
            return TrdResult<TrdAsignacionDto>.Conflict(
                "Esta serie ya esta asignada a esta dependencia en esta version (revise las inactivas).");
        }

        var asignacion = new TrdAsignacion
        {
            TenantId = _tenantContext.TenantId!.Value,
            TrdVersionId = versionId,
            DependenciaOrgUnitId = request.DependenciaId,
            SerieDocumentalId = request.SerieId,
            CodigoCcd = TrdConstruccionRules.ComponerCodigoCcd(dependencia.Codigo ?? "", serie.Codigo),
            TiempoGestion = request.TiempoGestion,
            TiempoCentral = request.TiempoCentral,
            DisposicionFinal = request.DisposicionFinal,
            ReproduccionTecnica = request.ReproduccionTecnica,
            SerieDdhhDih = request.SerieDdhhDih,
            Procedimiento = Normalize(request.Procedimiento),
            NivelClasificacionId = request.NivelClasificacionId
        };
        _db.TrdAsignaciones.Add(asignacion);
        _audit.Write(actorUserId, "trd.asignar", nameof(TrdAsignacion), asignacion,
            previousValue: null, newValue: Snapshot(asignacion), tenantId: asignacion.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<TrdAsignacionDto>.Ok((await GetAsignacionByIdAsync(asignacion.Id, cancellationToken))!);
    }

    public async Task<TrdResult<TrdAsignacionDto>> UpdateAsignacionAsync(
        long asignacionId, UpdateAsignacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var asignacion = await _db.TrdAsignaciones.FirstOrDefaultAsync(a => a.Id == asignacionId, cancellationToken);
        if (asignacion is null) { return TrdResult<TrdAsignacionDto>.NotFound("La asignacion no existe."); }
        var estado = await EstadoVersionAsync(asignacion.TrdVersionId, cancellationToken);
        if (!TrdConstruccionRules.PermiteEditarEstructura(estado))
        {
            return TrdResult<TrdAsignacionDto>.Invalid(estado == TrdVersionEstado.Vigente
                ? TrdConstruccionRules.MensajeVigenteSoloEstructura
                : TrdConstruccionRules.MensajeNoEditable);
        }
        var reglasError = TrdConstruccionRules.ValidateReglas(request.TiempoGestion, request.TiempoCentral, request.Procedimiento);
        if (reglasError is not null) { return TrdResult<TrdAsignacionDto>.Invalid(reglasError); }
        if (!await _db.NivelesClasificacion.AnyAsync(n => n.Id == request.NivelClasificacionId, cancellationToken))
        {
            return TrdResult<TrdAsignacionDto>.NotFound("El nivel de clasificacion no existe.");
        }

        var prev = Snapshot(asignacion);
        asignacion.TiempoGestion = request.TiempoGestion;
        asignacion.TiempoCentral = request.TiempoCentral;
        asignacion.DisposicionFinal = request.DisposicionFinal;
        asignacion.ReproduccionTecnica = request.ReproduccionTecnica;
        asignacion.SerieDdhhDih = request.SerieDdhhDih;
        asignacion.Procedimiento = Normalize(request.Procedimiento);
        asignacion.NivelClasificacionId = request.NivelClasificacionId;
        _audit.Write(actorUserId, "trd.asignacion.update", nameof(TrdAsignacion), asignacion,
            previousValue: prev, newValue: Snapshot(asignacion), tenantId: asignacion.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<TrdAsignacionDto>.Ok((await GetAsignacionByIdAsync(asignacion.Id, cancellationToken))!);
    }

    public async Task<TrdResult<TrdAsignacionDto>> UpdateProcedimientoAsync(
        long asignacionId, string? procedimiento, long actorUserId, CancellationToken cancellationToken = default)
    {
        var asignacion = await _db.TrdAsignaciones.FirstOrDefaultAsync(a => a.Id == asignacionId, cancellationToken);
        if (asignacion is null) { return TrdResult<TrdAsignacionDto>.NotFound("La asignacion no existe."); }
        var estado = await EstadoVersionAsync(asignacion.TrdVersionId, cancellationToken);
        if (!TrdConstruccionRules.PermiteEditarProcedimientoYMetadatos(estado))
        {
            return TrdResult<TrdAsignacionDto>.Invalid(TrdConstruccionRules.MensajeNoEditable);
        }
        var reglasError = TrdConstruccionRules.ValidateReglas(asignacion.TiempoGestion, asignacion.TiempoCentral, procedimiento);
        if (reglasError is not null) { return TrdResult<TrdAsignacionDto>.Invalid(reglasError); }

        var prev = new { asignacion.Procedimiento };
        asignacion.Procedimiento = Normalize(procedimiento);
        _audit.Write(actorUserId, "trd.asignacion.procedimiento", nameof(TrdAsignacion), asignacion,
            previousValue: prev, newValue: new { asignacion.Procedimiento }, tenantId: asignacion.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<TrdAsignacionDto>.Ok((await GetAsignacionByIdAsync(asignacion.Id, cancellationToken))!);
    }

    public async Task<TrdResult<bool>> SetAsignacionArchivedAsync(
        long asignacionId, bool archived, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var asignacion = await _db.TrdAsignaciones.FirstOrDefaultAsync(a => a.Id == asignacionId, cancellationToken);
        if (asignacion is null) { return TrdResult<bool>.NotFound("La asignacion no existe."); }
        var estado = await EstadoVersionAsync(asignacion.TrdVersionId, cancellationToken);
        // Eliminar/inactivar (y reactivar) solo En Construccion (RF01 3.1.3).
        if (!TrdConstruccionRules.PermiteEliminar(estado))
        {
            return TrdResult<bool>.Invalid(estado == TrdVersionEstado.Vigente
                ? TrdConstruccionRules.MensajeVigenteSoloEstructura
                : TrdConstruccionRules.MensajeNoEditable);
        }
        if (asignacion.IsArchived == archived) { return TrdResult<bool>.Ok(true); }
        asignacion.IsArchived = archived;
        _audit.Write(actorUserId, archived ? "trd.asignacion.inactivar" : "trd.asignacion.reactivar",
            nameof(TrdAsignacion), asignacion,
            previousValue: new { IsArchived = !archived }, newValue: new { asignacion.IsArchived },
            tenantId: asignacion.TenantId, reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<bool>.Ok(true);
    }

    // ---- Metadatos ----

    public async Task<TrdResult<TrdMetadatoDto>> AddMetadatoAsync(
        long asignacionId, SaveMetadatoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var asignacion = await _db.TrdAsignaciones.AsNoTracking().FirstOrDefaultAsync(a => a.Id == asignacionId, cancellationToken);
        if (asignacion is null) { return TrdResult<TrdMetadatoDto>.NotFound("La asignacion no existe."); }
        var estado = await EstadoVersionAsync(asignacion.TrdVersionId, cancellationToken);
        if (!TrdConstruccionRules.PermiteEditarProcedimientoYMetadatos(estado))
        {
            return TrdResult<TrdMetadatoDto>.Invalid(TrdConstruccionRules.MensajeNoEditable);
        }
        var val = await ValidarMetadatoAsync(request, cancellationToken);
        if (val is not null) { return val.To<TrdMetadatoDto>(); }

        var maxOrden = await _db.TrdMetadatos.Where(m => m.TrdAsignacionId == asignacionId)
            .Select(m => (int?)m.Orden).MaxAsync(cancellationToken) ?? 0;

        var metadato = new TrdMetadato
        {
            TenantId = asignacion.TenantId,
            TrdAsignacionId = asignacionId,
            Nombre = request.Nombre.Trim(),
            TipoDato = request.TipoDato,
            Obligatorio = request.Obligatorio,
            ListaMaestraId = request.TipoDato == TipoDatoMetadato.Lista ? request.ListaMaestraId : null,
            Contexto = ContextoMetadato.Expediente,
            Orden = maxOrden + 1
        };
        _db.TrdMetadatos.Add(metadato);
        _audit.Write(actorUserId, "trd.metadato.create", nameof(TrdMetadato), metadato,
            previousValue: null, newValue: SnapshotMeta(metadato), tenantId: metadato.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<TrdMetadatoDto>.Ok(await ToMetaDtoAsync(metadato, cancellationToken));
    }

    public async Task<TrdResult<TrdMetadatoDto>> UpdateMetadatoAsync(
        long metadatoId, SaveMetadatoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var metadato = await _db.TrdMetadatos.FirstOrDefaultAsync(m => m.Id == metadatoId, cancellationToken);
        if (metadato is null) { return TrdResult<TrdMetadatoDto>.NotFound("El metadato no existe."); }
        var estado = await EstadoVersionDeAsignacionAsync(metadato.TrdAsignacionId, cancellationToken);
        if (!TrdConstruccionRules.PermiteEditarProcedimientoYMetadatos(estado))
        {
            return TrdResult<TrdMetadatoDto>.Invalid(TrdConstruccionRules.MensajeNoEditable);
        }
        var val = await ValidarMetadatoAsync(request, cancellationToken);
        if (val is not null) { return val.To<TrdMetadatoDto>(); }

        var prev = SnapshotMeta(metadato);
        metadato.Nombre = request.Nombre.Trim();
        metadato.TipoDato = request.TipoDato;
        metadato.Obligatorio = request.Obligatorio;
        metadato.ListaMaestraId = request.TipoDato == TipoDatoMetadato.Lista ? request.ListaMaestraId : null;
        _audit.Write(actorUserId, "trd.metadato.update", nameof(TrdMetadato), metadato,
            previousValue: prev, newValue: SnapshotMeta(metadato), tenantId: metadato.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<TrdMetadatoDto>.Ok(await ToMetaDtoAsync(metadato, cancellationToken));
    }

    public async Task<TrdResult<bool>> SetMetadatoArchivedAsync(
        long metadatoId, bool archived, long actorUserId, CancellationToken cancellationToken = default)
    {
        var metadato = await _db.TrdMetadatos.FirstOrDefaultAsync(m => m.Id == metadatoId, cancellationToken);
        if (metadato is null) { return TrdResult<bool>.NotFound("El metadato no existe."); }
        var estado = await EstadoVersionDeAsignacionAsync(metadato.TrdAsignacionId, cancellationToken);
        if (!TrdConstruccionRules.PermiteEditarProcedimientoYMetadatos(estado))
        {
            return TrdResult<bool>.Invalid(TrdConstruccionRules.MensajeNoEditable);
        }
        if (metadato.IsArchived == archived) { return TrdResult<bool>.Ok(true); }
        metadato.IsArchived = archived;
        _audit.Write(actorUserId, archived ? "trd.metadato.inactivar" : "trd.metadato.reactivar",
            nameof(TrdMetadato), metadato,
            previousValue: new { IsArchived = !archived }, newValue: new { metadato.IsArchived },
            tenantId: metadato.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TrdResult<bool>.Ok(true);
    }

    // ---- Helpers ----

    private async Task<TrdResult<bool>?> ValidarMetadatoAsync(SaveMetadatoRequest request, CancellationToken cancellationToken)
    {
        var error = TrdConstruccionRules.ValidateMetadato(request.Nombre, request.TipoDato, request.ListaMaestraId);
        if (error is not null) { return TrdResult<bool>.Invalid(error); }
        if (request.TipoDato == TipoDatoMetadato.Lista && request.ListaMaestraId is long listaId)
        {
            var lista = await _db.ListasMaestras.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == listaId, cancellationToken);
            if (lista is null) { return TrdResult<bool>.NotFound("La lista seleccionada no existe."); }
            if (lista.Estado != ListaEstado.Activo)
            {
                return TrdResult<bool>.Invalid("La lista seleccionada esta inactiva.");
            }
        }
        return null;
    }

    private async Task<TrdVersionEstado> EstadoVersionAsync(long versionId, CancellationToken cancellationToken)
        => await _db.TrdVersiones.AsNoTracking().Where(v => v.Id == versionId)
            .Select(v => v.Estado).FirstAsync(cancellationToken);

    private async Task<TrdVersionEstado> EstadoVersionDeAsignacionAsync(long asignacionId, CancellationToken cancellationToken)
        => await _db.TrdAsignaciones.AsNoTracking().Where(a => a.Id == asignacionId)
            .Join(_db.TrdVersiones, a => a.TrdVersionId, v => v.Id, (a, v) => v.Estado)
            .FirstAsync(cancellationToken);

    private async Task<TrdAsignacionDto?> GetAsignacionByIdAsync(long id, CancellationToken cancellationToken)
    {
        var a = await _db.TrdAsignaciones.AsNoTracking()
            .Include(x => x.Serie).Include(x => x.Dependencia).Include(x => x.NivelClasificacion).Include(x => x.Metadatos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null) { return null; }
        var listaNombres = await ListaNombresAsync([a], cancellationToken);
        return ToDto(a, listaNombres);
    }

    private async Task<Dictionary<long, string>> ListaNombresAsync(
        IReadOnlyCollection<TrdAsignacion> asignaciones, CancellationToken cancellationToken)
    {
        var listaIds = asignaciones.SelectMany(a => a.Metadatos)
            .Where(m => m.ListaMaestraId is not null).Select(m => m.ListaMaestraId!.Value).Distinct().ToList();
        if (listaIds.Count == 0) { return []; }
        return await _db.ListasMaestras.AsNoTracking()
            .Where(l => listaIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.NombreLista, cancellationToken);
    }

    private static TrdAsignacionDto ToDto(TrdAsignacion a, IReadOnlyDictionary<long, string> listaNombres) => new(
        a.Id, a.TrdVersionId, a.DependenciaOrgUnitId, a.Dependencia?.Codigo ?? "", a.Dependencia?.Name ?? "",
        a.SerieDocumentalId, a.Serie?.Codigo ?? "", a.Serie?.Nombre ?? "", a.Serie?.ParentId is not null,
        a.CodigoCcd, a.TiempoGestion, a.TiempoCentral, a.DisposicionFinal, a.ReproduccionTecnica,
        a.SerieDdhhDih, a.Procedimiento, a.NivelClasificacionId, a.NivelClasificacion?.Nombre ?? "",
        a.IsArchived,
        a.Metadatos.OrderBy(m => m.Orden).Select(m => ToMetaDto(m, listaNombres)).ToList());

    private static TrdMetadatoDto ToMetaDto(TrdMetadato m, IReadOnlyDictionary<long, string> listaNombres) => new(
        m.Id, m.TrdAsignacionId, m.Nombre, m.TipoDato, m.Obligatorio, m.Orden, m.ListaMaestraId,
        m.ListaMaestraId is long id && listaNombres.TryGetValue(id, out var n) ? n : null, m.IsArchived);

    private async Task<TrdMetadatoDto> ToMetaDtoAsync(TrdMetadato m, CancellationToken cancellationToken)
    {
        string? nombre = m.ListaMaestraId is long id
            ? await _db.ListasMaestras.AsNoTracking().Where(l => l.Id == id).Select(l => l.NombreLista).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new TrdMetadatoDto(m.Id, m.TrdAsignacionId, m.Nombre, m.TipoDato, m.Obligatorio, m.Orden, m.ListaMaestraId, nombre, m.IsArchived);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(TrdAsignacion a) => new
    {
        a.TrdVersionId, a.DependenciaOrgUnitId, a.SerieDocumentalId, a.CodigoCcd, a.TiempoGestion,
        a.TiempoCentral, a.DisposicionFinal, a.ReproduccionTecnica, a.SerieDdhhDih, a.Procedimiento,
        a.NivelClasificacionId, a.IsArchived
    };

    private static object SnapshotMeta(TrdMetadato m) => new
    {
        m.Nombre, m.TipoDato, m.Obligatorio, m.Orden, m.ListaMaestraId, m.Contexto, m.IsArchived
    };
}
