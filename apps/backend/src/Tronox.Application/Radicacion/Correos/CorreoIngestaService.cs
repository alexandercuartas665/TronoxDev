using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Documentos;
using Tronox.Application.Tenancy;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion.Correos;

/// <summary>
/// Implementacion de la ingesta de correos con IA (RQ16, cerebro del modulo Correos->PQR portado de VISAL).
/// Flujo por buzon: descifra la clave -> lee IMAP (no leidos) -> por correo: dedup Message-ID, sube adjuntos
/// a object storage, guarda CorreoRecibido, clasifica con IA (tipo/datos del peticionario), y segun el modo
/// del buzon radica (RadicadorService, enlazando el tercero DAT-02) o deja pendiente. Marca leidos al final.
/// </summary>
public sealed class CorreoIngestaService : ICorreoIngestaService
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _secret;
    private readonly IImapCorreoReader _reader;
    private readonly ICorreoClasificadorIa _clasificador;
    private readonly IObjectStorage _storage;
    private readonly IRadicacionCorreosService _correos;
    private readonly ITenantContext _tenant;

    public CorreoIngestaService(IApplicationDbContext db, ISecretProtector secret, IImapCorreoReader reader,
        ICorreoClasificadorIa clasificador, IObjectStorage storage, IRadicacionCorreosService correos, ITenantContext tenant)
    {
        _db = db;
        _secret = secret;
        _reader = reader;
        _clasificador = clasificador;
        _storage = storage;
        _correos = correos;
        _tenant = tenant;
    }

    private const int MaxPorCorrida = 25;

    public async Task<ImapTestResult> ProbarConexionAsync(long buzonId, CancellationToken ct = default)
    {
        var b = await _db.BuzonesCorreo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == buzonId, ct);
        if (b is null) { return new ImapTestResult(false, 0, "Buzon no encontrado."); }
        if (string.IsNullOrEmpty(b.ContrasenaEncrypted)) { return new ImapTestResult(false, 0, "El buzon no tiene clave guardada."); }
        string pass;
        try { pass = _secret.Unprotect(b.ContrasenaEncrypted); }
        catch { return new ImapTestResult(false, 0, "La clave del buzon no se pudo descifrar; vuelve a guardarla."); }
        return await _reader.TestConnectionAsync(ImapParamsDe(b, pass), ct);
    }

    public async Task<CorreoIngestaResumen> ProcesarBuzonAsync(long buzonId, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return CorreoIngestaResumen.Fail("Sesion no valida."); }

        // DAT-07 (fail-closed): Correos -> PQR es una funcion de IA; si la entidad la tiene apagada, no se procesa.
        var iaHabilitada = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId.Value).Select(t => (bool?)t.IaHabilitada).FirstOrDefaultAsync(ct) ?? true;
        if (!iaHabilitada) { return CorreoIngestaResumen.Fail("La IA esta deshabilitada para esta entidad."); }

        var b = await _db.BuzonesCorreo.FirstOrDefaultAsync(x => x.Id == buzonId, ct);
        if (b is null) { return CorreoIngestaResumen.Fail("Buzon no encontrado."); }
        if (b.Protocolo != BuzonProtocolo.Imap) { return CorreoIngestaResumen.Fail("Solo se soporta IMAP."); }
        if (string.IsNullOrEmpty(b.ContrasenaEncrypted)) { return CorreoIngestaResumen.Fail("El buzon no tiene clave guardada."); }

        string pass;
        try { pass = _secret.Unprotect(b.ContrasenaEncrypted); }
        catch { return CorreoIngestaResumen.Fail("La clave del buzon no se pudo descifrar; vuelve a guardarla."); }

        // Tipos PQRSD de entrada para mapear la clasificacion de la IA a un TipoComunicacion.
        var tipos = await _db.TiposComunicacion.AsNoTracking()
            .Where(t => t.Activo && t.Direccion == RadicacionDireccion.Entrada)
            .Select(t => new { t.Id, t.Nombre, t.EsPqrsd })
            .ToListAsync(ct);

        IReadOnlyList<CorreoEntrante> correos;
        try
        {
            correos = await _reader.FetchAsync(new ImapFetchParams(
                b.Servidor ?? "imap.gmail.com", b.Puerto ?? 993, b.Seguridad == BuzonSeguridad.SslTls,
                b.Usuario, pass, string.IsNullOrWhiteSpace(b.Carpeta) ? "INBOX" : b.Carpeta,
                OnlyUnread: true, Since: null, MaxMessages: MaxPorCorrida), ct);
        }
        catch (Exception ex) { return CorreoIngestaResumen.Fail($"No se pudo leer el buzon: {ex.Message}"); }

        int pqr = 0, descartados = 0, duplicados = 0, errores = 0, adjuntosTot = 0, tokensTot = 0, radicados = 0;
        var uidsProcesados = new List<long>();

        foreach (var c in correos)
        {
            ct.ThrowIfCancellationRequested();
            // Dedup por Message-ID dentro del buzon.
            if (await _db.CorreosRecibidos.AnyAsync(x => x.BuzonCorreoId == buzonId && x.MessageId == c.MessageId, ct))
            {
                duplicados++;
                uidsProcesados.Add(c.Uid);
                continue;
            }

            var correo = new CorreoRecibido
            {
                TenantId = tenantId.Value,
                BuzonCorreoId = buzonId,
                BuzonEmail = b.DireccionEmail,
                Estado = CorreoRevisionEstado.Pendiente,
                Remitente = c.FromName ?? c.FromAddress,
                RemitenteEmail = c.FromAddress,
                Asunto = c.Subject,
                FechaRecepcion = c.ReceivedAt?.UtcDateTime,
                MessageId = c.MessageId,
                InReplyTo = c.InReplyTo,
                CuerpoTratado = c.BodyText,
                Modo = b.ModoRadicacion,
                NumAdjuntos = c.Attachments.Count
            };

            // Sube adjuntos a object storage (invariante 9) y los cuelga del correo.
            foreach (var a in c.Attachments)
            {
                if (a.Contenido is not { Length: > 0 }) { continue; }
                var ext = System.IO.Path.GetExtension(a.Nombre).TrimStart('.').ToLowerInvariant();
                var contentType = a.MimeType ?? DocumentoRules.ContentType(a.Nombre);
                var hash = DocumentoRules.HashSha256(a.Contenido);
                var key = $"{tenantId.Value}/{Guid.NewGuid():N}" + (string.IsNullOrEmpty(ext) ? "" : $".{ext}");
                using (var ms = new MemoryStream(a.Contenido, writable: false))
                {
                    await _storage.PutAsync(key, ms, contentType, ct);
                }
                correo.Adjuntos.Add(new CorreoRecibidoAdjunto
                {
                    TenantId = tenantId.Value,
                    Nombre = a.Nombre,
                    Extension = string.IsNullOrEmpty(ext) ? null : ext,
                    MimeType = contentType,
                    TamanoBytes = a.Contenido.LongLength,
                    StorageKey = key,
                    Sha256 = hash
                });
                adjuntosTot++;
            }

            // Clasificacion con IA. Si el buzon apunta a un agente, se usa su proveedor/comportamiento editable.
            var cl = await _clasificador.ClasificarAsync(correo.Remitente, correo.Asunto, correo.CuerpoTratado ?? "", b.AgenteIaId, ct);
            correo.ClasificacionJson = cl.Json;
            correo.TokensIa = cl.TokensTotal;
            tokensTot += cl.TokensTotal;

            // Bitacora del agente: si el buzon usa un agente, deja el rastro de la clasificacion.
            if (b.AgenteIaId is long agLog)
            {
                _db.AiAgentRunLogs.Add(new Domain.Entities.AiAgentRunLog
                {
                    TenantId = tenantId.Value, AgentId = agLog, ConversationId = 0,
                    OccurredAt = DateTimeOffset.UtcNow, Kind = AiAgentRunLogKind.Inbound,
                    Title = "Correo recibido", Content = $"De: {correo.Remitente}\nAsunto: {correo.Asunto}"
                });
                _db.AiAgentRunLogs.Add(new Domain.Entities.AiAgentRunLog
                {
                    TenantId = tenantId.Value, AgentId = agLog, ConversationId = 0,
                    OccurredAt = DateTimeOffset.UtcNow, Kind = cl.Ok ? AiAgentRunLogKind.Reply : AiAgentRunLogKind.Error,
                    Title = "Clasificacion IA", Content = cl.Ok ? $"es_pqr={cl.EsPqr}; tipo={cl.Tipo}" : $"error: {cl.Error}", Response = cl.Json
                });
            }

            if (!cl.Ok)
            {
                errores++;
                _db.CorreosRecibidos.Add(correo); // se conserva para no reprocesar; queda pendiente para revision manual.
                await _db.SaveChangesAsync(ct);
                uidsProcesados.Add(c.Uid);
                continue;
            }

            if (!cl.EsPqr)
            {
                correo.Estado = CorreoRevisionEstado.Descartado;
                correo.Confianza = 0;
                descartados++;
                _db.CorreosRecibidos.Add(correo);
                await _db.SaveChangesAsync(ct);
                uidsProcesados.Add(c.Uid);
                continue;
            }

            // Es PQR: enriquece con los datos extraidos por la IA.
            pqr++;
            if (!string.IsNullOrWhiteSpace(cl.Nombres)) { correo.Remitente = cl.Nombres; }
            if (!string.IsNullOrWhiteSpace(cl.Email)) { correo.RemitenteEmail = cl.Email; }
            correo.RemitenteDocumento = SoloDigitos(cl.Identificacion);
            correo.RemitenteTelefono = cl.Celular;
            if (!string.IsNullOrWhiteSpace(cl.Descripcion)) { correo.CuerpoTratado = cl.Descripcion; }
            correo.TipoDetectadoId = MapearTipo(cl.Tipo, tipos.Select(t => (t.Id, t.Nombre, t.EsPqrsd)).ToList())
                ?? b.TipoComunicacionDefaultId;
            correo.Confianza = 90;

            _db.CorreosRecibidos.Add(correo);
            await _db.SaveChangesAsync(ct);
            uidsProcesados.Add(c.Uid);

            // Segun el modo del buzon: Manual deja pendiente; Semi/Automatico radica ya (enlazando tercero).
            if (b.ModoRadicacion != BuzonModoRadicacion.Manual && correo.TipoDetectadoId is not null)
            {
                var r = await _correos.RadicarAsync(correo.Id, correo.TipoDetectadoId, false, ct);
                if (r.Ok) { radicados++; }
            }
        }

        // Marca leidos los correos leidos de esta corrida (best-effort).
        await _reader.MarkSeenAsync(ImapParamsDe(b, pass), uidsProcesados, ct);

        var resumen = new CorreoIngestaResumen(true, correos.Count, pqr, descartados, duplicados, errores,
            adjuntosTot, tokensTot, radicados, null);

        // Actualiza el "ultima corrida" del buzon (reusa TiempoEsperaMinutos? no; guarda resumen textual en un campo si existe).
        return resumen;
    }

    private ImapParams ImapParamsDe(BuzonCorreo b, string pass) => new(
        b.Servidor ?? "imap.gmail.com", b.Puerto ?? 993, b.Seguridad == BuzonSeguridad.SslTls,
        b.Usuario, pass, string.IsNullOrWhiteSpace(b.Carpeta) ? "INBOX" : b.Carpeta);

    private static string? SoloDigitos(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : new string(s.Where(char.IsDigit).ToArray()) is { Length: > 0 } d ? d : null;

    /// <summary>Mapea el tipo de la IA (Peticion/Queja/...) a un TipoComunicacion de entrada, preferentemente PQRSD.</summary>
    private static long? MapearTipo(string? tipo, IReadOnlyList<(long Id, string Nombre, bool EsPqrsd)> tipos)
    {
        if (string.IsNullOrWhiteSpace(tipo) || tipos.Count == 0) { return null; }
        var t = tipo.Trim();
        var match = tipos.FirstOrDefault(x => x.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase));
        if (match.Id != 0) { return match.Id; }
        // Si no calza el nombre, cualquier tipo PQRSD sirve de contenedor.
        var pqrsd = tipos.FirstOrDefault(x => x.EsPqrsd);
        return pqrsd.Id != 0 ? pqrsd.Id : null;
    }
}
