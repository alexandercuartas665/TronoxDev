namespace Tronox.Application.Radicacion;

/// <summary>Datos publicos del portal (branding + tipos publicados) para pintar la pagina publica.</summary>
public sealed record PortalPublicoDto(
    string? NombreEntidad, string? Subtitulo, string? Nit, string? Color, string? Banner, bool PermitirAnonimo,
    bool ExigirCaptcha, string? CanalesAtencion, string? AvisoPrivacidad, int MaxAdjuntoMb, string? Faq,
    IReadOnlyList<TipoPublicoDto> Tipos);

public sealed record TipoPublicoDto(long Id, string Codigo, string Nombre, string? Icono, string? Color,
    bool EsPqrsd, string Termino, string? DescripcionCiudadano);

/// <summary>Solicitud de radicacion desde el portal (props mutables para @bind).</summary>
public sealed class PortalRadicarRequest
{
    public long TipoComunicacionId { get; set; }
    public string? Asunto { get; set; }
    public string? Descripcion { get; set; }
    public bool Anonimo { get; set; }
    public string? RemitenteNombre { get; set; }
    public string? RemitenteTipoDoc { get; set; } = "CC";
    public string? RemitenteDocumento { get; set; }
    public string? RemitenteEmail { get; set; }
    public string? RemitenteTelefono { get; set; }
    /// <summary>Resultado del captcha del cliente (reforzar con reCAPTCHA v3 real).</summary>
    public bool CaptchaOk { get; set; }
}

public sealed record PortalRadicarResult(bool Ok, string? Error = null, string? Numero = null, string? Token = null)
{
    public static PortalRadicarResult Fail(string e) => new(false, e);
    public static PortalRadicarResult Success(string numero, string token) => new(true, null, numero, token);
}

public sealed record PortalConsultaResult(
    bool Ok, string? Error, string? Numero, string? Estado, string? TipoNombre, string? Fecha,
    string? Dependencia, int? DiasRestantes, IReadOnlyList<PortalEventoDto> Timeline, string? RespuestaPublica);

public sealed record PortalEventoDto(string Fecha, string Evento);
