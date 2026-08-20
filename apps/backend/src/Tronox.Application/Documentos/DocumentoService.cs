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
    private readonly IHtmlToPdfConverter _htmlToPdf;

    public DocumentoService(
        IApplicationDbContext db, ITenantContext tenantContext, IObjectStorage storage, IAuditWriter audit,
        IHtmlToPdfConverter htmlToPdf)
    {
        _db = db;
        _tenantContext = tenantContext;
        _storage = storage;
        _audit = audit;
        _htmlToPdf = htmlToPdf;
    }

    // ---- Bandejas ----

    public async Task<IReadOnlyList<ExpedienteDocumentoDto>> ListarPorExpedienteAsync(
        long expedienteId, long actorUserId, CancellationToken cancellationToken = default)
        => await _db.Documentos.AsNoTracking()
            .Include(d => d.TrdTipologia)
            .Where(d => d.ExpedienteId == expedienteId
                        && d.Estado != EstadoDocumento.Anulado
                        && !d.EsVersionHistorica)
            .OrderBy(d => d.OrdenEnExpediente).ThenBy(d => d.FechaIncorporacion)
            .Select(d => new ExpedienteDocumentoDto(
                d.Id, d.OrdenEnExpediente, d.Nombre, d.TrdTipologia!.Nombre, d.Formato, d.TamanoBytes,
                d.PaginaInicio, d.PaginaFin, d.Folios, d.FechaDocumento, d.FechaIncorporacion,
                d.Soporte, d.Estado, d.EstadoFirma, d.TieneBinario))
            .ToListAsync(cancellationToken);

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

    // ---- Editar metadatos (RF04/RF05, calcado de exp_visor_data.ashx op=doc/tipos/campos/guardar) ----

    public async Task<DocumentoResult<DocEditarMetadatosDto>> GetEditarMetadatosAsync(
        long docId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == docId && d.Estado != EstadoDocumento.Anulado, cancellationToken);
        if (doc is null) { return DocumentoResult<DocEditarMetadatosDto>.NotFound("El documento no existe."); }

        // op=tipos: todas las tipologias activas del tenant (no scopeadas a serie, como el legacy).
        var tipos = await _db.TrdTipologias.AsNoTracking()
            .Where(t => !t.IsArchived)
            .OrderBy(t => t.Nombre)
            .Select(t => new DocTipoOpcionDto(t.Id, t.Nombre))
            .ToListAsync(cancellationToken);

        // op=campos del tipo actual (con valores).
        var campos = doc.TrdTipologiaId is long tid
            ? await CargarCamposConValoresAsync(docId, tid, cancellationToken)
            : [];

        return DocumentoResult<DocEditarMetadatosDto>.Ok(
            new DocEditarMetadatosDto(doc.Id, doc.Nombre, doc.FechaDocumento, doc.TrdTipologiaId, tipos, campos));
    }

    public Task<IReadOnlyList<DocMetadatoValorDefDto>> GetMetadatosTipologiaConValoresAsync(
        long docId, long trdTipologiaId, CancellationToken cancellationToken = default)
        => CargarCamposConValoresAsync(docId, trdTipologiaId, cancellationToken);

    /// <summary>Carga los metadatos de una tipologia (contexto Documento) con el valor actual del documento.</summary>
    private async Task<IReadOnlyList<DocMetadatoValorDefDto>> CargarCamposConValoresAsync(
        long docId, long trdTipologiaId, CancellationToken cancellationToken)
    {
        var metas = await _db.TrdMetadatos.AsNoTracking()
            .Where(m => m.TrdTipologiaId == trdTipologiaId && m.Contexto == ContextoMetadato.Documento && !m.IsArchived)
            .OrderBy(m => m.Orden)
            .Select(m => new { m.Id, m.Nombre, m.TipoDato, m.Obligatorio, m.ListaMaestraId })
            .ToListAsync(cancellationToken);
        if (metas.Count == 0) { return []; }

        var valores = await _db.DocumentoMetadatos.AsNoTracking()
            .Where(v => v.DocumentoId == docId)
            .Select(v => new { v.TrdMetadatoId, v.Valor })
            .ToListAsync(cancellationToken);
        var valMap = valores.GroupBy(v => v.TrdMetadatoId).ToDictionary(g => g.Key, g => g.Last().Valor);

        var listaIds = metas.Where(m => m.ListaMaestraId is not null).Select(m => m.ListaMaestraId!.Value).Distinct().ToList();
        var opciones = listaIds.Count == 0
            ? []
            : await _db.ListaOpciones.AsNoTracking()
                .Where(o => listaIds.Contains(o.ListaMaestraId))
                .OrderBy(o => o.Orden)
                .Select(o => new { o.ListaMaestraId, o.Clave, o.Valor })
                .ToListAsync(cancellationToken);

        return metas.Select(m => new DocMetadatoValorDefDto(
            m.Id, m.Nombre, m.TipoDato, m.Obligatorio, m.ListaMaestraId,
            opciones.Where(o => o.ListaMaestraId == m.ListaMaestraId)
                .Select(o => new DocMetadatoOpcionDto(o.Clave, o.Valor)).ToList(),
            valMap.TryGetValue(m.Id, out var v) ? v : null)).ToList();
    }

    public async Task<DocumentoResult<bool>> GuardarMetadatosAsync(
        GuardarMetadatosRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        // op=guardar: nombre + fecha obligatorios (calcado del legacy mdGuardar).
        var errNombre = DocumentoRules.ValidateNombre(request.Nombre);
        if (errNombre is not null) { return DocumentoResult<bool>.Invalid(errNombre); }
        if (request.Fecha is null) { return DocumentoResult<bool>.Invalid("El nombre y la fecha son obligatorios."); }

        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == request.DocId && d.Estado != EstadoDocumento.Anulado, cancellationToken);
        if (doc is null) { return DocumentoResult<bool>.NotFound("El documento no existe."); }

        var tenantId = _tenantContext.TenantId!.Value;
        long? tipoFinal = doc.TrdTipologiaId;
        if (request.TipologiaId is long nuevoTipo && nuevoTipo > 0)
        {
            if (nuevoTipo != doc.TrdTipologiaId)
            {
                var existe = await _db.TrdTipologias.AnyAsync(t => t.Id == nuevoTipo && !t.IsArchived, cancellationToken);
                if (!existe) { return DocumentoResult<bool>.Invalid("El tipo documental no existe."); }
            }
            tipoFinal = nuevoTipo;
        }
        else
        {
            tipoFinal = null; // "-- Sin tipo --"
        }

        var prev = new { doc.Nombre, doc.FechaDocumento, doc.TrdTipologiaId };
        doc.Nombre = request.Nombre.Trim();
        doc.FechaDocumento = request.Fecha;
        doc.TrdTipologiaId = tipoFinal;

        // ReemplazarMetadatosDocumento: borra los existentes y reinserta solo los del tipo final con valor.
        var existentes = await _db.DocumentoMetadatos.Where(v => v.DocumentoId == doc.Id).ToListAsync(cancellationToken);
        if (existentes.Count > 0) { _db.DocumentoMetadatos.RemoveRange(existentes); }
        if (tipoFinal is long ft)
        {
            var validos = await _db.TrdMetadatos.AsNoTracking()
                .Where(m => m.TrdTipologiaId == ft && m.Contexto == ContextoMetadato.Documento && !m.IsArchived)
                .Select(m => m.Id).ToListAsync(cancellationToken);
            foreach (var m in request.Metadatos.Where(m => validos.Contains(m.TrdMetadatoId) && !string.IsNullOrWhiteSpace(m.Valor)))
            {
                _db.DocumentoMetadatos.Add(new DocumentoMetadato
                {
                    TenantId = tenantId, DocumentoId = doc.Id, TrdMetadatoId = m.TrdMetadatoId, Valor = m.Valor!.Trim()
                });
            }
        }

        _audit.Write(actorUserId, "documento.editar_metadatos", nameof(Documento), doc,
            previousValue: prev, newValue: new { doc.Nombre, doc.FechaDocumento, doc.TrdTipologiaId }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<bool>.Ok(true);
    }

    // ---- Editor de texto interno (RF08): calca ctrlEditorTexto ----

    public async Task<DocumentoResult<EditorContenidoDto>> AbrirEditorAsync(
        long docId, long actorUserId, CancellationToken cancellationToken = default)
    {
        // Solo un borrador PROPIO se reabre en el editor (calca GuardarContenidoHtml: ESTADO='Borrador' AND CREATED_BY).
        var doc = await _db.Documentos.AsNoTracking()
            .Where(d => d.Id == docId && d.Estado == EstadoDocumento.Borrador && d.CreatedBy == actorUserId)
            .Select(d => new EditorContenidoDto(d.Id, d.Nombre, d.ContenidoHtml))
            .FirstOrDefaultAsync(cancellationToken);
        return doc is null
            ? DocumentoResult<EditorContenidoDto>.NotFound("El borrador no existe o no es tuyo.")
            : DocumentoResult<EditorContenidoDto>.Ok(doc);
    }

    public async Task<DocumentoResult<long>> GuardarContenidoAsync(
        GuardarContenidoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var up = await UpsertBorradorTextoAsync(request, actorUserId, cancellationToken);
        if (!up.IsOk) { return DocumentoResult<long>.FromError(up); }
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<long>.Ok(up.Value.Doc.Id);
    }

    public async Task<DocumentoResult<long>> GenerarPdfDesdeEditorAsync(
        GuardarContenidoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        // El PDF exige contenido real (HTML sin tags ni &nbsp; no vacio), calca ed8BtnGenerarPdf_Click.
        if (string.IsNullOrWhiteSpace(TextoPlano(request.Html)))
        {
            return DocumentoResult<long>.Invalid("El documento esta vacio. Escribe contenido antes de generar el PDF.");
        }

        // 1) Asegura el borrador de texto (crea o actualiza) y persiste el HTML.
        var up = await UpsertBorradorTextoAsync(request, actorUserId, cancellationToken);
        if (!up.IsOk) { return DocumentoResult<long>.FromError(up); }
        var doc = up.Value.Doc;
        var tenantId = _tenantContext.TenantId!.Value;

        // 2) HTML -> PDF (Chromium). 3) Sube el binario al object storage ANTES de tocar mas la fila.
        byte[] pdf;
        try { pdf = await _htmlToPdf.ConvertirAsync(request.Html ?? "", cancellationToken); }
        catch (Exception ex) { return DocumentoResult<long>.Invalid("No se pudo generar el PDF: " + ex.Message); }
        if (pdf.Length == 0) { return DocumentoResult<long>.Invalid("El PDF generado quedo vacio."); }

        var key = $"{tenantId}/{Guid.NewGuid():N}.pdf";
        using (var ms = new MemoryStream(pdf, writable: false))
        {
            await _storage.PutAsync(key, ms, "application/pdf", cancellationToken);
        }

        // 4) El borrador queda CON binario PDF, pero SIGUE Borrador (decision de diseno): luego se
        //    incorpora a un expediente con el flujo Archivar (RF16). Asi se respeta el invariante
        //    "Archivado = en expediente". El HTML se conserva para poder reabrir y regenerar.
        var nombreArchivo = NombreArchivoPdf(doc.Nombre);
        doc.NombreArchivoOriginal = nombreArchivo;
        doc.Soporte = SoporteDocumento.Electronico;
        doc.Formato = "pdf";
        doc.TieneBinario = true;
        doc.RutaAlmacenamiento = key;
        doc.HashSha256 = DocumentoRules.HashSha256(pdf);
        doc.TamanoBytes = pdf.LongLength;
        doc.Folios = ContarPaginasPdf(pdf);
        doc.OcrEstado = OcrEstadoDocumento.Pendiente;

        _audit.Write(actorUserId, "documento.generar_pdf", nameof(Documento), doc,
            previousValue: null, newValue: new { doc.Formato, doc.HashSha256, doc.TamanoBytes }, tenantId: tenantId);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(key, cancellationToken); // no dejar binario sin fila
            throw;
        }
        return DocumentoResult<long>.Ok(doc.Id);
    }

    /// <summary>
    /// Crea (DocId null/0) o actualiza (DocId &gt; 0, solo Borrador propio) un borrador nacido del editor de
    /// texto, fijando Nombre + ContenidoHtml. Devuelve la entidad rastreada; NO llama SaveChanges (el caller
    /// decide). Calca CrearBorradorTexto / GuardarContenidoHtml del legacy.
    /// </summary>
    private async Task<DocumentoResult<(Documento Doc, bool EsNuevo)>> UpsertBorradorTextoAsync(
        GuardarContenidoRequest request, long actorUserId, CancellationToken cancellationToken)
    {
        var nombre = string.IsNullOrWhiteSpace(request.Nombre) ? "Documento_sin_nombre" : request.Nombre.Trim();
        var html = request.Html ?? "";
        var tenantId = _tenantContext.TenantId!.Value;

        if (request.DocId is long id && id > 0)
        {
            var doc = await _db.Documentos.FirstOrDefaultAsync(
                d => d.Id == id && d.Estado == EstadoDocumento.Borrador && d.CreatedBy == actorUserId, cancellationToken);
            if (doc is null)
            {
                return DocumentoResult<(Documento, bool)>.Invalid(
                    "No se pudo guardar: el documento ya no es un borrador tuyo (puede estar archivado o pertenecer a otro usuario).");
            }
            doc.Nombre = nombre;
            doc.ContenidoHtml = html;
            _audit.Write(actorUserId, "documento.editar_contenido", nameof(Documento), doc,
                previousValue: null, newValue: new { doc.Nombre, Longitud = html.Length }, tenantId: tenantId);
            return DocumentoResult<(Documento, bool)>.Ok((doc, false));
        }

        var nuevo = new Documento
        {
            TenantId = tenantId,
            Nombre = nombre,
            Soporte = SoporteDocumento.Electronico,
            Estado = EstadoDocumento.Borrador,
            EstadoFirma = EstadoFirmaDocumento.SinFirma,
            FechaDocumento = DateOnly.FromDateTime(DateTime.UtcNow),
            TieneBinario = false,
            OcrEstado = OcrEstadoDocumento.NoAplica,
            ContenidoHtml = html
        };
        _db.Documentos.Add(nuevo);
        _audit.Write(actorUserId, "documento.crear_borrador_texto", nameof(Documento), nuevo,
            previousValue: null, newValue: new { nuevo.Nombre, Origen = "editor_texto" }, tenantId: tenantId);
        return DocumentoResult<(Documento, bool)>.Ok((nuevo, true));
    }

    /// <summary>HTML sin etiquetas ni &nbsp; y recortado, para validar que el editor tenga contenido real.</summary>
    private static string TextoPlano(string? html)
        => System.Text.RegularExpressions.Regex.Replace(html ?? "", "<[^>]+>", "")
            .Replace("&nbsp;", "").Trim();

    /// <summary>Normaliza el nombre a un archivo .pdf (sin tildes, espacios a guion bajo), calca NormalizarNombre.</summary>
    private static string NombreArchivoPdf(string nombre)
    {
        var sinTilde = new string(nombre.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray()).Normalize(System.Text.NormalizationForm.FormC);
        var limpio = System.Text.RegularExpressions.Regex.Replace(sinTilde.Trim(), @"\s+", "_");
        limpio = System.Text.RegularExpressions.Regex.Replace(limpio, @"[^A-Za-z0-9_\-.]", "");
        if (string.IsNullOrWhiteSpace(limpio)) { limpio = "Documento_sin_nombre"; }
        return limpio + ".pdf";
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

    // ---- Carga de Archivos: incorporar directo en el expediente (Flujo A, RQ04) ----

    public async Task<DocumentoResult<bool>> IncorporarEnExpedienteAsync(
        IncorporarDocRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var errNombre = DocumentoRules.ValidateNombre(request.Nombre);
        if (errNombre is not null) { return DocumentoResult<bool>.Invalid(errNombre); }

        var nivelMax = await ResolveNivelMaxOrdenAsync(actorUserId, cancellationToken);
        var expediente = await _db.Expedientes.AsNoTracking()
            .Include(e => e.NivelClasificacion)
            .FirstOrDefaultAsync(e => e.Id == request.ExpedienteId && !e.Eliminado, cancellationToken);
        if (expediente is null || expediente.NivelClasificacion!.NivelOrden > nivelMax)
        { return DocumentoResult<bool>.NotFound("El expediente no existe."); }
        if (expediente.Estado != EstadoExpediente.Abierto)
        { return DocumentoResult<bool>.Invalid("El expediente esta Cerrado; no admite nuevos documentos."); }

        var tenantId = _tenantContext.TenantId!.Value;

        // Tipologia OPCIONAL: si viene, debe pertenecer a la serie del expediente; carga sus metadatos.
        List<(long Id, string Nombre, bool Obligatorio)> defs = [];
        if (request.TrdTipologiaId is long tipId)
        {
            var tipologia = await _db.TrdTipologias.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tipId && t.TrdAsignacionId == expediente.TrdAsignacionId && !t.IsArchived, cancellationToken);
            if (tipologia is null) { return DocumentoResult<bool>.Invalid("La tipologia no pertenece a la serie del expediente."); }
            defs = (await _db.TrdMetadatos.AsNoTracking()
                .Where(m => m.TrdTipologiaId == tipId && m.Contexto == ContextoMetadato.Documento && !m.IsArchived)
                .Select(m => new { m.Id, m.Nombre, m.Obligatorio }).ToListAsync(cancellationToken))
                .Select(x => (x.Id, x.Nombre, x.Obligatorio)).ToList();
        }
        var valores = request.Metadatos.GroupBy(m => m.TrdMetadatoId).ToDictionary(g => g.Key, g => g.Last().Valor);
        var errMeta = DocumentoRules.ValidateMetadatosObligatorios(defs, valores);
        if (errMeta is not null) { return DocumentoResult<bool>.Invalid(errMeta); }

        // Binario (si no es fisico): sube al object storage AL CONFIRMAR.
        string? key = null, hash = null, formato = null, contentType = null;
        long? tamano = null;
        var soporte = SoporteDocumento.Fisico;
        var tieneBinario = false;
        var ocr = OcrEstadoDocumento.NoAplica;
        var folios = Math.Max(1, request.Folios);
        if (!request.EsFisico)
        {
            if (request.Contenido is null || request.Contenido.Length == 0 || string.IsNullOrWhiteSpace(request.NombreArchivo))
            { return DocumentoResult<bool>.Invalid("Falta el archivo a subir."); }
            var errBin = DocumentoRules.ValidateBinario(request.NombreArchivo, request.Contenido.LongLength);
            if (errBin is not null) { return DocumentoResult<bool>.Invalid(errBin); }
            var ext = DocumentoRules.Extension(request.NombreArchivo);
            key = $"{tenantId}/{Guid.NewGuid():N}.{ext}";
            hash = DocumentoRules.HashSha256(request.Contenido);
            formato = DocumentoRules.Formato(request.NombreArchivo);
            tamano = request.Contenido.LongLength;
            contentType = DocumentoRules.ContentType(request.NombreArchivo);
            soporte = SoporteDocumento.Electronico;
            tieneBinario = true;
            ocr = DocumentoRules.OcrInicial(request.NombreArchivo);
            folios = ext == "pdf" ? ContarPaginasPdf(request.Contenido) : EsImagen(ext) ? 1 : Math.Max(1, request.Folios);
            using var ms = new MemoryStream(request.Contenido, writable: false);
            await _storage.PutAsync(key, ms, contentType, cancellationToken);
        }

        // Foliacion continua por expediente (inmutable): orden de incorporacion + rango de folios.
        var maxOrden = await _db.Documentos.AsNoTracking()
            .Where(d => d.ExpedienteId == expediente.Id && d.Estado == EstadoDocumento.Archivado)
            .Select(d => (int?)d.OrdenEnExpediente).MaxAsync(cancellationToken) ?? 0;
        var maxFolio = await _db.Documentos.AsNoTracking()
            .Where(d => d.ExpedienteId == expediente.Id && d.Estado == EstadoDocumento.Archivado)
            .Select(d => (int?)d.PaginaFin).MaxAsync(cancellationToken) ?? 0;
        var pini = maxFolio + 1;

        var doc = new Documento
        {
            TenantId = tenantId,
            Nombre = request.Nombre.Trim(),
            NombreArchivoOriginal = request.NombreArchivo,
            Soporte = soporte,
            Estado = EstadoDocumento.Archivado,
            EstadoFirma = EstadoFirmaDocumento.SinFirma,
            ExpedienteId = expediente.Id,
            TrdAsignacionId = expediente.TrdAsignacionId,          // DAT-03: hereda y congela
            TrdTipologiaId = request.TrdTipologiaId,
            NivelClasificacionId = expediente.NivelClasificacionId, // heredado del expediente (RF13)
            FechaDocumento = request.FechaDocumento,
            FechaIncorporacion = DateTime.UtcNow,
            OrdenEnExpediente = maxOrden + 1,
            PaginaInicio = pini,
            PaginaFin = pini + folios - 1,
            Folios = folios,
            Formato = formato,
            TamanoBytes = tamano,
            HashSha256 = hash,
            TieneBinario = tieneBinario,
            RutaAlmacenamiento = key,
            OcrEstado = ocr
        };
        var defIds = defs.Select(x => x.Id).ToHashSet();
        foreach (var input in valores)
        {
            if (!defIds.Contains(input.Key) || string.IsNullOrWhiteSpace(input.Value)) { continue; }
            doc.Metadatos.Add(new DocumentoMetadato { TenantId = tenantId, TrdMetadatoId = input.Key, Valor = input.Value!.Trim() });
        }
        _db.Documentos.Add(doc);
        _audit.Write(actorUserId, "documento.incorporar", nameof(Documento), doc,
            previousValue: null,
            newValue: new { doc.Nombre, ExpedienteId = expediente.Id, doc.OrdenEnExpediente, doc.HashSha256 }, tenantId: tenantId);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch { if (key is not null) { await _storage.DeleteAsync(key, cancellationToken); } throw; }
        return DocumentoResult<bool>.Ok(true);
    }

    private static bool EsImagen(string ext)
        => ext is "jpg" or "jpeg" or "png" or "tif" or "tiff" or "gif" or "bmp" or "webp";

    /// <summary>Cuenta paginas de un PDF de forma heuristica (marcadores /Type /Page). Fallback 1.</summary>
    private static int ContarPaginasPdf(byte[] contenido)
    {
        try
        {
            var txt = System.Text.Encoding.Latin1.GetString(contenido);
            var count = System.Text.RegularExpressions.Regex.Matches(txt, @"/Type\s*/Page[^s]").Count;
            return count > 0 ? count : 1;
        }
        catch { return 1; }
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
