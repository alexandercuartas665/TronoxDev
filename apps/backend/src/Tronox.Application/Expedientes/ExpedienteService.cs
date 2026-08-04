using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Tenancy;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Expedientes;

/// <summary>
/// Bandeja de expedientes (RQ03). El aislamiento por tenant lo garantiza el filtro global; la
/// visibilidad por clasificacion (RF10) se resuelve fail-closed AQUI, no confiando en el llamador:
/// el nivel maximo del usuario se calcula de sus roles vigentes y nunca se lista un expediente con
/// NivelOrden mayor.
/// </summary>
public sealed class ExpedienteService : IExpedienteService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISequenceService _sequences;
    private readonly IAuditWriter _audit;

    public ExpedienteService(
        IApplicationDbContext db, ITenantContext tenantContext, ISequenceService sequences, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sequences = sequences;
        _audit = audit;
    }

    // ---- Bandeja ----

    public async Task<ExpedienteBandejaDto> GetBandejaAsync(
        BandejaVista vista, ExpedienteFiltro filtro, long actorUserId, CancellationToken cancellationToken = default)
    {
        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);

        var query = _db.Expedientes.AsNoTracking()
            .Include(e => e.TrdAsignacion!).ThenInclude(a => a.Serie)
            .Include(e => e.TrdAsignacion!).ThenInclude(a => a.Dependencia!).ThenInclude(d => d.Fondo)
            .Include(e => e.NivelClasificacion)
            .Where(e => !e.Eliminado)
            // RF10 fail-closed: nunca listar por encima del nivel del usuario.
            .Where(e => e.NivelClasificacion!.NivelOrden <= nivelMax);

        query = vista switch
        {
            BandejaVista.Mis => query.Where(e => e.CreatedBy == actorUserId),
            // Compartir (RF11) es un slice posterior: por ahora la vista queda vacia.
            BandejaVista.Compartidos => query.Where(_ => false),
            BandejaVista.Publicos => query.Where(e => e.NivelClasificacion!.NivelOrden == 1),
            BandejaVista.Central => query.Where(e => e.Fase == FaseArchivo.Central),
            BandejaVista.Historico => query.Where(e => e.Fase == FaseArchivo.Historico),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var t = filtro.Texto.Trim().ToLower();
            // Contains sobre ToLower es neutro entre PostgreSQL y SQL Server (ADR-001), a diferencia
            // de EF.Functions.ILike que solo existe en Npgsql.
            query = query.Where(e => e.Codigo.ToLower().Contains(t)
                                     || e.Nombre.ToLower().Contains(t)
                                     || e.TrdAsignacion!.Serie!.Nombre.ToLower().Contains(t));
        }
        if (filtro.Estado is EstadoExpediente est) { query = query.Where(e => e.Estado == est); }
        if (filtro.Fase is FaseArchivo fase) { query = query.Where(e => e.Fase == fase); }
        if (filtro.NivelClasificacionId is long nid) { query = query.Where(e => e.NivelClasificacionId == nid); }
        if (filtro.DependenciaId is long dep) { query = query.Where(e => e.TrdAsignacion!.DependenciaOrgUnitId == dep); }
        if (filtro.SerieId is long ser) { query = query.Where(e => e.TrdAsignacion!.SerieDocumentalId == ser); }
        if (filtro.AperturaDesde is DateOnly d1) { query = query.Where(e => e.FechaApertura >= d1); }
        if (filtro.AperturaHasta is DateOnly d2) { query = query.Where(e => e.FechaApertura <= d2); }

        var rows = await query.OrderByDescending(e => e.CreatedAt).ToListAsync(cancellationToken);

        var creadorNombres = await ResolverNombresAsync(rows.Select(e => e.CreatedBy), cancellationToken);

        var items = rows.Select(e => new ExpedienteBandejaItemDto(
            e.Id,
            e.Codigo,
            e.Nombre,
            e.TrdAsignacion?.Serie?.Codigo ?? "",
            e.TrdAsignacion?.Serie?.Nombre ?? "",
            e.TrdAsignacion?.Dependencia?.Name ?? "",
            e.FechaApertura,
            e.FechaCierre,
            e.Estado,
            e.Fase,
            e.NivelClasificacion?.Nombre ?? "",
            e.NivelClasificacion?.NivelOrden ?? 0,
            e.CreatedBy is long cb && creadorNombres.TryGetValue(cb, out var n) ? n : null,
            e.TrdAsignacion?.Dependencia?.Fondo?.NombreFondo,
            e.CreatedAt,
            e.Estado == EstadoExpediente.Abierto)).ToList();

        var stats = new ExpedienteStatsDto(
            items.Count,
            items.Count(i => i.Estado == EstadoExpediente.Abierto),
            items.Count(i => i.Estado == EstadoExpediente.Cerrado));

        return new ExpedienteBandejaDto(items, stats);
    }

    // ---- Opciones de creacion ----

    public async Task<IReadOnlyList<FondoOpcionDto>> GetFondosAsync(CancellationToken cancellationToken = default)
        => await _db.Fondos.AsNoTracking()
            .Where(f => f.Estado == FondoEstado.Activo)
            .OrderBy(f => f.CodigoFondo)
            .Select(f => new FondoOpcionDto(f.Id, f.CodigoFondo, f.NombreFondo))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DependenciaOpcionDto>> GetDependenciasParaCrearAsync(
        long? fondoId, CancellationToken cancellationToken = default)
    {
        // Dependencias con al menos una asignacion en la version Vigente (asi hay series que cruzar).
        var conAsignacion = _db.TrdAsignaciones.AsNoTracking()
            .Where(a => !a.IsArchived && a.TrdVersion!.Estado == TrdVersionEstado.Vigente)
            .Select(a => a.DependenciaOrgUnitId)
            .Distinct();

        var query = _db.OrgUnits.AsNoTracking()
            .Where(u => u.Classifier == OrgUnitClassifier.Dependencia && !u.IsArchived)
            .Where(u => conAsignacion.Contains(u.Id));
        if (fondoId is long fid) { query = query.Where(u => u.FondoId == fid); }

        return await query.OrderBy(u => u.Codigo).ThenBy(u => u.Name)
            .Select(u => new DependenciaOpcionDto(u.Id, u.Codigo ?? "", u.Name, u.FondoId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SerieOpcionDto>> GetSeriesParaCrearAsync(
        long dependenciaId, CancellationToken cancellationToken = default)
        => await _db.TrdAsignaciones.AsNoTracking()
            .Include(a => a.Serie)
            .Include(a => a.NivelClasificacion)
            .Where(a => !a.IsArchived && a.DependenciaOrgUnitId == dependenciaId
                        && a.TrdVersion!.Estado == TrdVersionEstado.Vigente)
            .OrderBy(a => a.CodigoCcd)
            .Select(a => new SerieOpcionDto(
                a.Id,
                a.Serie!.Codigo,
                a.Serie!.Nombre,
                a.CodigoCcd,
                a.NivelClasificacionId,
                a.NivelClasificacion!.Nombre,
                a.NivelClasificacion!.NivelOrden))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MetadatoDefDto>> GetMetadatosSerieAsync(
        long trdAsignacionId, CancellationToken cancellationToken = default)
    {
        var metas = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdAsignacionId == trdAsignacionId
                        && m.Contexto == ContextoMetadato.Expediente
                        && !m.IsArchived)
            .OrderBy(m => m.Orden)
            .Select(m => new { m.Id, m.Nombre, m.TipoDato, m.Obligatorio, m.ListaMaestraId })
            .ToListAsync(cancellationToken);

        var listaIds = metas.Where(m => m.ListaMaestraId is not null).Select(m => m.ListaMaestraId!.Value).Distinct().ToList();
        var opciones = listaIds.Count == 0
            ? []
            : await _db.ListaOpciones.AsNoTracking()
                .Where(o => listaIds.Contains(o.ListaMaestraId))
                .OrderBy(o => o.Orden)
                .Select(o => new { o.ListaMaestraId, o.Clave, o.Valor })
                .ToListAsync(cancellationToken);

        return metas.Select(m => new MetadatoDefDto(
            m.Id, m.Nombre, m.TipoDato, m.Obligatorio, m.ListaMaestraId,
            opciones.Where(o => o.ListaMaestraId == m.ListaMaestraId)
                .Select(o => new MetadatoOpcionDto(o.Clave, o.Valor)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<NivelClasificacionOpcionDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
        => await _db.NivelesClasificacion.AsNoTracking()
            .Where(n => n.Activo)
            .OrderBy(n => n.NivelOrden)
            .Select(n => new NivelClasificacionOpcionDto(n.Id, n.Codigo, n.Nombre, n.NivelOrden))
            .ToListAsync(cancellationToken);

    // ---- Crear ----

    public async Task<ExpedienteResult<ExpedienteDetalleDto>> CrearAsync(
        CrearExpedienteRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var asignacion = await _db.TrdAsignaciones.AsNoTracking()
            .Include(a => a.Serie)
            .Include(a => a.Dependencia)
            .Include(a => a.NivelClasificacion)
            .Include(a => a.TrdVersion)
            .FirstOrDefaultAsync(a => a.Id == request.TrdAsignacionId, cancellationToken);
        if (asignacion is null) { return ExpedienteResult<ExpedienteDetalleDto>.NotFound("La serie/asignacion de TRD no existe."); }
        if (asignacion.IsArchived || asignacion.TrdVersion!.Estado != TrdVersionEstado.Vigente)
        {
            return ExpedienteResult<ExpedienteDetalleDto>.Invalid("Solo se crean expedientes sobre series de la TRD Vigente (RF03).");
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var errNombre = ExpedienteRules.ValidateNombre(request.Nombre);
        if (errNombre is not null) { return ExpedienteResult<ExpedienteDetalleDto>.Invalid(errNombre); }
        var errFecha = ExpedienteRules.ValidateFechaApertura(request.FechaApertura, hoy);
        if (errFecha is not null) { return ExpedienteResult<ExpedienteDetalleDto>.Invalid(errFecha); }

        var nivelElegido = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.NivelClasificacionId, cancellationToken);
        if (nivelElegido is null) { return ExpedienteResult<ExpedienteDetalleDto>.NotFound("El nivel de clasificacion no existe."); }
        if (!ExpedienteRules.PuedeElevar(asignacion.NivelClasificacion!.NivelOrden, nivelElegido.NivelOrden))
        {
            return ExpedienteResult<ExpedienteDetalleDto>.Invalid(ExpedienteRules.MensajeNoBajarClasificacion);
        }

        var defs = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdAsignacionId == asignacion.Id && m.Contexto == ContextoMetadato.Expediente && !m.IsArchived)
            .Select(m => new { m.Id, m.Nombre, m.Obligatorio })
            .ToListAsync(cancellationToken);
        var valores = request.Metadatos
            .GroupBy(m => m.TrdMetadatoId)
            .ToDictionary(g => g.Key, g => g.Last().Valor);
        var errMeta = ExpedienteRules.ValidateMetadatosObligatorios(
            defs.Select(d => (d.Id, d.Nombre, d.Obligatorio)), valores);
        if (errMeta is not null) { return ExpedienteResult<ExpedienteDetalleDto>.Invalid(errMeta); }

        var anio = request.FechaApertura.Year;
        var seqCode = ExpedienteRules.SequenceCode(anio);
        // EnsureSequence ANTES de la transaccion (una carrera de creacion no debe envenenarla).
        await _sequences.EnsureSequenceAsync(seqCode, cancellationToken);

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);
        var consecutivo = await _sequences.NextAsync(seqCode, "", ExpedienteRules.ConsecutivoPadding, cancellationToken);
        var codigo = ExpedienteRules.ComponerCodigo(
            asignacion.Dependencia?.Codigo ?? "", asignacion.Serie?.Codigo ?? "", anio, consecutivo);

        var tenantId = _tenantContext.TenantId!.Value;
        var expediente = new Expediente
        {
            TenantId = tenantId,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            TrdAsignacionId = asignacion.Id,
            NivelClasificacionId = nivelElegido.Id,
            Estado = EstadoExpediente.Abierto,
            Fase = FaseArchivo.Gestion,
            EstadoUbicacion = EstadoUbicacionExpediente.SinUbicar,
            FechaApertura = request.FechaApertura
        };
        var defIds = defs.Select(d => d.Id).ToHashSet();
        foreach (var input in valores)
        {
            if (!defIds.Contains(input.Key) || string.IsNullOrWhiteSpace(input.Value)) { continue; }
            expediente.Metadatos.Add(new ExpedienteMetadato
            {
                TenantId = tenantId,
                TrdMetadatoId = input.Key,
                Valor = input.Value!.Trim()
            });
        }

        _db.Expedientes.Add(expediente);
        _audit.Write(actorUserId, "expediente.crear", nameof(Expediente), expediente,
            previousValue: null, newValue: new { expediente.Codigo, expediente.Nombre, expediente.TrdAsignacionId },
            tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return ExpedienteResult<ExpedienteDetalleDto>.Ok((await BuildDetalleAsync(expediente.Id, cancellationToken))!);
    }

    // ---- Detalle ----

    public async Task<ExpedienteResult<ExpedienteDetalleDto>> GetDetalleAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var nivelOrden = await _db.Expedientes.AsNoTracking()
            .Where(e => e.Id == id && !e.Eliminado)
            .Select(e => (int?)e.NivelClasificacion!.NivelOrden)
            .FirstOrDefaultAsync(cancellationToken);
        if (nivelOrden is null) { return ExpedienteResult<ExpedienteDetalleDto>.NotFound("El expediente no existe."); }
        if (nivelOrden.Value > nivelMax)
        {
            // La existencia misma es informacion restringida (RF01/RF10): se responde como no encontrado.
            return ExpedienteResult<ExpedienteDetalleDto>.NotFound("El expediente no existe.");
        }

        var dto = await BuildDetalleAsync(id, cancellationToken);
        return dto is null
            ? ExpedienteResult<ExpedienteDetalleDto>.NotFound("El expediente no existe.")
            : ExpedienteResult<ExpedienteDetalleDto>.Ok(dto);
    }

    // ---- Editar ----

    public async Task<ExpedienteResult<ExpedienteDetalleDto>> EditarAsync(
        long id, EditarExpedienteRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var expediente = await _db.Expedientes
            .Include(e => e.TrdAsignacion!).ThenInclude(a => a.NivelClasificacion)
            .Include(e => e.Metadatos)
            .FirstOrDefaultAsync(e => e.Id == id && !e.Eliminado, cancellationToken);
        if (expediente is null) { return ExpedienteResult<ExpedienteDetalleDto>.NotFound("El expediente no existe."); }
        if (expediente.Estado != EstadoExpediente.Abierto)
        {
            return ExpedienteResult<ExpedienteDetalleDto>.Invalid("Solo se editan expedientes en estado Abierto (RF01).");
        }

        var errNombre = ExpedienteRules.ValidateNombre(request.Nombre);
        if (errNombre is not null) { return ExpedienteResult<ExpedienteDetalleDto>.Invalid(errNombre); }

        var nivelElegido = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.NivelClasificacionId, cancellationToken);
        if (nivelElegido is null) { return ExpedienteResult<ExpedienteDetalleDto>.NotFound("El nivel de clasificacion no existe."); }
        if (!ExpedienteRules.PuedeElevar(expediente.TrdAsignacion!.NivelClasificacion!.NivelOrden, nivelElegido.NivelOrden))
        {
            return ExpedienteResult<ExpedienteDetalleDto>.Invalid(ExpedienteRules.MensajeNoBajarClasificacion);
        }

        var defs = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdAsignacionId == expediente.TrdAsignacionId
                        && m.Contexto == ContextoMetadato.Expediente && !m.IsArchived)
            .Select(m => new { m.Id, m.Nombre, m.Obligatorio })
            .ToListAsync(cancellationToken);
        var valores = request.Metadatos
            .GroupBy(m => m.TrdMetadatoId)
            .ToDictionary(g => g.Key, g => g.Last().Valor);
        var errMeta = ExpedienteRules.ValidateMetadatosObligatorios(
            defs.Select(d => (d.Id, d.Nombre, d.Obligatorio)), valores);
        if (errMeta is not null) { return ExpedienteResult<ExpedienteDetalleDto>.Invalid(errMeta); }

        var prev = new { expediente.Nombre, expediente.NivelClasificacionId };
        expediente.Nombre = request.Nombre.Trim();
        expediente.NivelClasificacionId = nivelElegido.Id;

        // Reemplazo de metadatos (borra los actuales y reinserta los provistos que sean validos).
        var defIds = defs.Select(d => d.Id).ToHashSet();
        expediente.Metadatos.Clear();
        foreach (var input in valores)
        {
            if (!defIds.Contains(input.Key) || string.IsNullOrWhiteSpace(input.Value)) { continue; }
            expediente.Metadatos.Add(new ExpedienteMetadato
            {
                TenantId = expediente.TenantId,
                TrdMetadatoId = input.Key,
                Valor = input.Value!.Trim()
            });
        }

        _audit.Write(actorUserId, "expediente.editar", nameof(Expediente), expediente,
            previousValue: prev, newValue: new { expediente.Nombre, expediente.NivelClasificacionId },
            tenantId: expediente.TenantId);
        await _db.SaveChangesAsync(cancellationToken);

        return ExpedienteResult<ExpedienteDetalleDto>.Ok((await BuildDetalleAsync(expediente.Id, cancellationToken))!);
    }

    // ---- Eliminar (logico) ----

    public async Task<ExpedienteResult<bool>> EliminarAsync(
        long id, string justificacion, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(justificacion))
        {
            return ExpedienteResult<bool>.Invalid("La justificacion de eliminacion es obligatoria (RF01).");
        }
        var expediente = await _db.Expedientes.FirstOrDefaultAsync(e => e.Id == id && !e.Eliminado, cancellationToken);
        if (expediente is null) { return ExpedienteResult<bool>.NotFound("El expediente no existe."); }

        expediente.Eliminado = true;
        expediente.FechaEliminacion = DateTime.UtcNow;
        expediente.EliminadoPorUserId = actorUserId;
        expediente.JustificacionEliminacion = justificacion.Trim();

        _audit.Write(actorUserId, "expediente.eliminar", nameof(Expediente), expediente,
            previousValue: new { Eliminado = false }, newValue: new { Eliminado = true, Motivo = justificacion.Trim() },
            tenantId: expediente.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpedienteResult<bool>.Ok(true);
    }

    // ---- Helpers ----

    /// <summary>
    /// Nivel de clasificacion maximo (NivelOrden) del usuario, a partir de sus roles vigentes. Union
    /// por el mayor. FAIL-CLOSED: sin roles vigentes -> 0 (por debajo de Publico=1) -> no ve nada.
    /// </summary>
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

    private async Task<Dictionary<long, string>> ResolverNombresAsync(
        IEnumerable<long?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) { return []; }
        var users = await _db.TenantUsers.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Nombres, u.Apellidos, u.Email })
            .ToListAsync(cancellationToken);
        return users.ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.Nombres) && string.IsNullOrWhiteSpace(u.Apellidos)
                ? u.Email
                : $"{u.Nombres} {u.Apellidos}".Trim());
    }

    private async Task<ExpedienteDetalleDto?> BuildDetalleAsync(long id, CancellationToken cancellationToken)
    {
        var e = await _db.Expedientes.AsNoTracking()
            .Include(x => x.TrdAsignacion!).ThenInclude(a => a.Serie)
            .Include(x => x.TrdAsignacion!).ThenInclude(a => a.Dependencia!).ThenInclude(d => d.Fondo)
            .Include(x => x.TrdAsignacion!).ThenInclude(a => a.TrdVersion)
            .Include(x => x.TrdAsignacion!).ThenInclude(a => a.NivelClasificacion)
            .Include(x => x.NivelClasificacion)
            .Include(x => x.Metadatos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (e is null) { return null; }

        var defs = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdAsignacionId == e.TrdAsignacionId && m.Contexto == ContextoMetadato.Expediente)
            .OrderBy(m => m.Orden)
            .Select(m => new { m.Id, m.Nombre, m.TipoDato })
            .ToListAsync(cancellationToken);
        var valores = e.Metadatos.ToDictionary(m => m.TrdMetadatoId, m => m.Valor);
        var metas = defs.Select(d => new MetadatoValorDto(
            d.Id, d.Nombre, d.TipoDato, valores.TryGetValue(d.Id, out var v) ? v : null)).ToList();

        var nombres = await ResolverNombresAsync([e.CreatedBy], cancellationToken);

        return new ExpedienteDetalleDto(
            e.Id,
            e.Codigo,
            e.Nombre,
            e.TrdAsignacionId,
            e.TrdAsignacion?.Serie?.Codigo ?? "",
            e.TrdAsignacion?.Serie?.Nombre ?? "",
            e.TrdAsignacion?.Dependencia?.Name ?? "",
            e.TrdAsignacion?.Dependencia?.Fondo?.NombreFondo,
            e.TrdAsignacion?.TrdVersion?.CodigoVersion ?? "",
            e.Estado,
            e.Fase,
            e.EstadoUbicacion,
            e.NivelClasificacionId,
            e.NivelClasificacion?.Nombre ?? "",
            e.NivelClasificacion?.NivelOrden ?? 0,
            e.TrdAsignacion?.NivelClasificacion?.NivelOrden ?? 0,
            e.FechaApertura,
            e.FechaCierre,
            e.CreatedAt,
            e.CreatedBy is long cb && nombres.TryGetValue(cb, out var nm) ? nm : null,
            metas);
    }
}
