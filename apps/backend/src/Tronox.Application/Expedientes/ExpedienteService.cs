using System.Security.Cryptography;
using System.Text;
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
    private readonly IRotuloExportador _rotulos;

    public ExpedienteService(
        IApplicationDbContext db, ITenantContext tenantContext, ISequenceService sequences, IAuditWriter audit,
        IRotuloExportador rotulos)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sequences = sequences;
        _audit = audit;
        _rotulos = rotulos;
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

    // ---- Rotulacion (RF17) ----

    public async Task<ExpedienteResult<byte[]>> GenerarRotulosAsync(
        IReadOnlyList<long> ids, RotuloTamano tamano, int porHoja, int posicionInicio,
        long actorUserId, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Count == 0) { return ExpedienteResult<byte[]>.Invalid("Seleccione al menos un expediente."); }
        if (ids.Count > 50) { return ExpedienteResult<byte[]>.Invalid("Maximo 50 rotulos por generacion."); }

        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var distintos = ids.Distinct().ToList();

        // Fail-closed por clasificacion (RF10): solo los expedientes que el usuario puede ver.
        var exps = await _db.Expedientes.AsNoTracking()
            .Include(e => e.TrdAsignacion!).ThenInclude(a => a.Serie!).ThenInclude(s => s.Parent)
            .Include(e => e.TrdAsignacion!).ThenInclude(a => a.Dependencia!).ThenInclude(d => d.Fondo)
            .Include(e => e.NivelClasificacion)
            .Where(e => !e.Eliminado && distintos.Contains(e.Id))
            .Where(e => e.NivelClasificacion!.NivelOrden <= nivelMax)
            .ToListAsync(cancellationToken);
        if (exps.Count == 0) { return ExpedienteResult<byte[]>.NotFound("No hay expedientes visibles para rotular."); }

        var expIds = exps.Select(e => e.Id).ToList();

        // Folios = suma de folios de los documentos vigentes del expediente.
        var folios = await _db.Documentos.AsNoTracking()
            .Where(d => d.ExpedienteId != null && expIds.Contains(d.ExpedienteId!.Value) && !d.EsVersionHistorica)
            .GroupBy(d => d.ExpedienteId!.Value)
            .Select(g => new { ExpId = g.Key, Folios = g.Sum(x => x.Folios ?? 0) })
            .ToDictionaryAsync(x => x.ExpId, x => x.Folios, cancellationToken);

        // Ubicacion actual = ultima asignacion de ubicacion fisica por expediente.
        var ubic = await _db.ExpedienteUbicaciones.AsNoTracking()
            .Include(u => u.TopografiaElemento)
            .Where(u => expIds.Contains(u.ExpedienteId))
            .OrderByDescending(u => u.Id)
            .ToListAsync(cancellationToken);
        var ubicPorExp = ubic.GroupBy(u => u.ExpedienteId).ToDictionary(g => g.Key, g => g.First());

        // Respetar el orden de seleccion del usuario.
        var porId = exps.ToDictionary(e => e.Id);
        var rotulos = new List<RotuloDatoDto>();
        foreach (var id in distintos)
        {
            if (!porId.TryGetValue(id, out var e)) { continue; }
            var nodo = e.TrdAsignacion?.Serie;
            var esSub = nodo?.ParentId != null;
            var serie = esSub ? nodo!.Parent : nodo;
            var subserie = esSub ? nodo : null;
            var ubicLabel = ubicPorExp.TryGetValue(e.Id, out var u) && u.TopografiaElemento is not null
                ? $"{u.TopografiaElemento.Sigla} - {u.TopografiaElemento.Nombre}".Trim(' ', '-')
                : "";
            rotulos.Add(new RotuloDatoDto(
                Fondo: e.TrdAsignacion?.Dependencia?.Fondo?.NombreFondo ?? "",
                Seccion: e.TrdAsignacion?.Dependencia?.Name ?? "",
                Serie: serie is null ? "" : $"{serie.Codigo} {serie.Nombre}".Trim(),
                Subserie: subserie is null ? "" : $"{subserie.Codigo} {subserie.Nombre}".Trim(),
                CodigoExp: e.Codigo,
                NombreExp: e.Nombre,
                FechaInicial: e.FechaApertura.ToString("dd/MM/yyyy"),
                FechaFinal: e.FechaCierre?.ToString("dd/MM/yyyy") ?? "",
                Folios: (folios.TryGetValue(e.Id, out var f) ? f : 0).ToString(),
                Ubicacion: ubicLabel));
        }

        var pdf = _rotulos.Generar(rotulos, tamano, porHoja, posicionInicio);
        return ExpedienteResult<byte[]>.Ok(pdf);
    }

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
            metas,
            e.TrdAsignacion?.CodigoCcd ?? "",
            e.TrdAsignacion?.TiempoGestion ?? 0,
            e.TrdAsignacion?.TiempoCentral ?? 0,
            e.TrdAsignacion?.DisposicionFinal ?? DisposicionFinal.ConservacionTotal,
            e.TrdAsignacion?.SerieDdhhDih ?? false,
            e.TrdAsignacion?.Procedimiento);
    }

    // ================= Cierre / reapertura (RF08) =================

    public async Task<ExpedienteResult<int>> CerrarAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var e = await _db.Expedientes.Include(x => x.Metadatos)
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);
        if (e is null) { return ExpedienteResult<int>.NotFound("El expediente no existe."); }
        if (e.Estado == EstadoExpediente.Cerrado) { return ExpedienteResult<int>.Invalid("El expediente ya esta cerrado."); }

        var tenantId = _tenantContext.TenantId!.Value;
        var numero = await _db.ExpedienteCierres.Where(c => c.ExpedienteId == id).CountAsync(cancellationToken) + 1;
        var hash = CalcularHashIndice(e);
        _db.ExpedienteCierres.Add(new ExpedienteCierre
        {
            TenantId = tenantId, ExpedienteId = id, NumeroCierre = numero, HashSha256 = hash, JustificacionReapertura = null
        });
        var prev = new { e.Estado };
        e.Estado = EstadoExpediente.Cerrado;
        e.FechaCierre = DateOnly.FromDateTime(DateTime.UtcNow);
        _audit.Write(actorUserId, "expediente.cerrar", nameof(Expediente), e,
            previousValue: prev, newValue: new { e.Estado, e.FechaCierre, HashIndice = hash }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpedienteResult<int>.Ok(numero);
    }

    public async Task<ExpedienteResult<int>> ReabrirAsync(long id, string justificacion, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(justificacion) || justificacion.Trim().Length < 20)
        {
            return ExpedienteResult<int>.Invalid("La justificacion de reapertura debe tener al menos 20 caracteres.");
        }
        var e = await _db.Expedientes.Include(x => x.Metadatos)
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);
        if (e is null) { return ExpedienteResult<int>.NotFound("El expediente no existe."); }
        if (e.Estado == EstadoExpediente.Abierto) { return ExpedienteResult<int>.Invalid("El expediente ya esta abierto."); }

        var tenantId = _tenantContext.TenantId!.Value;
        var numero = await _db.ExpedienteCierres.Where(c => c.ExpedienteId == id).CountAsync(cancellationToken) + 1;
        _db.ExpedienteCierres.Add(new ExpedienteCierre
        {
            TenantId = tenantId, ExpedienteId = id, NumeroCierre = numero,
            HashSha256 = CalcularHashIndice(e), JustificacionReapertura = justificacion.Trim()
        });
        var prev = new { e.Estado };
        e.Estado = EstadoExpediente.Abierto;
        e.FechaCierre = null;
        _audit.Write(actorUserId, "expediente.reabrir", nameof(Expediente), e,
            previousValue: prev, newValue: new { e.Estado, Motivo = justificacion.Trim() }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpedienteResult<int>.Ok(numero);
    }

    private static string CalcularHashIndice(Expediente e)
    {
        var sb = new StringBuilder();
        sb.Append(e.Codigo).Append('|').Append(e.Nombre).Append('|').Append(e.FechaApertura).Append('|')
            .Append(e.TrdAsignacionId).Append('|').Append((int)e.NivelClasificacionId);
        foreach (var m in e.Metadatos.OrderBy(m => m.TrdMetadatoId))
        {
            sb.Append('|').Append(m.TrdMetadatoId).Append('=').Append(m.Valor);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    // ================= Ubicacion fisica (RF12) =================

    public async Task<ExpedienteResult<ExpedienteUbicacionDto>> GetUbicacionAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var existe = await _db.Expedientes.AsNoTracking().AnyAsync(e => e.Id == id && !e.Eliminado, cancellationToken);
        if (!existe) { return ExpedienteResult<ExpedienteUbicacionDto>.NotFound("El expediente no existe."); }

        var filas = await _db.ExpedienteUbicaciones.AsNoTracking()
            .Where(u => u.ExpedienteId == id)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.TopografiaElementoId, u.Fase, u.CreatedAt, u.CreatedBy, u.Observacion })
            .ToListAsync(cancellationToken);

        var codigos = await TopografiaCodigosAsync(cancellationToken);
        var nombres = await ResolverNombresAsync(filas.Select(f => f.CreatedBy), cancellationToken);
        string Ubic(long tid) => codigos.TryGetValue(tid, out var c) ? c : "(ubicacion)";
        string? Por(long? cb) => cb is long v && nombres.TryGetValue(v, out var n) ? n : null;

        var hist = filas.Select(f => new UbicacionHistorialItemDto(
            f.Id, Ubic(f.TopografiaElementoId), f.Fase, Por(f.CreatedBy), f.CreatedAt, f.Observacion)).ToList();
        var act = filas.Count == 0 ? null : new UbicacionActualDto(
            filas[0].TopografiaElementoId, Ubic(filas[0].TopografiaElementoId), filas[0].Fase, Por(filas[0].CreatedBy), filas[0].CreatedAt);

        return ExpedienteResult<ExpedienteUbicacionDto>.Ok(new ExpedienteUbicacionDto(act, hist));
    }

    public async Task<IReadOnlyList<TopografiaOpcionDto>> GetTopografiaOpcionesAsync(CancellationToken cancellationToken = default)
    {
        var codigos = await TopografiaCodigosAsync(cancellationToken);
        var nodos = await _db.TopografiaElementos.AsNoTracking()
            .Where(t => t.Estado != TopografiaEstado.Inactivo)
            .Select(t => new { t.Id, t.Nombre })
            .ToListAsync(cancellationToken);
        return nodos
            .Select(n => new TopografiaOpcionDto(n.Id, codigos.TryGetValue(n.Id, out var c) ? c : "", n.Nombre))
            .OrderBy(o => o.Codigo).ToList();
    }

    public async Task<IReadOnlyList<TopografiaCascadaNodoDto>> GetTopografiaArbolAsync(CancellationToken cancellationToken = default)
    {
        var niveles = await _db.TopografiaNiveles.AsNoTracking()
            .Select(n => new { n.Id, n.NombreNivel, n.Orden }).ToListAsync(cancellationToken);
        var nivPorId = niveles.ToDictionary(n => n.Id);

        var elems = await _db.TopografiaElementos.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId, t.NivelId, t.Nombre, t.Sigla, t.Estado }).ToListAsync(cancellationToken);
        if (elems.Count == 0) { return []; }

        var codigos = await TopografiaCodigosAsync(cancellationToken);
        var idx = elems.ToDictionary(t => t.Id, t => (t.ParentId, t.Estado));
        var conHijos = elems.Where(t => t.ParentId is not null).Select(t => t.ParentId!.Value).ToHashSet();

        return elems.Select(t =>
        {
            var esHoja = !conHijos.Contains(t.Id);
            var motivo = esHoja ? ValidarUbicacionAsignable(t.Id, idx) : null;
            var niv = nivPorId.TryGetValue(t.NivelId, out var n) ? n : null;
            return new TopografiaCascadaNodoDto(
                t.Id, t.ParentId, niv?.Orden ?? 0, niv?.NombreNivel ?? "", t.Nombre, t.Sigla,
                codigos.TryGetValue(t.Id, out var c) ? c : "", t.Estado,
                esHoja, esHoja && string.IsNullOrEmpty(motivo), string.IsNullOrEmpty(motivo) ? null : motivo);
        })
        .OrderBy(n => n.NivelOrden).ThenBy(n => n.Nombre).ToList();
    }

    /// <summary>
    /// RF12 (Mantis #6491, calcado del legacy ValidarUbicacionAsignable): recorre la cadena hoja-&gt;raiz
    /// por ParentId. Ni la hoja ni ningun ancestro puede estar Inactivo; la hoja no puede estar Llena.
    /// Devuelve "" si es asignable, o el motivo del bloqueo.
    /// </summary>
    private static string ValidarUbicacionAsignable(long hojaId, Dictionary<long, (long? ParentId, TopografiaEstado Estado)> idx)
    {
        if (!idx.ContainsKey(hojaId)) { return "La ubicacion seleccionada no existe."; }
        var cur = (long?)hojaId;
        var prof = 0;
        var guard = 0;
        while (cur is long cid && idx.TryGetValue(cid, out var node) && guard++ < 50)
        {
            if (node.Estado == TopografiaEstado.Inactivo)
            {
                return prof == 0 ? "La ubicacion seleccionada esta Inactiva."
                                 : "La ubicacion seleccionada pertenece a una rama Inactiva.";
            }
            if (prof == 0 && node.Estado == TopografiaEstado.Lleno) { return "La ubicacion seleccionada esta Llena."; }
            cur = node.ParentId;
            prof++;
        }
        return "";
    }

    public async Task<ExpedienteResult<bool>> AsignarUbicacionAsync(
        long id, long topografiaElementoId, string? observacion, long actorUserId, CancellationToken cancellationToken = default)
    {
        var e = await _db.Expedientes.FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);
        if (e is null) { return ExpedienteResult<bool>.NotFound("El expediente no existe."); }
        var nodo = await _db.TopografiaElementos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == topografiaElementoId, cancellationToken);
        if (nodo is null) { return ExpedienteResult<bool>.NotFound("La ubicacion topografica no existe."); }

        // RF12: solo se asigna a una HOJA (nivel final de la topografia) que no sea Llena ni Inactiva
        // (ni bajo rama Inactiva). Fail-closed: el servidor re-valida aunque la UI ya filtre.
        var todos = await _db.TopografiaElementos.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId, t.Estado }).ToListAsync(cancellationToken);
        if (todos.Any(t => t.ParentId == topografiaElementoId))
        {
            return ExpedienteResult<bool>.Invalid("Seleccione el nivel final (hoja) de la topografia.");
        }
        var idx = todos.ToDictionary(t => t.Id, t => (t.ParentId, t.Estado));
        var motivo = ValidarUbicacionAsignable(topografiaElementoId, idx);
        if (!string.IsNullOrEmpty(motivo)) { return ExpedienteResult<bool>.Invalid(motivo); }

        var tenantId = _tenantContext.TenantId!.Value;
        var yaTenia = await _db.ExpedienteUbicaciones.AnyAsync(u => u.ExpedienteId == id, cancellationToken);
        _db.ExpedienteUbicaciones.Add(new ExpedienteUbicacion
        {
            TenantId = tenantId, ExpedienteId = id, TopografiaElementoId = topografiaElementoId,
            Fase = e.Fase, Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim()
        });
        e.EstadoUbicacion = yaTenia ? EstadoUbicacionExpediente.Reubicado : EstadoUbicacionExpediente.Ubicado;
        _audit.Write(actorUserId, "expediente.ubicar", nameof(Expediente), e,
            previousValue: null, newValue: new { topografiaElementoId, e.EstadoUbicacion }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpedienteResult<bool>.Ok(true);
    }

    /// <summary>Codigo topografico (siglas concatenadas raiz-&gt;nodo) de cada elemento del tenant.</summary>
    private async Task<Dictionary<long, string>> TopografiaCodigosAsync(CancellationToken cancellationToken)
    {
        var todos = await _db.TopografiaElementos.AsNoTracking()
            .Select(t => new { t.Id, t.ParentId, t.Sigla }).ToListAsync(cancellationToken);
        var porId = todos.ToDictionary(t => t.Id);
        var codigos = new Dictionary<long, string>();
        foreach (var t in todos)
        {
            var partes = new List<string>();
            var cur = (long?)t.Id;
            var guard = 0;
            while (cur is long cid && porId.TryGetValue(cid, out var node) && guard++ < 50)
            {
                partes.Insert(0, node.Sigla);
                cur = node.ParentId;
            }
            codigos[t.Id] = string.Join("-", partes);
        }
        return codigos;
    }

    // ================= Vinculos (RF14) =================

    public async Task<ExpedienteResult<IReadOnlyList<VinculoDto>>> GetVinculosAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var existe = await _db.Expedientes.AsNoTracking().AnyAsync(e => e.Id == id && !e.Eliminado, cancellationToken);
        if (!existe) { return ExpedienteResult<IReadOnlyList<VinculoDto>>.NotFound("El expediente no existe."); }

        var vinculos = await _db.ExpedienteVinculos.AsNoTracking()
            .Where(v => v.Activo && (v.ExpedienteOrigenId == id || v.ExpedienteDestinoId == id))
            .Select(v => new { v.Id, v.ExpedienteOrigenId, v.ExpedienteDestinoId, v.Observacion, v.CreatedAt, v.CreatedBy })
            .ToListAsync(cancellationToken);

        var otrosIds = vinculos.Select(v => v.ExpedienteOrigenId == id ? v.ExpedienteDestinoId : v.ExpedienteOrigenId).Distinct().ToList();
        var otros = await _db.Expedientes.AsNoTracking()
            .Where(e => otrosIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Codigo, e.Nombre, e.Estado })
            .ToListAsync(cancellationToken);
        var otrosMap = otros.ToDictionary(o => o.Id);
        var nombres = await ResolverNombresAsync(vinculos.Select(v => v.CreatedBy), cancellationToken);

        var res = vinculos.Select(v =>
        {
            var otroId = v.ExpedienteOrigenId == id ? v.ExpedienteDestinoId : v.ExpedienteOrigenId;
            otrosMap.TryGetValue(otroId, out var o);
            return new VinculoDto(v.Id, otroId, o?.Codigo ?? "", o?.Nombre ?? "", o?.Estado ?? EstadoExpediente.Abierto,
                v.CreatedBy is long cb && nombres.TryGetValue(cb, out var n) ? n : null, v.CreatedAt, v.Observacion);
        }).ToList();
        return ExpedienteResult<IReadOnlyList<VinculoDto>>.Ok(res);
    }

    public async Task<IReadOnlyList<VinculoBusquedaDto>> BuscarParaVincularAsync(long id, string texto, long actorUserId, CancellationToken cancellationToken = default)
    {
        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var yaVinculados = await _db.ExpedienteVinculos.AsNoTracking()
            .Where(v => v.Activo && (v.ExpedienteOrigenId == id || v.ExpedienteDestinoId == id))
            .Select(v => v.ExpedienteOrigenId == id ? v.ExpedienteDestinoId : v.ExpedienteOrigenId)
            .ToListAsync(cancellationToken);

        var q = _db.Expedientes.AsNoTracking()
            .Where(e => !e.Eliminado && e.Id != id && e.NivelClasificacion!.NivelOrden <= nivelMax
                        && !yaVinculados.Contains(e.Id));
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(e => e.Codigo.ToLower().Contains(t) || e.Nombre.ToLower().Contains(t));
        }
        return await q.OrderByDescending(e => e.CreatedAt).Take(20)
            .Select(e => new VinculoBusquedaDto(e.Id, e.Codigo, e.Nombre, e.Estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpedienteResult<bool>> CrearVinculoAsync(long id, long destinoId, string? observacion, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (id == destinoId) { return ExpedienteResult<bool>.Invalid("Un expediente no se puede vincular consigo mismo."); }
        var origen = await _db.Expedientes.AsNoTracking().AnyAsync(e => e.Id == id && !e.Eliminado, cancellationToken);
        var destino = await _db.Expedientes.AsNoTracking().AnyAsync(e => e.Id == destinoId && !e.Eliminado, cancellationToken);
        if (!origen || !destino) { return ExpedienteResult<bool>.NotFound("El expediente no existe."); }

        var dup = await _db.ExpedienteVinculos.AnyAsync(v => v.Activo &&
            ((v.ExpedienteOrigenId == id && v.ExpedienteDestinoId == destinoId)
             || (v.ExpedienteOrigenId == destinoId && v.ExpedienteDestinoId == id)), cancellationToken);
        if (dup) { return ExpedienteResult<bool>.Conflict("Los expedientes ya estan vinculados."); }

        var tenantId = _tenantContext.TenantId!.Value;
        var vinculo = new ExpedienteVinculo
        {
            TenantId = tenantId, ExpedienteOrigenId = id, ExpedienteDestinoId = destinoId,
            Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim(), Activo = true
        };
        _db.ExpedienteVinculos.Add(vinculo);
        _audit.Write(actorUserId, "expediente.vincular", nameof(ExpedienteVinculo), vinculo,
            previousValue: null, newValue: new { id, destinoId }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpedienteResult<bool>.Ok(true);
    }

    public async Task<ExpedienteResult<bool>> DesvincularAsync(long vinculoId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var v = await _db.ExpedienteVinculos.FirstOrDefaultAsync(x => x.Id == vinculoId && x.Activo, cancellationToken);
        if (v is null) { return ExpedienteResult<bool>.NotFound("El vinculo no existe."); }
        v.Activo = false;
        _audit.Write(actorUserId, "expediente.desvincular", nameof(ExpedienteVinculo), v,
            previousValue: new { Activo = true }, newValue: new { Activo = false }, tenantId: v.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ExpedienteResult<bool>.Ok(true);
    }

    // ================= Trazabilidad (RF09) =================

    public async Task<IReadOnlyList<TrazaItemDto>> GetTrazabilidadAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var logs = await _db.SuperAdminAuditLogs.AsNoTracking()
            .Where(l => l.EntityName == nameof(Expediente) && l.EntityId == id && l.TenantId == tenantId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new { l.ActionName, l.ActorUserId, l.CreatedAt, l.NewValue, l.Reason })
            .ToListAsync(cancellationToken);

        var nombres = await ResolverNombresAsync(logs.Select(l => (long?)l.ActorUserId), cancellationToken);
        return logs.Select(l => new TrazaItemDto(
            l.ActionName,
            nombres.TryGetValue(l.ActorUserId, out var n) ? n : null,
            l.CreatedAt,
            l.Reason ?? l.NewValue)).ToList();
    }
}
