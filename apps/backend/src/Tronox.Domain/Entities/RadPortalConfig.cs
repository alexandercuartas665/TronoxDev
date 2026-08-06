using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion del portal ciudadano de radicacion (RAD_PORTAL_CONFIG, RQ09 RF03-1). Singleton por
/// tenant. Branding + reglas del portal publico (radicar PQRSD + consultar). Los tipos que se publican
/// al ciudadano se controlan con TipoComunicacion.HabilitadoWeb (no aqui). <see cref="Slug"/> resuelve el
/// tenant server-side en la URL publica (no por query manipulable, invariante 1, ADR). TENANT-SCOPED.
/// </summary>
public class RadPortalConfig : TenantEntity
{
    public string? NombreEntidad { get; set; }
    public string? Subtitulo { get; set; }
    public string? Nit { get; set; }
    /// <summary>Color institucional HEX.</summary>
    public string? Color { get; set; }
    public int MaxAdjuntoMb { get; set; } = 20;
    public string? Banner { get; set; }
    public bool PermitirAnonimo { get; set; } = true;
    public bool ExigirCaptcha { get; set; } = true;
    public string? CanalesAtencion { get; set; }
    /// <summary>Aviso de privacidad (Ley 1581/2012).</summary>
    public string? AvisoPrivacidad { get; set; }
    /// <summary>Preguntas frecuentes ("Pregunta|Respuesta" por linea).</summary>
    public string? Faq { get; set; }

    /// <summary>Slug publico del portal (resuelve el tenant server-side en /portal/{slug}). Unico global.</summary>
    public string? Slug { get; set; }
}
