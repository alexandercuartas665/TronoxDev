using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del ciclo de envio de salidas (RF05-6). Valida por canal, envia el PDF por correo en
/// EMAIL (IEmailSender + object storage), registra la comunicacion y la trazabilidad, y marca el estado
/// de envio del radicado. Resultado tipado, todo en una transaccion. Constancia/acta como HTML imprimible.
/// </summary>
public sealed class RadicacionEnvioService : IRadicacionEnvioService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IObjectStorage _storage;
    private readonly IEmailSender _email;

    public RadicacionEnvioService(IApplicationDbContext db, ITenantContext tenant, IObjectStorage storage, IEmailSender email)
    {
        _db = db;
        _tenant = tenant;
        _storage = storage;
        _email = email;
    }

    public async Task<RegistrarEnvioResult> RegistrarEnvioAsync(RegistrarEnvioRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return RegistrarEnvioResult.Fail("Sesion no valida."); }

        var radicado = await _db.Radicados.Include(r => r.Archivos)
            .FirstOrDefaultAsync(r => r.Id == req.RadicadoId, ct);
        if (radicado is null) { return RegistrarEnvioResult.Fail("Radicado no encontrado."); }
        if (radicado.Tipo != RadicadoTipo.Salida) { return RegistrarEnvioResult.Fail("Solo las salidas registran envio."); }

        var canal = (req.Canal ?? "").Trim().ToUpperInvariant();
        var (destino, detalle, error) = ValidarCanal(canal, req);
        if (error is not null) { return RegistrarEnvioResult.Fail(error); }

        var estado = "Enviado";

        // Canal EMAIL: envia el documento principal (o el primero) adjunto por correo.
        if (canal == "EMAIL")
        {
            var principal = radicado.Archivos.FirstOrDefault();
            if (principal?.StorageKey is null)
            {
                return RegistrarEnvioResult.Fail("La salida no tiene documento para enviar por correo.");
            }
            byte[]? bytes = null;
            await using (var s = await _storage.GetAsync(principal.StorageKey, ct))
            {
                if (s is not null)
                {
                    using var ms = new MemoryStream();
                    await s.CopyToAsync(ms, ct);
                    bytes = ms.ToArray();
                }
            }
            if (bytes is null) { return RegistrarEnvioResult.Fail("No se pudo leer el documento principal."); }

            var asunto = $"[{radicado.NumeroRadicado}] {radicado.Asunto}".Trim();
            var cuerpo = $"<p>Cordial saludo,</p><p>Adjuntamos el documento radicado " +
                $"<b>{radicado.NumeroRadicado}</b>" +
                (radicado.RadicadoRelacionadoId is not null ? " en respuesta a su radicado." : ".") +
                "</p><p>Este es un envio automatico del Sistema de Gestion Documental (TRONOX SGDEA).</p>";
            var res = await _email.SendWithAttachmentAsync(new[] { destino! }, asunto, cuerpo,
                bytes, principal.Nombre, principal.MimeType ?? "application/pdf", ct);
            estado = res.Ok ? "Enviado" : "Fallido";
            if (!res.Ok) { detalle += $" (fallo el envio: {res.Error})"; }
        }

        var ahora = DateTime.UtcNow;
        _db.RadicadosComunicaciones.Add(new RadicadoComunicacion
        {
            TenantId = tenantId.Value,
            RadicadoId = radicado.Id,
            Fecha = ahora,
            UsuarioId = _tenant.UserId,
            Canal = canal,
            Destino = destino,
            Asunto = radicado.Asunto,
            Detalle = detalle,
            Estado = estado
        });

        radicado.EstadoEnvio = estado;
        radicado.Trazas.Add(new RadicadoTrazabilidad
        {
            TenantId = tenantId.Value,
            RadicadoId = radicado.Id,
            Accion = "ENVIO",
            Fecha = ahora,
            UsuarioId = _tenant.UserId,
            Detalle = $"Envio {estado.ToLowerInvariant()} por {canal}. {detalle}".Trim()
        });

        await _db.SaveChangesAsync(ct);
        return estado == "Fallido"
            ? new RegistrarEnvioResult(false, "El correo no pudo enviarse; el envio quedo como Fallido.", estado)
            : RegistrarEnvioResult.Success(estado);
    }

    private static (string? destino, string detalle, string? error) ValidarCanal(string canal, RegistrarEnvioRequest req) => canal switch
    {
        "EMAIL" => string.IsNullOrWhiteSpace(req.Correo)
            ? (null, "", "El correo de destino es obligatorio.")
            : (req.Correo!.Trim(), $"Enviado por correo a {req.Correo!.Trim()}.", null),
        "FISICO" => string.IsNullOrWhiteSpace(req.Guia)
            ? (null, "", "El numero de guia es obligatorio.")
            : (req.Guia!.Trim(), $"Correo fisico, guia {req.Guia!.Trim()}.", null),
        "PERSONAL" => string.IsNullOrWhiteSpace(req.Recibe)
            ? (null, "", "El nombre de quien recibe es obligatorio.")
            : (req.Recibe!.Trim(), $"Entrega personal, recibe {req.Recibe!.Trim()}.", null),
        "JUDICIAL" => (string.IsNullOrWhiteSpace(req.Juzgado) || string.IsNullOrWhiteSpace(req.Expediente) || req.FechaNotificacion is null)
            ? (null, "", "Juzgado, expediente y fecha de notificacion son obligatorios.")
            : ($"{req.Juzgado!.Trim()} - Exp. {req.Expediente!.Trim()}",
               $"Notificacion judicial: {req.Juzgado!.Trim()}, expediente {req.Expediente!.Trim()}, fecha {req.FechaNotificacion:dd/MM/yyyy}.", null),
        _ => (null, "", "Canal de envio no valido.")
    };

    public async Task<string?> ConstanciaHtmlAsync(long radicadoId, CancellationToken ct = default)
    {
        var r = await _db.Radicados.AsNoTracking()
            .Where(x => x.Id == radicadoId && x.Tipo == RadicadoTipo.Salida)
            .Select(x => new
            {
                x.NumeroRadicado, x.Asunto, x.FechaRadicacion, x.RemitenteNombre, x.CanalEnvio, x.EstadoEnvio,
                Padre = x.RadicadoRelacionado != null ? x.RadicadoRelacionado.NumeroRadicado : null,
                x.EsRespuestaDefinitiva
            })
            .FirstOrDefaultAsync(ct);
        if (r is null) { return null; }

        var com = await _db.RadicadosComunicaciones.AsNoTracking()
            .Where(c => c.RadicadoId == radicadoId)
            .OrderByDescending(c => c.Fecha)
            .Select(c => new { c.Canal, c.Destino, c.Detalle, c.Estado, c.Fecha })
            .FirstOrDefaultAsync(ct);

        var canal = (com?.Canal ?? r.CanalEnvio ?? "").ToUpperInvariant();
        var titulo = canal == "JUDICIAL" ? "ACTA DE NOTIFICACION JUDICIAL" : "CONSTANCIA DE ENVIO";
        static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        var filas = new System.Text.StringBuilder();
        void Row(string k, string? v) => filas.Append($"<tr><td class='k'>{Esc(k)}</td><td>{Esc(v)}</td></tr>");
        Row("Radicado de salida", r.NumeroRadicado);
        Row("Fecha de radicacion", r.FechaRadicacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        Row("Destinatario", r.RemitenteNombre);
        Row("Asunto", r.Asunto);
        Row("Canal de envio", canal);
        if (com is not null)
        {
            Row("Detalle del envio", com.Detalle);
            Row("Estado de envio", com.Estado);
            Row("Fecha de envio", com.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        }
        if (r.Padre is not null)
        {
            Row("Radicado de entrada vinculado", $"{r.Padre} ({(r.EsRespuestaDefinitiva ? "respuesta definitiva" : "salida parcial")})");
        }

        return $@"<!DOCTYPE html><html lang='es'><head><meta charset='utf-8'><title>{Esc(titulo)} {Esc(r.NumeroRadicado)}</title>
<style>body{{font-family:Arial,Helvetica,sans-serif;color:#212529;margin:32px;}}
h1{{background:#405189;color:#fff;padding:12px 16px;font-size:16px;border-radius:6px;letter-spacing:.04em;}}
table{{width:100%;border-collapse:collapse;margin-top:16px;font-size:13px;}}
td{{padding:8px 10px;border-bottom:1px solid #E2E8F0;vertical-align:top;}}
td.k{{width:220px;color:#566980;font-weight:700;}}
.firmas{{display:flex;gap:60px;margin-top:64px;}}
.firma{{flex:1;border-top:1px solid #212529;padding-top:6px;font-size:12px;text-align:center;color:#566980;}}
</style></head><body>
<h1>{Esc(titulo)}</h1>
<table>{filas}</table>
<div class='firmas'><div class='firma'>Firma de quien entrega</div><div class='firma'>Firma de quien recibe</div></div>
</body></html>";
    }
}
