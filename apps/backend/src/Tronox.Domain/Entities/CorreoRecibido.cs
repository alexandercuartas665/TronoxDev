using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Correo capturado de un buzon configurado (RF01-4), pendiente de convertirse en radicado. Espejo del
/// legacy RAD_CORREOS. El Panel de Control cuenta los Pendiente ("Correos por revisar", agrupados por
/// buzon). El modulo completo (bandeja de correos, radicar desde correo) llega al portar rad_correos.aspx.
/// TENANT-SCOPED.
/// </summary>
public class CorreoRecibido : TenantEntity
{
    /// <summary>Buzon del que se capturo (RF01-4). FK a BuzonCorreo. NO ACTION.</summary>
    public long? BuzonCorreoId { get; set; }
    public BuzonCorreo? BuzonCorreo { get; set; }

    /// <summary>Direccion del buzon (denormalizada, como el legacy BUZON_EMAIL: agrupa el KPI).</summary>
    public string? BuzonEmail { get; set; }

    public CorreoRevisionEstado Estado { get; set; } = CorreoRevisionEstado.Pendiente;

    public string? Remitente { get; set; }
    public string? Asunto { get; set; }
    public DateTime? FechaRecepcion { get; set; }

    /// <summary>Radicado generado al procesar el correo (si Estado = Radicado). NO ACTION.</summary>
    public long? RadicadoId { get; set; }
}
