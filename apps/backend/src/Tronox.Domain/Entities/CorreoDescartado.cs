using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Log inmutable de descarte de un correo (RAD_CORREOS_DESCARTADOS). Append-only (RNF-04): recuperar NO
/// borra el registro, marca Recuperado=true. Causal obligatoria. TENANT-SCOPED. Cascade con el correo.
/// </summary>
public class CorreoDescartado : TenantEntity
{
    public long CorreoRecibidoId { get; set; }
    public CorreoRecibido? CorreoRecibido { get; set; }

    /// <summary>Usuario que descarto (TenantUser). NO ACTION.</summary>
    public long? UsuarioId { get; set; }

    public string Causal { get; set; } = null!;
    public DateTime Fecha { get; set; }

    public bool Recuperado { get; set; }
    public DateTime? FechaRecupera { get; set; }
    /// <summary>Usuario que recupero (TenantUser). NO ACTION.</summary>
    public long? UsuarioRecuperaId { get; set; }
}
