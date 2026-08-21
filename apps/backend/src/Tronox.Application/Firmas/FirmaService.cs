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
            SolicitadoPor = actorUserId
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

        // 1) Descargar el PDF actual.
        var stream = await _storage.GetAsync(doc.RutaAlmacenamiento, cancellationToken);
        if (stream is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El binario no esta disponible."); }
        byte[] original;
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms, cancellationToken);
            await stream.DisposeAsync();
            original = ms.ToArray();
        }

        // 2) Cajita visual (best-effort) + 3) hash del PDF sellado.
        var ahora = DateTimeOffset.UtcNow;
        var cajita = new CajitaFirma(
            Nombre: snap.Nombre,
            Cargo: snap.Cargo,
            Dependencia: snap.Dependencia,
            Fecha: ahora.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            VerificarUrl: $"verificar.tronox.co/v/{doc.Id}");
        var sellado = _stamper.EstamparCajita(original, cajita);
        var hash = DocumentoRules.HashSha256(sellado);

        // 4) Subir el PDF sellado a una key nueva (no se pisa el original hasta confirmar en base).
        var nuevaKey = $"{_tenant.TenantId!.Value}/{Guid.NewGuid():N}.pdf";
        using (var ms = new MemoryStream(sellado, writable: false))
        {
            await _storage.PutAsync(nuevaKey, ms, "application/pdf", cancellationToken);
        }

        var keyAnterior = doc.RutaAlmacenamiento;
        try
        {
            // 5) Sellar en sitio (sin versionar): el documento apunta al PDF firmado y queda Firmado.
            doc.RutaAlmacenamiento = nuevaKey;
            doc.HashSha256 = hash;
            doc.TamanoBytes = sellado.LongLength;
            doc.EstadoFirma = EstadoFirmaDocumento.Firmado;

            // 6) Registrar la firma directa (FIR_FIRMAS 'Firmado') + cumplir solicitud pendiente propia si la hubiera.
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
                HashDocumento = hash,
                TimestampFirma = ahora,
                IpFirma = ip,
                SesionId = sesionId,
                OtpRequerido = false,
                SolicitadoPor = actorUserId
            };
            _db.Firmas.Add(firma);

            _audit.Write(actorUserId, "documento.firmar", nameof(Firma), doc,
                previousValue: new { EstadoFirma = EstadoFirmaDocumento.Pendiente },
                newValue: new { doc.EstadoFirma, Hash = hash }, tenantId: _tenant.TenantId!.Value);
            await _db.SaveChangesAsync(cancellationToken);

            // 7) Borrar el binario anterior (best-effort; ya no se referencia).
            if (!string.IsNullOrEmpty(keyAnterior) && keyAnterior != nuevaKey)
            {
                try { await _storage.DeleteAsync(keyAnterior, cancellationToken); } catch { /* huerfano tolerable */ }
            }
            return DocumentoResult<FirmaEjecutadaDto>.Ok(new FirmaEjecutadaDto(firma.Id, hash, ahora));
        }
        catch
        {
            // Si falla la base, no dejar el PDF sellado huerfano.
            try { await _storage.DeleteAsync(nuevaKey, cancellationToken); } catch { /* nada */ }
            throw;
        }
    }

    // ---- Helpers ----

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
