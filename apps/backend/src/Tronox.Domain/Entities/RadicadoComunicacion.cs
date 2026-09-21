using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Comunicacion/envio de un radicado (RAD_COMUNICACIONES): el log de envios que muestra la pestana
/// Comunicaciones del detalle. Se puebla al portar rad_salida (registro de envio); por ahora la tabla
/// existe para que el detalle la lea (vacia). TENANT-SCOPED. Cascade con el radicado.
/// </summary>
public class RadicadoComunicacion : TenantEntity
{
    public long RadicadoId { get; set; }
    public Radicado? Radicado { get; set; }

    public DateTime Fecha { get; set; }

    /// <summary>Usuario que registro el envio (TenantUser). NO ACTION.</summary>
    public long? UsuarioId { get; set; }

    /// <summary>Canal de envio (EMAIL, FISICO, PERSONAL, JUDICIAL...).</summary>
    public string? Canal { get; set; }

    public string? Destino { get; set; }
    public string? Asunto { get; set; }
    public string? Detalle { get; set; }

    /// <summary>Estado del envio (Enviado / Fallido / ...). Texto acotado.</summary>
    public string? Estado { get; set; }
}
