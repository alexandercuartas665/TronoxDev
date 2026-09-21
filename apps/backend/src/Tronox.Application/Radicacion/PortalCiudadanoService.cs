using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion del portal ciudadano. El tenant se resuelve por slug (IgnoreQueryFilters). El radicar
/// delega en RadicadorService (canal WEB) y agrega el token de seguimiento; la consulta solo expone datos
/// publicos. Quirks del legacy NO replicados: tenant por query manipulable, SQL concatenado, captcha
/// casero, rate-limit en memoria, BLOB. reCAPTCHA v3 real y rate-limit Redis quedan como refuerzo.
/// </summary>
public sealed class PortalCiudadanoService : IPortalCiudadanoService
{
    private readonly IApplicationDbContext _db;
    private readonly IRadicadorService _radicador;

    public PortalCiudadanoService(IApplicationDbContext db, IRadicadorService radicador)
    {
        _db = db;
        _radicador = radicador;
    }

    public async Task<long?> ResolverTenantAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) { return null; }
        var s = slug.Trim().ToLowerInvariant();
        return await _db.RadPortalConfigs.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Slug == s).Select(p => (long?)p.TenantId).FirstOrDefaultAsync(ct);
    }

    public async Task<PortalPublicoDto?> GetPortalAsync(CancellationToken ct = default)
    {
        var c = await _db.RadPortalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (c is null) { return null; }
        var tiposRaw = await _db.TiposComunicacion.AsNoTracking()
            .Where(t => t.Activo && t.Direccion == RadicacionDireccion.Entrada && t.HabilitadoWeb)
            .OrderBy(t => t.OrdenPortal ?? 999).ThenBy(t => t.Nombre)
            .Select(t => new { t.Id, t.Codigo, t.Nombre, t.Icono, t.Color, t.EsPqrsd, t.RequiereRespuesta, t.DiasRespuesta, t.TipoDia, t.DescripcionCiudadano })
            .ToListAsync(ct);
        var tipos = tiposRaw.Select(t => new TipoPublicoDto(
            t.Id, t.Codigo, t.Nombre, t.Icono, t.Color, t.EsPqrsd,
            t.RequiereRespuesta && t.DiasRespuesta is int d
                ? $"Respuesta: {d} dias {(t.TipoDia?.ToString() ?? "habiles").ToLowerInvariant()}"
                : "Sin termino de respuesta",
            t.DescripcionCiudadano)).ToList();
        return new PortalPublicoDto(c.NombreEntidad, c.Subtitulo, c.Nit, c.Color, c.Banner, c.PermitirAnonimo,
            c.ExigirCaptcha, c.CanalesAtencion, c.AvisoPrivacidad, c.MaxAdjuntoMb, c.Faq, tipos);
    }

    public async Task<PortalRadicarResult> RadicarAsync(PortalRadicarRequest req, CancellationToken ct = default)
    {
        var portal = await _db.RadPortalConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        if (portal is null) { return PortalRadicarResult.Fail("Portal no disponible."); }
        if (portal.ExigirCaptcha && !req.CaptchaOk) { return PortalRadicarResult.Fail("Debe validar el captcha."); }
        if (req.Anonimo && !portal.PermitirAnonimo) { return PortalRadicarResult.Fail("Este portal no permite radicacion anonima."); }
        if (!req.Anonimo && (string.IsNullOrWhiteSpace(req.RemitenteNombre) || string.IsNullOrWhiteSpace(req.RemitenteDocumento)))
        {
            return PortalRadicarResult.Fail("Nombre y documento son obligatorios (o marque anonimo).");
        }

        var tipo = await _db.TiposComunicacion.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.TipoComunicacionId && t.HabilitadoWeb && t.Activo, ct);
        if (tipo is null) { return PortalRadicarResult.Fail("El tipo seleccionado no esta disponible en el portal."); }

        var res = await _radicador.RadicarAsync(new RadicarNuevoRequest(
            Tipo: RadicadoTipo.Entrada,
            TipoComunicacionId: req.TipoComunicacionId,
            Asunto: req.Asunto,
            Descripcion: req.Descripcion,
            Canal: RadicadoCanal.Web,
            Anonimo: req.Anonimo,
            RemitenteNombre: req.RemitenteNombre,
            RemitenteEmail: req.RemitenteEmail,
            RemitenteTipoDoc: req.RemitenteTipoDoc,
            RemitenteDocumento: req.RemitenteDocumento,
            RemitenteTelefono: req.RemitenteTelefono,
            Soporte: "Electronico"), ct);
        if (!res.Ok || res.RadicadoId is null) { return PortalRadicarResult.Fail(res.Error ?? "No se pudo radicar."); }

        // Token de seguimiento (para consulta publica).
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var radicado = await _db.Radicados.FirstOrDefaultAsync(r => r.Id == res.RadicadoId, ct);
        if (radicado is not null) { radicado.PortalToken = token; await _db.SaveChangesAsync(ct); }

        return PortalRadicarResult.Success(res.Numero!, token);
    }

    private static readonly RadicadoEstado[] EstadosPublicos =
    {
        RadicadoEstado.Radicado, RadicadoEstado.Distribuido, RadicadoEstado.EnTramite, RadicadoEstado.Respondido, RadicadoEstado.Archivado
    };

    public async Task<PortalConsultaResult> ConsultarAsync(string numero, string documento, CancellationToken ct = default)
    {
        PortalConsultaResult Fail(string e) => new(false, e, null, null, null, null, null, null, Array.Empty<PortalEventoDto>(), null);
        if (string.IsNullOrWhiteSpace(numero) || string.IsNullOrWhiteSpace(documento)) { return Fail("Ingrese numero de radicado y documento."); }

        var r = await _db.Radicados.AsNoTracking()
            .Where(x => x.NumeroRadicado == numero.Trim() && !x.Anonimo && x.RemitenteDocumento == documento.Trim())
            .Select(x => new
            {
                x.Id, x.NumeroRadicado, x.Estado, x.FechaRadicacion, x.FechaVencimiento,
                x.RespuestaPublica, x.EsRespuestaPublica,
                TipoNombre = x.TipoComunicacion != null ? x.TipoComunicacion.Nombre : null,
                Dependencia = x.DependenciaDestino != null ? x.DependenciaDestino.Name : null
            }).FirstOrDefaultAsync(ct);
        if (r is null) { return Fail("No se encontro un radicado con ese numero y documento."); }

        var hoy = DateTime.UtcNow.Date;
        int? dias = r.FechaVencimiento is null ? null : (int)(r.FechaVencimiento.Value.Date - hoy).TotalDays;

        // Timeline publico: solo acciones visibles al ciudadano.
        var trazas = await _db.RadicadosTrazabilidad.AsNoTracking()
            .Where(t => t.RadicadoId == r.Id && (t.Accion == "RADICADO" || t.Accion == "DISTRIBUIR" || t.Accion == "RESPONDIDO"))
            .OrderBy(t => t.Fecha)
            .Select(t => new { t.Fecha, t.Accion }).ToListAsync(ct);
        var timeline = trazas.Select(t => new PortalEventoDto(t.Fecha.ToString("dd/MM/yyyy"), EventoPublico(t.Accion))).ToList();

        return new PortalConsultaResult(true, null, r.NumeroRadicado, EstadoPublico(r.Estado), r.TipoNombre,
            r.FechaRadicacion.ToString("dd/MM/yyyy"), r.Dependencia, dias, timeline,
            r.EsRespuestaPublica ? r.RespuestaPublica : null);
    }

    private static string EstadoPublico(RadicadoEstado e) => e switch
    {
        RadicadoEstado.Radicado => "Radicado",
        RadicadoEstado.Distribuido => "En distribucion",
        RadicadoEstado.EnTramite => "En tramite",
        RadicadoEstado.Respondido => "Respondido",
        RadicadoEstado.Archivado => "Archivado",
        _ => "En proceso"
    };

    private static string EventoPublico(string accion) => accion switch
    {
        "RADICADO" => "Su solicitud fue radicada",
        "DISTRIBUIR" => "Asignada a la dependencia competente",
        "RESPONDIDO" => "Se emitio respuesta",
        _ => accion
    };
}
