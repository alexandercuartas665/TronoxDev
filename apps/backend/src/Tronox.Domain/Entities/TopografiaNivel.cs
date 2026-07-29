using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Tipo de nivel de la topografia fisica del archivo (RQ02 - RF06 3.6.1). TENANT-SCOPED. Define la
/// jerarquia personalizable de contenedores fisicos (ej. Bodega > Estante > Entrepano > Caja). Se
/// configura UNA sola vez antes de crear elementos; solo se puede modificar si no existen elementos
/// creados bajo esa estructura (RF06 3.6.6-1).
///
/// Regla: solo UN nivel puede tener ControlaCapacidad = true por tenant, y debe ser el de mayor
/// Orden (el contenedor de menor tamano, ej. la Caja) (RF06 3.6.6-2).
/// </summary>
public class TopografiaNivel : TenantEntity
{
    /// <summary>Nombre del tipo de nivel. Ej: "Bodega", "Estante", "Entrepano", "Caja".</summary>
    public string NombreNivel { get; set; } = null!;

    /// <summary>Base para generar el codigo topografico. Ej: "BOD", "EST", "ENT", "CAJ".</summary>
    public string SiglaBase { get; set; } = null!;

    /// <summary>Jerarquia: 1 = nivel mas alto (contenedor mayor). Unico por tenant.</summary>
    public int Orden { get; set; }

    /// <summary>
    /// Marca el nivel que lleva la cuenta de unidades (capacidad). Solo uno por tenant y debe ser
    /// el de mayor Orden.
    /// </summary>
    public bool ControlaCapacidad { get; set; }
}
