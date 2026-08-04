using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Documentos;

/// <summary>
/// "Mis Documentos" (RQ04 - RF15/RF16). El binario vive en object storage (ADR-009), nunca en base de
/// datos. Los borradores son PRIVADOS del creador (filtro por CreatedBy). Al archivar, el documento
/// hereda la asignacion de TRD del expediente (DAT-03) y su clasificacion (solo elevar). El unico
/// borrado fisico del sistema es el borrador nunca archivado.
/// </summary>
public sealed class DocumentoService : IDocumentoService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IObjectStorage _storage;
    private readonly IAuditWriter _audit;

    public DocumentoService(
        IApplicationDbContext db, ITenantContext tenantContext, IObjectStorage storage, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _storage = storage;
        _audit = audit;
    }

    // ---- Bandejas ----

    public async Task<IReadOnlyList<BorradorItemDto>> ListarBorradoresAsync(
        long actorUserId, string? texto = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Documentos.AsNoTracking()
            .Where(d => d.Estado == EstadoDocumento.Borrador && d.CreatedBy == actorUserId);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            query = query.Where(d => d.Nombre.ToLower().Contains(t));
        }
        return await query.OrderByDescending(d => d.CreatedAt)
            .Select(d => new BorradorItemDto(
                d.Id, d.Nombre, d.Formato, d.Soporte, d.CreatedAt, d.Folios, d.TamanoBytes, d.EstadoFirma, d.TieneBinario))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchivadoItemDto>> ListarArchivadosPorMiAsync(
        long actorUserId, string? texto = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Documentos.AsNoTracking()
            .Include(d => d.Expediente)
            .Include(d => d.TrdTipologia)
            .Include(d => d.NivelClasificacion)
            .Where(d => d.Estado == EstadoDocumento.Archivado && d.CreatedBy == actorUserId);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            query = query.Where(d => d.Nombre.ToLower().Contains(t));
        }
        return await query.OrderByDescending(d => d.FechaIncorporacion)
            .Select(d => new ArchivadoItemDto(
                d.Id, d.Nombre, d.TrdTipologia!.Nombre,
                d.Expediente!.Codigo, d.Expediente!.Nombre,
                d.FechaIncorporacion, d.OrdenEnExpediente, d.Folios, d.TamanoBytes,
                d.NivelClasificacion!.Nombre, d.EstadoFirma, d.TieneBinario))
            .ToListAsync(cancellationToken);
    }

    // ---- Detalle ----

    public async Task<DocumentoResult<DocumentoDetalleDto>> GetDetalleAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var d = await LoadForReadAsync(id, actorUserId, cancellationToken);
        if (d is null) { return DocumentoResult<DocumentoDetalleDto>.NotFound("El documento no existe."); }
        return DocumentoResult<DocumentoDetalleDto>.Ok(await BuildDetalleAsync(d, cancellationToken));
    }

    // ---- Crear borrador ----

    public async Task<DocumentoResult<DocumentoDetalleDto>> CrearBorradorBinarioAsync(
        string nombre, DateOnly? fechaDocumento, byte[] contenido, string nombreArchivo,
        long actorUserId, CancellationToken cancellationToken = default)
    {
        var errNombre = DocumentoRules.ValidateNombre(nombre);
        if (errNombre is not null) { return DocumentoResult<DocumentoDetalleDto>.Invalid(errNombre); }
        var errBin = DocumentoRules.ValidateBinario(nombreArchivo, contenido.LongLength);
        if (errBin is not null) { return DocumentoResult<DocumentoDetalleDto>.Invalid(errBin); }

        var tenantId = _tenantContext.TenantId!.Value;
        var ext = DocumentoRules.Extension(nombreArchivo);
        var key = $"{tenantId}/{Guid.NewGuid():N}.{ext}";
        var hash = DocumentoRules.HashSha256(contenido);
        var contentType = DocumentoRules.ContentType(nombreArchivo);

        // Sube al object storage ANTES de tocar la base: si falla, no queda fila huerfana.
        using (var ms = new MemoryStream(contenido, writable: false))
        {
            await _storage.PutAsync(key, ms, contentType, cancellationToken);
        }

        var doc = new Documento
        {
            TenantId = tenantId,
            Nombre = nombre.Trim(),
            NombreArchivoOriginal = nombreArchivo,
            Soporte = SoporteDocumento.Electronico,
            Estado = EstadoDocumento.Borrador,
            EstadoFirma = EstadoFirmaDocumento.SinFirma,
            FechaDocumento = fechaDocumento,
            Formato = DocumentoRules.Formato(nombreArchivo),
            TamanoBytes = contenido.LongLength,
            HashSha256 = hash,
            TieneBinario = true,
            RutaAlmacenamiento = key,
            OcrEstado = DocumentoRules.OcrInicial(nombreArchivo)
        };
        _db.Documentos.Add(doc);
        _audit.Write(actorUserId, "documento.crear_borrador", nameof(Documento), doc,
            previousValue: null, newValue: new { doc.Nombre, doc.Formato, doc.HashSha256 }, tenantId: tenantId);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Compensa el blob si el guardado falla (no dejar binario sin fila).
            await _storage.DeleteAsync(key, cancellationToken);
            throw;
        }
        return DocumentoResult<DocumentoDetalleDto>.Ok(await BuildDetalleAsync(doc, cancellationToken));
    }

    public async Task<DocumentoResult<DocumentoDetalleDto>> CrearBorradorFisicoAsync(
        CrearBorradorFisicoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var errNombre = DocumentoRules.ValidateNombre(request.Nombre);
        if (errNombre is not null) { return DocumentoResult<DocumentoDetalleDto>.Invalid(errNombre); }

        var doc = new Documento
        {
            TenantId = _tenantContext.TenantId!.Value,
            Nombre = request.Nombre.Trim(),
            Soporte = SoporteDocumento.Fisico,
            Estado = EstadoDocumento.Borrador,
            EstadoFirma = EstadoFirmaDocumento.SinFirma,
            FechaDocumento = request.FechaDocumento,
            TieneBinario = false,
            OcrEstado = OcrEstadoDocumento.NoAplica
        };
        _db.Documentos.Add(doc);
        _audit.Write(actorUserId, "documento.crear_fisico", nameof(Documento), doc,
            previousValue: null, newValue: new { doc.Nombre }, tenantId: doc.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<DocumentoDetalleDto>.Ok(await BuildDetalleAsync(doc, cancellationToken));
    }

    public async Task<DocumentoResult<DocumentoDetalleDto>> EditarBorradorAsync(
        long id, string nombre, DateOnly? fechaDocumento, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doc is null || doc.CreatedBy != actorUserId) { return DocumentoResult<DocumentoDetalleDto>.NotFound("El documento no existe."); }
        if (doc.Estado != EstadoDocumento.Borrador) { return DocumentoResult<DocumentoDetalleDto>.Invalid("Solo se editan borradores."); }
        var errNombre = DocumentoRules.ValidateNombre(nombre);
        if (errNombre is not null) { return DocumentoResult<DocumentoDetalleDto>.Invalid(errNombre); }

        doc.Nombre = nombre.Trim();
        doc.FechaDocumento = fechaDocumento;
        _audit.Write(actorUserId, "documento.editar_borrador", nameof(Documento), doc,
            previousValue: null, newValue: new { doc.Nombre }, tenantId: doc.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<DocumentoDetalleDto>.Ok(await BuildDetalleAsync(doc, cancellationToken));
    }

    // ---- Descargar ----

    public async Task<DocumentoResult<DocumentoDescargaDto>> DescargarAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var d = await LoadForReadAsync(id, actorUserId, cancellationToken);
        if (d is null) { return DocumentoResult<DocumentoDescargaDto>.NotFound("El documento no existe."); }
        if (!d.TieneBinario || string.IsNullOrEmpty(d.RutaAlmacenamiento))
        {
            return DocumentoResult<DocumentoDescargaDto>.Invalid("El documento no tiene binario (es fisico).");
        }
        var stream = await _storage.GetAsync(d.RutaAlmacenamiento, cancellationToken);
        if (stream is null) { return DocumentoResult<DocumentoDescargaDto>.NotFound("El binario no esta disponible."); }
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        await stream.DisposeAsync();
        var nombreArchivo = d.NombreArchivoOriginal ?? $"{d.Nombre}.{(d.Formato ?? "bin").ToLowerInvariant()}";
        var contentType = DocumentoRules.ContentType(nombreArchivo);
        return DocumentoResult<DocumentoDescargaDto>.Ok(new DocumentoDescargaDto(ms.ToArray(), nombreArchivo, contentType));
    }

    // ---- Eliminar borrador (unico borrado fisico) ----

    public async Task<DocumentoResult<bool>> EliminarBorradorAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (doc is null || doc.CreatedBy != actorUserId) { return DocumentoResult<bool>.NotFound("El documento no existe."); }
        if (doc.Estado != EstadoDocumento.Borrador || doc.FechaIncorporacion is not null)
        {
            return DocumentoResult<bool>.Invalid("Solo se eliminan borradores nunca archivados.");
        }

        var key = doc.RutaAlmacenamiento;
        _db.Documentos.Remove(doc);
        _audit.Write(actorUserId, "documento.eliminar_borrador", nameof(Documento), doc,
            previousValue: new { doc.Nombre }, newValue: null, tenantId: doc.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(key)) { await _storage.DeleteAsync(key, cancellationToken); }
        return DocumentoResult<bool>.Ok(true);
    }

    // ---- Archivar (RF16) ----

    public async Task<IReadOnlyList<ExpedienteDestinoDto>> GetExpedientesDestinoAsync(
        long actorUserId, string? texto = null, CancellationToken cancellationToken = default)
    {
        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var query = _db.Expedientes.AsNoTracking()
            .Include(e => e.NivelClasificacion)
            .Where(e => !e.Eliminado && e.Estado == EstadoExpediente.Abierto)
            .Where(e => e.NivelClasificacion!.NivelOrden <= nivelMax);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            query = query.Where(e => e.Codigo.ToLower().Contains(t) || e.Nombre.ToLower().Contains(t));
        }
        return await query.OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExpedienteDestinoDto(
                e.Id, e.Codigo, e.Nombre, e.TrdAsignacionId, e.NivelClasificacion!.NivelOrden))
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TipologiaOpcionDto>> GetTipologiasExpedienteAsync(
        long expedienteId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var asignacionId = await _db.Expedientes.AsNoTracking()
            .Where(e => e.Id == expedienteId && !e.Eliminado)
            .Select(e => (long?)e.TrdAsignacionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (asignacionId is null) { return []; }
        return await _db.TrdTipologias.AsNoTracking()
            .Where(t => t.TrdAsignacionId == asignacionId.Value && !t.IsArchived)
            .OrderBy(t => t.Nombre)
            .Select(t => new TipologiaOpcionDto(t.Id, t.Nombre, t.Soporte))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocMetadatoDefDto>> GetMetadatosTipologiaAsync(
        long trdTipologiaId, CancellationToken cancellationToken = default)
    {
        var metas = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdTipologiaId == trdTipologiaId && m.Contexto == ContextoMetadato.Documento && !m.IsArchived)
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

        return metas.Select(m => new DocMetadatoDefDto(
            m.Id, m.Nombre, m.TipoDato, m.Obligatorio, m.ListaMaestraId,
            opciones.Where(o => o.ListaMaestraId == m.ListaMaestraId)
                .Select(o => new DocMetadatoOpcionDto(o.Clave, o.Valor)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<NivelDocOpcionDto>> GetNivelesAsync(CancellationToken cancellationToken = default)
        => await _db.NivelesClasificacion.AsNoTracking()
            .Where(n => n.Activo)
            .OrderBy(n => n.NivelOrden)
            .Select(n => new NivelDocOpcionDto(n.Id, n.Nombre, n.NivelOrden))
            .ToListAsync(cancellationToken);

    public async Task<DocumentoResult<DocumentoDetalleDto>> ArchivarAsync(
        ArchivarRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.Include(d => d.Metadatos)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentoId, cancellationToken);
        if (doc is null || doc.CreatedBy != actorUserId) { return DocumentoResult<DocumentoDetalleDto>.NotFound("El documento no existe."); }
        if (doc.Estado != EstadoDocumento.Borrador) { return DocumentoResult<DocumentoDetalleDto>.Invalid("Solo se archivan borradores."); }

        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var expediente = await _db.Expedientes.AsNoTracking()
            .Include(e => e.NivelClasificacion)
            .FirstOrDefaultAsync(e => e.Id == request.ExpedienteId && !e.Eliminado, cancellationToken);
        if (expediente is null || expediente.NivelClasificacion!.NivelOrden > nivelMax)
        {
            return DocumentoResult<DocumentoDetalleDto>.NotFound("El expediente no existe.");
        }
        if (expediente.Estado != EstadoExpediente.Abierto)
        {
            return DocumentoResult<DocumentoDetalleDto>.Invalid("El expediente esta Cerrado; no admite nuevos documentos.");
        }

        var tipologia = await _db.TrdTipologias.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TrdTipologiaId
                                      && t.TrdAsignacionId == expediente.TrdAsignacionId && !t.IsArchived, cancellationToken);
        if (tipologia is null) { return DocumentoResult<DocumentoDetalleDto>.Invalid("La tipologia no pertenece a la serie del expediente."); }

        var nivelElegido = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.NivelClasificacionId, cancellationToken);
        if (nivelElegido is null) { return DocumentoResult<DocumentoDetalleDto>.NotFound("El nivel de clasificacion no existe."); }
        if (!DocumentoRules.PuedeElevar(expediente.NivelClasificacion!.NivelOrden, nivelElegido.NivelOrden))
        {
            return DocumentoResult<DocumentoDetalleDto>.Invalid(DocumentoRules.MensajeNoBajarClasificacion);
        }

        var defs = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdTipologiaId == tipologia.Id && m.Contexto == ContextoMetadato.Documento && !m.IsArchived)
            .Select(m => new { m.Id, m.Nombre, m.Obligatorio })
            .ToListAsync(cancellationToken);
        var valores = request.Metadatos.GroupBy(m => m.TrdMetadatoId).ToDictionary(g => g.Key, g => g.Last().Valor);
        var errMeta = DocumentoRules.ValidateMetadatosObligatorios(
            defs.Select(x => (x.Id, x.Nombre, x.Obligatorio)), valores);
        if (errMeta is not null) { return DocumentoResult<DocumentoDetalleDto>.Invalid(errMeta); }

        // Foliacion: consecutivo por expediente segun orden de incorporacion (inmutable).
        var maxOrden = await _db.Documentos.AsNoTracking()
            .Where(d => d.ExpedienteId == expediente.Id && d.Estado == EstadoDocumento.Archivado)
            .Select(d => (int?)d.OrdenEnExpediente).MaxAsync(cancellationToken) ?? 0;

        doc.Estado = EstadoDocumento.Archivado;
        doc.ExpedienteId = expediente.Id;
        doc.TrdAsignacionId = expediente.TrdAsignacionId;   // DAT-03: hereda y congela
        doc.TrdTipologiaId = tipologia.Id;
        doc.NivelClasificacionId = nivelElegido.Id;
        doc.FechaIncorporacion = DateTime.UtcNow;
        doc.OrdenEnExpediente = maxOrden + 1;
        if (request.FechaDocumento is DateOnly fd) { doc.FechaDocumento = fd; }
        if (doc.Folios is null && doc.TieneBinario) { doc.Folios = 1; }

        var defIds = defs.Select(x => x.Id).ToHashSet();
        doc.Metadatos.Clear();
        foreach (var input in valores)
        {
            if (!defIds.Contains(input.Key) || string.IsNullOrWhiteSpace(input.Value)) { continue; }
            doc.Metadatos.Add(new DocumentoMetadato
            {
                TenantId = doc.TenantId,
                TrdMetadatoId = input.Key,
                Valor = input.Value!.Trim()
            });
        }

        _audit.Write(actorUserId, "documento.archivar", nameof(Documento), doc,
            previousValue: new { Estado = "Borrador" },
            newValue: new { Estado = "Archivado", ExpedienteId = expediente.Id, doc.OrdenEnExpediente },
            tenantId: doc.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<DocumentoDetalleDto>.Ok(await BuildDetalleAsync(doc, cancellationToken));
    }

    // ---- Helpers ----

    /// <summary>
    /// Carga un documento para LECTURA respetando el acceso: un borrador solo lo ve su creador; un
    /// documento archivado/anulado respeta la clasificacion fail-closed del usuario.
    /// </summary>
    private async Task<Documento?> LoadForReadAsync(long id, long actorUserId, CancellationToken cancellationToken)
    {
        var d = await _db.Documentos.AsNoTracking()
            .Include(x => x.Expediente)
            .Include(x => x.TrdTipologia)
            .Include(x => x.NivelClasificacion)
            .Include(x => x.Metadatos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d is null) { return null; }
        if (d.Estado == EstadoDocumento.Borrador)
        {
            return d.CreatedBy == actorUserId ? d : null;
        }
        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var nivelOrden = d.NivelClasificacion?.NivelOrden ?? 0;
        return nivelOrden <= nivelMax ? d : null;
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

    private async Task<DocumentoDetalleDto> BuildDetalleAsync(Documento d, CancellationToken cancellationToken)
    {
        // Recarga proyecciones relacionadas si vienen de una entidad recien creada/modificada.
        var exp = d.ExpedienteId is long eid
            ? await _db.Expedientes.AsNoTracking().Where(e => e.Id == eid)
                .Select(e => new { e.Codigo, e.Nombre }).FirstOrDefaultAsync(cancellationToken)
            : null;
        var tipNombre = d.TrdTipologiaId is long tid
            ? await _db.TrdTipologias.AsNoTracking().Where(t => t.Id == tid).Select(t => t.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;
        var nivelNombre = d.NivelClasificacionId is long nid
            ? await _db.NivelesClasificacion.AsNoTracking().Where(n => n.Id == nid).Select(n => n.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;

        IReadOnlyList<DocMetadatoValorDto> metas = [];
        if (d.TrdTipologiaId is long tipId)
        {
            var defs = await _db.TrdMetadatos.AsNoTracking()
                .Where(m => m.TrdTipologiaId == tipId && m.Contexto == ContextoMetadato.Documento)
                .OrderBy(m => m.Orden)
                .Select(m => new { m.Id, m.Nombre, m.TipoDato })
                .ToListAsync(cancellationToken);
            var valores = await _db.DocumentoMetadatos.AsNoTracking()
                .Where(m => m.DocumentoId == d.Id)
                .ToDictionaryAsync(m => m.TrdMetadatoId, m => m.Valor, cancellationToken);
            metas = defs.Select(x => new DocMetadatoValorDto(
                x.Id, x.Nombre, x.TipoDato, valores.TryGetValue(x.Id, out var v) ? v : null)).ToList();
        }

        return new DocumentoDetalleDto(
            d.Id, d.Nombre, d.NombreArchivoOriginal, d.Estado, d.Soporte, d.EstadoFirma,
            d.Formato, d.TamanoBytes, d.Folios, d.HashSha256, d.TieneBinario,
            d.FechaDocumento, d.FechaIncorporacion,
            exp?.Codigo, exp?.Nombre, tipNombre, nivelNombre, metas);
    }
}
