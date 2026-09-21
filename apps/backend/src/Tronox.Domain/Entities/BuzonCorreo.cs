using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Buzon de correo de recepcion para captura y radicacion de comunicaciones por correo (RQ09 RF01-4).
/// La contrasena se guarda cifrada AES-256 (ISecretProtector), nunca en claro (invariante seguridad).
/// El worker que efectivamente jala los correos es una integracion posterior; aqui vive la config.
/// TENANT-SCOPED.
/// </summary>
public class BuzonCorreo : TenantEntity
{
    public string NombreBuzon { get; set; } = null!;

    public string DireccionEmail { get; set; } = null!;

    public BuzonProtocolo Protocolo { get; set; } = BuzonProtocolo.Imap;

    /// <summary>Servidor IMAP (si Protocolo = Imap).</summary>
    public string? Servidor { get; set; }

    public int? Puerto { get; set; }

    public BuzonSeguridad Seguridad { get; set; } = BuzonSeguridad.SslTls;

    public string Usuario { get; set; } = null!;

    /// <summary>Contrasena cifrada AES-256 (nunca en claro). Null = sin cambiar.</summary>
    public string? ContrasenaEncrypted { get; set; }

    public string Carpeta { get; set; } = "INBOX";

    public BuzonFrecuenciaRevision FrecuenciaRevision { get; set; } = BuzonFrecuenciaRevision.Diez;

    public BuzonModoRadicacion ModoRadicacion { get; set; } = BuzonModoRadicacion.SemiAutomatico;

    /// <summary>Solo en modo semi-automatico.</summary>
    public int? TiempoEsperaMinutos { get; set; }

    /// <summary>Tipo de comunicacion si el sistema no lo detecta (FK). NO ACTION.</summary>
    public long? TipoComunicacionDefaultId { get; set; }
    public TipoComunicacion? TipoComunicacionDefault { get; set; }

    /// <summary>Dependencia destino si no se detecta (FK OrgUnit). NO ACTION.</summary>
    public long? DependenciaDefaultId { get; set; }
    public OrgUnit? DependenciaDefault { get; set; }

    public bool Activo { get; set; } = true;
}
