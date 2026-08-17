using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Vinculo (relacion) entre dos expedientes (RQ03 - RF14, legacy EXP_EXPEDIENTES_VINCULOS). Es
/// bidireccional: el vinculo entre A y B se ve desde ambos. Desvincular es logico (Activo=false) para
/// conservar la traza (invariante 8 / RNF-04), a diferencia del DELETE fisico del legacy. TENANT-SCOPED.
/// </summary>
public class ExpedienteVinculo : TenantEntity
{
    public long ExpedienteOrigenId { get; set; }
    public Expediente? ExpedienteOrigen { get; set; }

    public long ExpedienteDestinoId { get; set; }
    public Expediente? ExpedienteDestino { get; set; }

    public string? Observacion { get; set; }

    /// <summary>Vinculo vigente. Desvincular pone false (no se borra fisicamente).</summary>
    public bool Activo { get; set; } = true;
}
