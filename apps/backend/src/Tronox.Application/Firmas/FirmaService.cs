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
    private readonly IEmailSender _email;

    private const int OtpVigenciaMinutos = 5;

    public FirmaService(
        IApplicationDbContext db, ITenantContext tenant, IObjectStorage storage,
        IAuditWriter audit, IPdfSignatureStamper stamper, IEmailSender email)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
        _audit = audit;
        _stamper = stamper;
        _email = email;
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

        var sello = await SellarPdfEnSitioAsync(doc, snap, 0, cancellationToken);
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

    // ---- Circuitos multi-firmante (RF07) ----

    public async Task<DocumentoResult<long>> CrearCircuitoAsync(
        CrearCircuitoRequest r, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (r.Firmantes is null || r.Firmantes.Count == 0) { return DocumentoResult<long>.Invalid("Agrega al menos un firmante."); }
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == r.DocId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<long>.NotFound("El documento no existe."); }
        if (doc.CreatedBy != actorUserId) { return DocumentoResult<long>.Invalid("Solo el creador puede iniciar el circuito."); }
        if (doc.EstadoFirma == EstadoFirmaDocumento.Firmado) { return DocumentoResult<long>.Conflict("El documento ya esta firmado."); }
        if (!doc.TieneBinario) { return DocumentoResult<long>.Invalid("Solo se puede armar un circuito sobre un documento con archivo."); }
        if (await _db.FirmaCircuitos.AnyAsync(c => c.DocumentoId == r.DocId && c.Estado == EstadoCircuito.Activo, cancellationToken))
        {
            return DocumentoResult<long>.Conflict("El documento ya tiene un circuito de firma activo.");
        }
        if (r.Firmantes.Any(x => x.PlatformUserId == actorUserId))
        {
            return DocumentoResult<long>.Invalid("No puedes incluirte como firmante del circuito que solicitas.");
        }

        var ids = r.Firmantes.Select(x => x.PlatformUserId).Distinct().ToList();
        if (ids.Count != r.Firmantes.Count) { return DocumentoResult<long>.Invalid("Hay firmantes repetidos."); }
        var users = await _db.PlatformUsers.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = p.DisplayName ?? p.Email }).ToListAsync(cancellationToken);
        var nm = users.ToDictionary(x => x.Id, x => x.Nombre);
        if (nm.Count != ids.Count) { return DocumentoResult<long>.NotFound("Algun firmante no existe."); }

        var solicitanteNombre = await _db.PlatformUsers.AsNoTracking()
            .Where(p => p.Id == actorUserId).Select(p => p.DisplayName ?? p.Email).FirstOrDefaultAsync(cancellationToken);
        var tenantId = _tenant.TenantId!.Value;

        var circ = new FirmaCircuito
        {
            TenantId = tenantId,
            DocumentoId = doc.Id,
            Modo = r.Modo,
            Estado = EstadoCircuito.Activo,
            TotalFirmantes = r.Firmantes.Count,
            FirmantesCompletados = 0,
            TipoFirmaMixto = r.Firmantes.Select(x => x.TipoFirma).Distinct().Count() > 1,
            OtpRequerido = r.OtpRequerido,
            SolicitantePlatformUserId = actorUserId,
            SolicitanteNombre = solicitanteNombre
        };
        _db.FirmaCircuitos.Add(circ);
        await _db.SaveChangesAsync(cancellationToken);

        var orden = 0;
        foreach (var fin in r.Firmantes)
        {
            orden++;
            var activo = r.Modo == ModoCircuito.Paralelo || orden == 1;
            var cff = new FirmaCircuitoFirmante
            {
                TenantId = tenantId,
                CircuitoId = circ.Id,
                Orden = orden,
                FirmantePlatformUserId = fin.PlatformUserId,
                NombreFirmante = nm[fin.PlatformUserId],
                TipoFirma = fin.TipoFirma,
                Estado = activo ? EstadoCircuitoFirmante.Pendiente : EstadoCircuitoFirmante.EnEspera
            };
            _db.FirmaCircuitoFirmantes.Add(cff);
            await _db.SaveChangesAsync(cancellationToken);

            if (activo)
            {
                var firma = new Firma
                {
                    TenantId = tenantId,
                    DocumentoId = doc.Id,
                    FirmanteUserId = fin.PlatformUserId,
                    NombreFirmante = nm[fin.PlatformUserId],
                    TipoFirma = fin.TipoFirma,
                    Estado = EstadoFirma.Pendiente,
                    OtpRequerido = r.OtpRequerido,
                    SolicitadoPor = actorUserId,
                    CircuitoId = circ.Id,
                    Prioridad = Domain.Enums.PrioridadTarea.Media,
                    FechaLimite = r.FechaLimite,
                    Instrucciones = string.IsNullOrWhiteSpace(r.Instrucciones) ? null : r.Instrucciones.Trim()
                };
                _db.Firmas.Add(firma);
                await _db.SaveChangesAsync(cancellationToken);
                cff.FirmaId = firma.Id;
            }
        }

        doc.EstadoFirma = EstadoFirmaDocumento.Pendiente;
        _audit.Write(actorUserId, "documento.circuito_crear", nameof(FirmaCircuito), circ,
            previousValue: null, newValue: new { circ.Modo, circ.TotalFirmantes }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return DocumentoResult<long>.Ok(circ.Id);
    }

    public async Task<IReadOnlyList<CircuitoEnviadoDto>> ListarCircuitosEnviadosAsync(
        long actorUserId, string? texto = null, CancellationToken cancellationToken = default)
    {
        var q = _db.FirmaCircuitos.AsNoTracking()
            .Include(c => c.Firmantes)
            .Include(c => c.Documento!).ThenInclude(d => d.Expediente)
            .Where(c => c.SolicitantePlatformUserId == actorUserId);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(c => c.Documento != null && c.Documento.Nombre.ToLower().Contains(t));
        }
        var rows = await q.OrderByDescending(c => c.Id).Take(100).ToListAsync(cancellationToken);

        return rows.Select(c => new CircuitoEnviadoDto(
            c.Id, c.DocumentoId, c.Documento?.Nombre ?? "", c.Documento?.Expediente?.Codigo,
            c.Documento?.TieneBinario ?? false, c.Modo, c.Estado, c.TotalFirmantes, c.FirmantesCompletados,
            c.CreatedAt, c.MotivoCancelacion,
            c.Firmantes.OrderBy(x => x.Orden).Select(x => new CircuitoFirmanteDto(
                x.Orden, x.NombreFirmante, x.CargoFirmante, x.TipoFirma, x.Estado, x.TimestampFirma)).ToList()))
            .ToList();
    }

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
            f.Documento?.TieneBinario ?? false, f.TipoFirma, f.Estado, f.OtpRequerido, f.NombreFirmante,
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
        // EnProgreso = circuitos activos iniciados por el usuario (RF07).
        var prog = await _db.FirmaCircuitos.AsNoTracking().CountAsync(c => c.SolicitantePlatformUserId == actorUserId && c.Estado == EstadoCircuito.Activo, cancellationToken);
        return new FirmaResumenDto(pend, prog, firm, rech);
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
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == f.DocumentoId, cancellationToken);

        if (f.CircuitoId is long cid)
        {
            // RF07 §3.7.3: un rechazo cancela TODO el circuito; las firmas previas quedan invalidadas.
            var circ = await _db.FirmaCircuitos.FirstOrDefaultAsync(c => c.Id == cid, cancellationToken);
            if (circ is not null) { circ.Estado = EstadoCircuito.Cancelado; circ.MotivoCancelacion = comentario.Trim(); }
            var cf = await _db.FirmaCircuitoFirmantes.FirstOrDefaultAsync(x => x.CircuitoId == cid && x.FirmaId == f.Id, cancellationToken);
            if (cf is not null) { cf.Estado = EstadoCircuitoFirmante.Rechazado; }
            // Cancela las solicitudes aun pendientes del circuito.
            var pendientes = await _db.Firmas
                .Where(x => x.CircuitoId == cid && x.Id != firmaId && x.Estado == EstadoFirma.Pendiente)
                .ToListAsync(cancellationToken);
            foreach (var p in pendientes) { p.Estado = EstadoFirma.Rechazado; p.ComentarioRechazo = comentario.Trim(); }
            if (doc is not null) { doc.EstadoFirma = EstadoFirmaDocumento.SinFirma; }
        }
        else if (doc is not null && doc.EstadoFirma == EstadoFirmaDocumento.Pendiente)
        {
            // Firma individual: si no quedan mas pendientes del documento, vuelve a "sin firma".
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

        return await CumplirFirmaAsync(f, actorUserId, ip, sesionId, cancellationToken);
    }

    public async Task<DocumentoResult<OtpEnvioDto>> GenerarOtpAsync(
        long firmaId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var f = await _db.Firmas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == firmaId, cancellationToken);
        if (f is null) { return DocumentoResult<OtpEnvioDto>.NotFound("La solicitud de firma no existe."); }
        if (f.FirmanteUserId != actorUserId) { return DocumentoResult<OtpEnvioDto>.Invalid("Esta solicitud no te esta asignada."); }
        if (f.Estado != EstadoFirma.Pendiente) { return DocumentoResult<OtpEnvioDto>.Conflict("La solicitud ya fue resuelta."); }

        var correo = await _db.PlatformUsers.AsNoTracking()
            .Where(p => p.Id == actorUserId).Select(p => p.Email).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(correo)) { return DocumentoResult<OtpEnvioDto>.Invalid("El firmante no tiene correo para el OTP."); }

        // Invalida los OTP vigentes previos de esta firma (solo el ultimo cuenta).
        var previos = await _db.FirmaOtps
            .Where(o => o.FirmaId == firmaId && o.VerificadoAt == null && o.ExpiraAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);
        foreach (var p in previos) { p.ExpiraAt = DateTimeOffset.UtcNow; }

        var codigo = GenerarCodigo6();
        var otp = new FirmaOtp
        {
            TenantId = _tenant.TenantId!.Value,
            FirmaId = firmaId,
            Usuario = actorUserId,
            CodigoHash = Sha256Hex(codigo),
            Canal = "correo",
            ExpiraAt = DateTimeOffset.UtcNow.AddMinutes(OtpVigenciaMinutos),
            Intentos = 0
        };
        _db.FirmaOtps.Add(otp);
        await _db.SaveChangesAsync(cancellationToken);

        // Envio best-effort. Si no hay SMTP configurado, se revela el codigo en pantalla (modo demo).
        string? demo = null;
        try
        {
            await _email.SendAsync(correo, "Codigo de verificacion de firma - TRONOX",
                $"<p>Su codigo de firma es <strong>{codigo}</strong>. Vigencia: {OtpVigenciaMinutos} minutos.</p>", cancellationToken);
        }
        catch { demo = codigo; }

        return DocumentoResult<OtpEnvioDto>.Ok(new OtpEnvioDto(Enmascarar(correo), OtpVigenciaMinutos, demo));
    }

    public async Task<DocumentoResult<FirmaEjecutadaDto>> FirmarConOtpAsync(
        long firmaId, string codigo, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default)
    {
        var f = await _db.Firmas.FirstOrDefaultAsync(x => x.Id == firmaId, cancellationToken);
        if (f is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("La solicitud de firma no existe."); }
        if (f.FirmanteUserId != actorUserId) { return DocumentoResult<FirmaEjecutadaDto>.Invalid("Esta solicitud no te esta asignada."); }
        if (f.Estado != EstadoFirma.Pendiente) { return DocumentoResult<FirmaEjecutadaDto>.Conflict("La solicitud ya fue resuelta."); }

        var okOtp = await ValidarOtpAsync(firmaId, codigo, cancellationToken);
        if (!okOtp) { return DocumentoResult<FirmaEjecutadaDto>.Invalid("Codigo invalido o expirado."); }

        return await CumplirFirmaAsync(f, actorUserId, ip, sesionId, cancellationToken);
    }

    // ---- Helpers ----

    /// <summary>Sella el PDF y pasa la firma pendiente (ya validada) a Firmado. Compartido por firmar con/sin OTP.</summary>
    private async Task<DocumentoResult<FirmaEjecutadaDto>> CumplirFirmaAsync(
        Firma f, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken)
    {
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == f.DocumentoId && !d.EsVersionHistorica, cancellationToken);
        if (doc is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El documento no existe."); }
        var errPdf = ValidarFirmablePdf(doc);
        if (errPdf is not null) { return DocumentoResult<FirmaEjecutadaDto>.Invalid(errPdf); }

        var snap = await ResolverSnapshotAsync(actorUserId, cancellationToken);
        if (snap is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El firmante no existe."); }

        // Contexto de circuito (RF07): si la firma pertenece a un circuito, se avanza al completar.
        FirmaCircuito? circ = null;
        FirmaCircuitoFirmante? cf = null;
        var indiceCajita = 0;
        if (f.CircuitoId is long cid)
        {
            circ = await _db.FirmaCircuitos.FirstOrDefaultAsync(c => c.Id == cid, cancellationToken);
            cf = await _db.FirmaCircuitoFirmantes.FirstOrDefaultAsync(x => x.CircuitoId == cid && x.FirmaId == f.Id, cancellationToken);
            indiceCajita = circ?.FirmantesCompletados ?? 0; // apila la cajita sobre las ya firmadas
        }

        var sello = await SellarPdfEnSitioAsync(doc, snap, indiceCajita, cancellationToken);
        if (sello is null) { return DocumentoResult<FirmaEjecutadaDto>.NotFound("El binario no esta disponible."); }
        var keyAnterior = doc.RutaAlmacenamiento;
        try
        {
            doc.RutaAlmacenamiento = sello.Value.NuevaKey;
            doc.HashSha256 = sello.Value.Hash;
            doc.TamanoBytes = sello.Value.Size;

            // Cumplir la solicitud pendiente (no se duplica fila): pasa a Firmado con el resultado.
            f.Estado = EstadoFirma.Firmado;
            f.NombreFirmante = snap.Nombre;
            f.CargoFirmante = snap.Cargo;
            f.DependenciaFirmante = snap.Dependencia;
            f.HashDocumento = sello.Value.Hash;
            f.TimestampFirma = sello.Value.Ts;
            f.IpFirma = ip;
            f.SesionId = sesionId;

            // Avance del circuito. El documento solo queda Firmado cuando el circuito se completa.
            var completo = true;
            if (circ is not null)
            {
                if (cf is not null) { cf.Estado = EstadoCircuitoFirmante.Firmado; cf.TimestampFirma = sello.Value.Ts; }
                circ.FirmantesCompletados += 1;
                if (circ.FirmantesCompletados >= circ.TotalFirmantes)
                {
                    circ.Estado = EstadoCircuito.Completado;
                }
                else
                {
                    completo = false;
                    if (circ.Modo == ModoCircuito.Secuencial)
                    {
                        var siguiente = await _db.FirmaCircuitoFirmantes
                            .Where(x => x.CircuitoId == circ.Id && x.Estado == EstadoCircuitoFirmante.EnEspera)
                            .OrderBy(x => x.Orden).FirstOrDefaultAsync(cancellationToken);
                        if (siguiente is not null)
                        {
                            var nueva = NuevaFirmaDeCircuito(circ, siguiente);
                            _db.Firmas.Add(nueva);
                            await _db.SaveChangesAsync(cancellationToken); // obtener Id
                            siguiente.Estado = EstadoCircuitoFirmante.Pendiente;
                            siguiente.FirmaId = nueva.Id;
                        }
                    }
                }
            }
            doc.EstadoFirma = completo ? EstadoFirmaDocumento.Firmado : EstadoFirmaDocumento.Pendiente;

            _audit.Write(actorUserId, "documento.firmar_solicitada", nameof(Firma), f,
                previousValue: new { Estado = EstadoFirma.Pendiente },
                newValue: new { f.Estado, Hash = sello.Value.Hash, CircuitoCompleto = completo }, tenantId: _tenant.TenantId!.Value);
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

    /// <summary>Crea la solicitud (Firma) Pendiente del siguiente firmante del circuito.</summary>
    private Firma NuevaFirmaDeCircuito(FirmaCircuito circ, FirmaCircuitoFirmante f) => new()
    {
        TenantId = _tenant.TenantId!.Value,
        DocumentoId = circ.DocumentoId,
        FirmanteUserId = f.FirmantePlatformUserId,
        NombreFirmante = f.NombreFirmante,
        CargoFirmante = f.CargoFirmante,
        TipoFirma = f.TipoFirma,
        Estado = EstadoFirma.Pendiente,
        OtpRequerido = circ.OtpRequerido,
        SolicitadoPor = circ.SolicitantePlatformUserId,
        CircuitoId = circ.Id
    };

    /// <summary>Valida el codigo contra el OTP vigente de la firma (incrementa intentos; marca verificado si OK).</summary>
    private async Task<bool> ValidarOtpAsync(long firmaId, string codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo)) { return false; }
        var otp = await _db.FirmaOtps
            .Where(o => o.FirmaId == firmaId && o.VerificadoAt == null)
            .OrderByDescending(o => o.Id).FirstOrDefaultAsync(cancellationToken);
        if (otp is null) { return false; }

        otp.Intentos += 1;
        if (DateTimeOffset.UtcNow > otp.ExpiraAt) { await _db.SaveChangesAsync(cancellationToken); return false; }
        if (!string.Equals(otp.CodigoHash, Sha256Hex(codigo.Trim()), StringComparison.OrdinalIgnoreCase))
        {
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }
        otp.VerificadoAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Codigo de 6 digitos con RNG criptografico (000000-999999).</summary>
    private static string GenerarCodigo6()
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var val = (int)(BitConverter.ToUInt32(bytes) & 0x7FFFFFFF);
        return (val % 1000000).ToString("D6");
    }

    private static string Sha256Hex(string texto)
    {
        var data = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(data).ToLowerInvariant();
    }

    /// <summary>Enmascara un correo para mostrarlo (ej. j***@dominio.com).</summary>
    private static string Enmascarar(string correo)
    {
        var at = correo.IndexOf('@');
        if (at <= 1) { return correo; }
        return correo[0] + new string('*', Math.Min(3, at - 1)) + correo[at..];
    }

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
        Documento doc, FirmanteSnapshotDto snap, int indiceCajita, CancellationToken cancellationToken)
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
            ahora.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), $"verificar.tronox.co/v/{doc.Id}", indiceCajita);
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
