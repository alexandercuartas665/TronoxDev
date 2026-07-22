using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Subfondo documental (RQ01 - RF02 seccion 5.2.2). TENANT-SCOPED y OPCIONAL: un fondo puede
/// no tener ninguno. Subdivide un fondo cuando la entidad productora tuvo estructuras internas
/// diferenciadas.
///
/// CodigoSubfondo es unico DENTRO DEL FONDO (no dentro del tenant): dos fondos distintos pueden
/// reutilizar el mismo codigo de subfondo.
/// </summary>
public class Subfondo : TenantEntity
{
    public long FondoId { get; set; }
    public Fondo? Fondo { get; set; }

    /// <summary>Codigo del subfondo. Unico por (TenantId, FondoId).</summary>
    public string CodigoSubfondo { get; set; } = null!;

    public string NombreSubfondo { get; set; } = null!;

    public SubfondoEstado Estado { get; set; } = SubfondoEstado.Activo;
}
