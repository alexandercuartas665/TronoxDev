using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Documentos;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Firmas;

/// <summary>
/// Firma electronica (RQ05 - RF05). Slice 1: firma directa (auto-firma) + contrato estable de
/// solicitud/consulta/cancelacion. Calca FirmaRepository/FirmaDirectaHelper del legacy. La firma es una
/// dimension independiente del archivado del documento. Ver ADR-017 para lo diferido (stepper OTP,
/// solicitud-cumplimiento, PDF/A, QR, sellado XMP, certificado, masiva, plantillas, hora legal NTP).
/// </summary>
public sealed class FirmaService : IFirmaService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IObjectStorage _storage;
    private readonly IAuditWriter _audit;
    private readonly IPdfSignatureStamper _stamper;

    public FirmaService(
        IApplicationDbContext db, ITenantContext tenant, IObjectStorage storage,
        IAuditWriter audit, IPdfSignatureStamper stamper)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
        _audit = audit;
        _stamper = stamper;
    }

    // ---- Contrato estable RQ05 ----

    public async Task<DocumentoResult<long>> SolicitarFirmaAsync(
        SolicitarFirmaRequest r, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.FirstOrDefaultAsync(
            d => d.Id == r.DocId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<long>.NotFound("El documento no existe."); }
        if (doc.CreatedBy != actorUserId) { return DocumentoResult<long>.Invalid("Solo el creador puede solicitar la firma."); }
        if (!doc.TieneBinario) { return DocumentoResult<long>.Invalid("Solo se puede solicitar firma de un documento con archivo."); }
        if (doc.EstadoFirma == EstadoFirmaDocumento.Firmado) { return DocumentoResult<long>.Conflict("El documento ya esta firmado."); }

        var yaPendiente = await _db.Firmas.AnyAsync(
            f => f.DocumentoId == r.DocId && f.FirmanteUserId == r.FirmanteUserId && f.Estado == EstadoFirma.Pendiente, cancellationToken);
        if (yaPendiente) { return DocumentoResult<long>.Conflict("Ya hay una solicitud de firma pendiente para ese firmante."); }

        var snap = await ResolverSnapshotAsync(r.FirmanteUserId, cancellationToken);
        if (snap is null) { return DocumentoResult<long>.NotFound("El firmante no existe."); }

        var firma = new Firma
        {
            TenantId = _tenant.TenantId!.Value,
            DocumentoId = r.DocId,
            FirmanteUserId = r.FirmanteUserId,
            NombreFirmante = snap.Nombre,
            CargoFirmante = snap.Cargo,
            DependenciaFirmante = snap.Dependencia,
            TipoFirma = r.TipoFirma,
            Estado = EstadoFirma.Pendiente,
            OtpRequerido = r.OtpRequerido,
            SolicitadoPor = actorUserId,
            Prioridad = r.Prioridad,
            FechaLimite = r.FechaLimite,
            Instrucciones = string.IsNullOrWhiteSpace(r.Instrucciones) ? null : r.Instrucciones.Trim(),
            Tag = string.IsNullOrWhiteSpace(r.Tag) ? null : r.Tag.Trim()
        };
        _db.Firmas.Add(firma);
        doc.EstadoFirma = EstadoFirmaDocumento.Pendiente;
        _audit.Write(actorUserId, "documento.solicitar_firma", nameof(Firma), doc,
            previousValue: null, newValue: new { firma.FirmanteUserId, firma.TipoFirma, firma.OtpRequerido },
            tenantId: _tenant.TenantId!.Value);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<long>.Ok(firma.Id);
    }

    public async Task<DocumentoResult<FirmaEstadoDto>> ConsultarEstadoFirmaAsync(
        long docId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == docId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<FirmaEstadoDto>.NotFound("El documento no existe."); }

        var firmas = await _db.Firmas.AsNoTracking()
            .Where(f => f.DocumentoId == docId)
            .OrderByDescending(f => f.Id)
            .Select(f => new FirmaItemDto(
                f.Id, f.FirmanteUserId, f.NombreFirmante, f.CargoFirmante, f.DependenciaFirmante,
                f.TipoFirma, f.Estado, f.TimestampFirma, f.OtpRequerido, f.HashDocumento))
            .ToListAsync(cancellationToken);

        return DocumentoResult<FirmaEstadoDto>.Ok(new FirmaEstadoDto(docId, doc.EstadoFirma, firmas));
    }

    public async Task<DocumentoResult<bool>> CancelarFirmaAsync(
        long firmaId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var firma = await _db.Firmas.FirstOrDefaultAsync(f => f.Id == firmaId, cancellationToken);
        if (firma is null) { return DocumentoResult<bool>.NotFound("La firma no existe."); }
        if (firma.Estado != EstadoFirma.Pendiente) { return DocumentoResult<bool>.Invalid("Solo se puede cancelar una solicitud pendiente."); }
        if (firma.SolicitadoPor != actorUserId && firma.FirmanteUserId != actorUserId)
        {
            return DocumentoResult<bool>.Invalid("Solo el solicitante o el firmante pueden cancelar la solicitud.");
        }

        firma.Estado = EstadoFirma.Cancelado;

        // Si no quedan mas solicitudes pendientes, la dimension del documento vuelve a "sin firma".
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == firma.DocumentoId, cancellationToken);
        if (doc is not null && doc.EstadoFirma == EstadoFirmaDocumento.Pendiente)
        {
            var quedanPend = await _db.Firmas.AnyAsync(
                f => f.DocumentoId == firma.DocumentoId && f.Id != firmaId && f.Estado == EstadoFirma.Pendiente, cancellationToken);
            if (!quedanPend) { doc.EstadoFirma = EstadoFirmaDocumento.SinFirma; }
        }
        _audit.Write(actorUserId, "documento.cancelar_firma", nameof(Firma), firma,
            previousValue: new { Estado = EstadoFirma.Pendiente }, newValue: new { firma.Estado },
            tenantId: _tenant.TenantId!.Value);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<bool>.Ok(true);
    }

    // ---- Firma directa (slice 1) ----

    public async Task<DocumentoResult<FirmanteSnapshotDto>> GetFirmanteSnapshotAsync(
        long docId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == docId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<FirmanteSnapshotDto>.NotFound("El documento no existe."); }
        if (doc.CreatedBy != actorUserId) { return DocumentoResult<FirmanteSnapshotDto>.Invalid("Solo el creador puede firmar directamente."); }

        var snap = await ResolverSnapshotAsync(actorUserId, cancellationToken);
        if (snap is null) { return DocumentoResult<FirmanteSnapshotDto>.NotFound("El firmante no existe."); }
        return DocumentoResult<FirmanteSnapshotDto>.Ok(snap);
    }

    public async Task<DocumentoResult<FirmaEjecutadaDto>> FirmarDirectoAsync(
        long docId, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default)
    {
        // El firmante directo es el creador; el documento debe estar Terminado y con binario PDF.
        var doc = await _db.Documentos.FirstOrDefaultAsync(
            d => d.Id == docId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El documento no existe."); }
        if (doc.CreatedBy != actorUserId) { return DocumentoResult<FirmaEjecutadaDto>.Invalid("Solo el creador puede firmar directamente."); }
        if (doc.Estado != EstadoDocumento.Terminado) { return DocumentoResult<FirmaEjecutadaDto>.Invalid("Solo se puede firmar un documento Terminado."); }
        if (doc.EstadoFirma == EstadoFirmaDocumento.Firmado) { return DocumentoResult<FirmaEjecutadaDto>.Conflict("El documento ya esta firmado."); }
        if (!doc.TieneBinario || string.IsNullOrEmpty(doc.RutaAlmacenamiento))
        {
            return DocumentoResult<FirmaEjecutadaDto>.Invalid("El documento no tiene archivo para firmar.");
        }
        var esPdf = string.Equals(doc.Formato, "PDF", StringComparison.OrdinalIgnoreCase)
                    || (doc.NombreArchivoOriginal?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false);
        if (!esPdf) { return DocumentoResult<FirmaEjecutadaDto>.Invalid("La firma electronica del slice 1 solo aplica a PDF."); }

        var snap = await ResolverSnapshotAsync(actorUserId, cancellationToken);
        if (snap is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El firmante no existe."); }

        var sello = await SellarPdfEnSitioAsync(doc, snap, cancellationToken);
        if (sello is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El binario no esta disponible."); }
        var keyAnterior = doc.RutaAlmacenamiento;
        try
        {
            // Sellar en sitio (sin versionar): el documento apunta al PDF firmado y queda Firmado.
            doc.RutaAlmacenamiento = sello.Value.NuevaKey;
            doc.HashSha256 = sello.Value.Hash;
            doc.TamanoBytes = sello.Value.Size;
            doc.EstadoFirma = EstadoFirmaDocumento.Firmado;

            // Registrar la firma directa (FIR_FIRMAS 'Firmado').
            var firma = new Firma
            {
                TenantId = _tenant.TenantId!.Value,
                DocumentoId = doc.Id,
                FirmanteUserId = actorUserId,
                NombreFirmante = snap.Nombre,
                CargoFirmante = snap.Cargo,
                DependenciaFirmante = snap.Dependencia,
                TipoFirma = TipoFirma.Electronica,
                Estado = EstadoFirma.Firmado,
                HashDocumento = sello.Value.Hash,
                TimestampFirma = sello.Value.Ts,
                IpFirma = ip,
                SesionId = sesionId,
                OtpRequerido = false,
                SolicitadoPor = actorUserId
            };
            _db.Firmas.Add(firma);

            _audit.Write(actorUserId, "documento.firmar", nameof(Firma), doc,
                previousValue: new { EstadoFirma = EstadoFirmaDocumento.Pendiente },
                newValue: new { doc.EstadoFirma, Hash = sello.Value.Hash }, tenantId: _tenant.TenantId!.Value);
            await _db.SaveChangesAsync(cancellationToken);

            await BorrarKeyAnteriorAsync(keyAnterior, sello.Value.NuevaKey, cancellationToken);
            return DocumentoResult<FirmaEjecutadaDto>.Ok(new FirmaEjecutadaDto(firma.Id, sello.Value.Hash, sello.Value.Ts));
        }
        catch
        {
            try { await _storage.DeleteAsync(sello.Value.NuevaKey, cancellationToken); } catch { /* nada */ }
            throw;
        }
    }

    public async Task<IReadOnlyList<FirmanteOpcionDto>> GetFirmantesAsignablesAsync(
        long actorUserId, CancellationToken cancellationToken = default)
        => await _db.TenantUsers.AsNoTracking()
            .Where(u => u.PlatformUserId != actorUserId)
            .OrderBy(u => u.Nombres).ThenBy(u => u.Apellidos)
            .Select(u => new FirmanteOpcionDto(u.PlatformUserId,
                string.IsNullOrWhiteSpace(u.Nombres) && string.IsNullOrWhiteSpace(u.Apellidos)
                    ? u.Email : (u.Nombres + " " + u.Apellidos).Trim()))
            .ToListAsync(cancellationToken);

    // ---- Bandeja "Mis Firmas" (RF10) ----

    public async Task<IReadOnlyList<FirmaBandejaItemDto>> ListarBandejaAsync(
        BandejaFirma tab, long actorUserId, string? texto = null, CancellationToken cancellationToken = default)
    {
        var q = _db.Firmas.AsNoTracking()
            .Include(f => f.Documento!).ThenInclude(d => d.Expediente)
            .AsQueryable();

        // Las firmas individuales (sin circuito) del usuario. Fail-closed: solo lo que le pertenece.
        q = tab switch
        {
            BandejaFirma.Enviadas => q.Where(f => f.SolicitadoPor == actorUserId && f.FirmanteUserId != actorUserId),
            BandejaFirma.Completadas => q.Where(f => f.FirmanteUserId == actorUserId && f.Estado == EstadoFirma.Firmado),
            BandejaFirma.Rechazadas => q.Where(f => f.Estado == EstadoFirma.Rechazado && (f.FirmanteUserId == actorUserId || f.SolicitadoPor == actorUserId)),
            _ => q.Where(f => f.FirmanteUserId == actorUserId && f.Estado == EstadoFirma.Pendiente)
        };

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(f => f.Documento != null && f.Documento.Nombre.ToLower().Contains(t));
        }

        q = tab switch
        {
            BandejaFirma.Completadas => q.OrderByDescending(f => f.TimestampFirma),
            BandejaFirma.Pendientes => q.OrderBy(f => f.CreatedAt),
            _ => q.OrderByDescending(f => f.CreatedAt)
        };

        var rows = await q.Take(200).ToListAsync(cancellationToken);

        // Nombre del solicitante (PlatformUser) para las filas cuyo solicitante no es el firmante.
        var solicitantes = rows.Select(f => f.SolicitadoPor).Distinct().ToList();
        var nombres = await _db.PlatformUsers.AsNoTracking()
            .Where(p => solicitantes.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = p.DisplayName ?? p.Email }).ToListAsync(cancellationToken);
        var nm = nombres.ToDictionary(x => x.Id, x => x.Nombre);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        return rows.Select(f => new FirmaBandejaItemDto(
            f.Id, f.DocumentoId, f.Documento?.Nombre ?? "", f.Documento?.Expediente?.Codigo,
            f.Documento?.TieneBinario ?? false, f.TipoFirma, f.Estado, f.NombreFirmante,
            nm.TryGetValue(f.SolicitadoPor, out var sn) ? sn : null,
            f.Prioridad, f.FechaLimite,
            f.FechaLimite is DateOnly fl ? fl.DayNumber - hoy.DayNumber : (int?)null,
            f.CreatedAt, f.TimestampFirma, f.Instrucciones, f.ComentarioRechazo)).ToList();
    }

    public async Task<FirmaResumenDto> ContarResumenAsync(long actorUserId, CancellationToken cancellationToken = default)
    {
        var pend = await _db.Firmas.AsNoTracking().CountAsync(f => f.FirmanteUserId == actorUserId && f.Estado == EstadoFirma.Pendiente, cancellationToken);
        var firm = await _db.Firmas.AsNoTracking().CountAsync(f => f.FirmanteUserId == actorUserId && f.Estado == EstadoFirma.Firmado, cancellationToken);
        var rech = await _db.Firmas.AsNoTracking().CountAsync(f => f.Estado == EstadoFirma.Rechazado && (f.FirmanteUserId == actorUserId || f.SolicitadoPor == actorUserId), cancellationToken);
        // EnProgreso = circuitos activos (RF07): diferido, 0 por ahora.
        return new FirmaResumenDto(pend, 0, firm, rech);
    }

    public async Task<DocumentoResult<bool>> RechazarSolicitudAsync(
        long firmaId, string comentario, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comentario)) { return DocumentoResult<bool>.Invalid("El comentario es obligatorio al rechazar."); }
        var f = await _db.Firmas.FirstOrDefaultAsync(x => x.Id == firmaId, cancellationToken);
        if (f is null) { return DocumentoResult<bool>.NotFound("La solicitud de firma no existe."); }
        if (f.FirmanteUserId != actorUserId) { return DocumentoResult<bool>.Invalid("Esta solicitud no te esta asignada."); }
        if (f.Estado != EstadoFirma.Pendiente) { return DocumentoResult<bool>.Conflict("La solicitud ya fue resuelta."); }

        f.Estado = EstadoFirma.Rechazado;
        f.ComentarioRechazo = comentario.Trim();

        // Si no quedan mas firmas pendientes del documento, su dimension vuelve a "sin firma".
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == f.DocumentoId, cancellationToken);
        if (doc is not null && doc.EstadoFirma == EstadoFirmaDocumento.Pendiente)
        {
            var quedan = await _db.Firmas.AnyAsync(x => x.DocumentoId == f.DocumentoId && x.Id != firmaId && x.Estado == EstadoFirma.Pendiente, cancellationToken);
            if (!quedan) { doc.EstadoFirma = EstadoFirmaDocumento.SinFirma; }
        }
        _audit.Write(actorUserId, "documento.rechazar_firma", nameof(Firma), f,
            previousValue: new { Estado = EstadoFirma.Pendiente }, newValue: new { f.Estado, f.ComentarioRechazo },
            tenantId: _tenant.TenantId!.Value);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<bool>.Ok(true);
    }

    public async Task<DocumentoResult<FirmaEjecutadaDto>> FirmarSolicitadaAsync(
        long firmaId, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default)
    {
        var f = await _db.Firmas.FirstOrDefaultAsync(x => x.Id == firmaId, cancellationToken);
        if (f is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("La solicitud de firma no existe."); }
        if (f.FirmanteUserId != actorUserId) { return DocumentoResult<FirmaEjecutadaDto>.Invalid("Esta solicitud no te esta asignada."); }
        if (f.Estado != EstadoFirma.Pendiente) { return DocumentoResult<FirmaEjecutadaDto>.Conflict("La solicitud ya fue resuelta."); }

        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == f.DocumentoId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El documento no existe."); }
        var errPdf = ValidarFirmablePdf(doc);
        if (errPdf is not null) { return DocumentoResult<FirmaEjecutadaDto>.Invalid(errPdf); }

        var snap = await ResolverSnapshotAsync(actorUserId, cancellationToken);
        if (snap is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El firmante no existe."); }

        var sello = await SellarPdfEnSitioAsync(doc, snap, cancellationToken);
        if (sello is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El binario no esta disponible."); }
        var keyAnterior = doc.RutaAlmacenamiento;
        try
        {
            doc.RutaAlmacenamiento = sello.Value.NuevaKey;
            doc.HashSha256 = sello.Value.Hash;
            doc.TamanoBytes = sello.Value.Size;
            doc.EstadoFirma = EstadoFirmaDocumento.Firmado;

            // Cumplir la solicitud pendiente (no se duplica fila): pasa a Firmado con el resultado.
            f.Estado = EstadoFirma.Firmado;
            f.NombreFirmante = snap.Nombre;
            f.CargoFirmante = snap.Cargo;
            f.DependenciaFirmante = snap.Dependencia;
            f.HashDocumento = sello.Value.Hash;
            f.TimestampFirma = sello.Value.Ts;
            f.IpFirma = ip;
            f.SesionId = sesionId;

            _audit.Write(actorUserId, "documento.firmar_solicitada", nameof(Firma), f,
                previousValue: new { Estado = EstadoFirma.Pendiente },
                newValue: new { f.Estado, Hash = sello.Value.Hash }, tenantId: _tenant.TenantId!.Value);
            await _db.SaveChangesAsync(cancellationToken);

            await BorrarKeyAnteriorAsync(keyAnterior, sello.Value.NuevaKey, cancellationToken);
            return DocumentoResult<FirmaEjecutadaDto>.Ok(new FirmaEjecutadaDto(f.Id, sello.Value.Hash, sello.Value.Ts));
        }
        catch
        {
            try { await _storage.DeleteAsync(sello.Value.NuevaKey, cancellationToken); } catch { /* nada */ }
            throw;
        }
    }

    // ---- Helpers ----

    /// <summary>Valida que el documento sea firmable: con binario PDF y no firmado aun.</summary>
    private static string? ValidarFirmablePdf(Documento doc)
    {
        if (doc.EstadoFirma == EstadoFirmaDocumento.Firmado) { return "El documento ya esta firmado."; }
        if (!doc.TieneBinario || string.IsNullOrEmpty(doc.RutaAlmacenamiento)) { return "El documento no tiene archivo para firmar."; }
        var esPdf = string.Equals(doc.Formato, "PDF", StringComparison.OrdinalIgnoreCase)
                    || (doc.NombreArchivoOriginal?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false);
        return esPdf ? null : "La firma electronica del slice 1 solo aplica a PDF.";
    }

    /// <summary>
    /// Descarga el PDF vivo, estampa la cajita (best-effort) y sube el sellado a una key nueva SIN pisar la
    /// anterior (el caller confirma en base y luego borra la vieja). Devuelve null si no hay binario.
    /// </summary>
    private async Task<(long Size, string Hash, DateTimeOffset Ts, string NuevaKey)?> SellarPdfEnSitioAsync(
        Documento doc, FirmanteSnapshotDto snap, CancellationToken cancellationToken)
    {
        var stream = await _storage.GetAsync(doc.RutaAlmacenamiento!, cancellationToken);
        if (stream is null) { return null; }
        byte[] original;
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms, cancellationToken);
            await stream.DisposeAsync();
            original = ms.ToArray();
        }
        var ahora = DateTimeOffset.UtcNow;
        var cajita = new CajitaFirma(snap.Nombre, snap.Cargo, snap.Dependencia,
            ahora.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), $"verificar.tronox.co/v/{doc.Id}");
        var sellado = _stamper.EstamparCajita(original, cajita);
        var hash = DocumentoRules.HashSha256(sellado);
        var nuevaKey = $"{_tenant.TenantId!.Value}/{Guid.NewGuid():N}.pdf";
        using (var ms = new MemoryStream(sellado, writable: false))
        {
            await _storage.PutAsync(nuevaKey, ms, "application/pdf", cancellationToken);
        }
        return (sellado.LongLength, hash, ahora, nuevaKey);
    }

    private async Task BorrarKeyAnteriorAsync(string? keyAnterior, string nuevaKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(keyAnterior) && keyAnterior != nuevaKey)
        {
            try { await _storage.DeleteAsync(keyAnterior, cancellationToken); } catch { /* huerfano tolerable */ }
        }
    }

    /// <summary>Snapshot del firmante. Slice 1: nombre del PlatformUser; cargo/dependencia diferidos (organigrama).</summary>
    private async Task<FirmanteSnapshotDto?> ResolverSnapshotAsync(long userId, CancellationToken cancellationToken)
    {
        var u = await _db.PlatformUsers.AsNoTracking()
            .Where(p => p.Id == userId)
            .Select(p => new { p.Id, Nombre = p.DisplayName ?? p.Email })
            .FirstOrDefaultAsync(cancellationToken);
        return u is null ? null : new FirmanteSnapshotDto(u.Id, u.Nombre, null, null);
    }
}
