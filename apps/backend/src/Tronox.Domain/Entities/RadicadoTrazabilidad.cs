using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Pista de auditoria de un radicado (RAD_TRAZABILIDAD del legacy). Append-only (RNF-04): ningun rol la
/// edita ni borra. El panel la usa para el cumplimiento SLA y el tiempo promedio de respuesta: la primera
/// traza con Accion = "RESPONDIDO" marca la fecha de respuesta. TENANT-SCOPED. Cascade con el radicado.
/// </summary>
public class RadicadoTrazabilidad : TenantEntity
{
    public long RadicadoId { get; set; }
    public Radicado? Radicado { get; set; }

    /// <summary>Accion registrada (RADICADO, DISTRIBUIDO, RESPONDIDO, ARCHIVADO, ...). Texto abierto.</summary>
    public string Accion { get; set; } = null!;

    public DateTime Fecha { get; set; }

    /// <summary>Usuario (TenantUser) que ejecuto la accion. NO ACTION.</summary>
    public long? UsuarioId { get; set; }

    public string? Detalle { get; set; }
}
