using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Elemento fisico real de la topografia del archivo (RQ02 - RF06 3.6.2). TENANT-SCOPED. Es un nodo
/// del arbol de ubicaciones (adjacency-list por ParentId). Cada elemento pertenece a un
/// TopografiaNivel (Bodega, Estante, ...). Self-ref NO ACTION: inactivar/borrar un padre jamas
/// arrastra a sus hijos por cascada (invariante 8: nunca hay borrado fisico).
///
/// El CODIGO TOPOGRAFICO no se almacena: se calcula en runtime concatenando las siglas desde la
/// raiz hasta el elemento (RF06 3.6.3), mismo patron que el codigo de dependencias/series.
/// </summary>
public class TopografiaElemento : TenantEntity
{
    /// <summary>Tipo de nivel al que pertenece (Bodega, Estante, ...).</summary>
    public long NivelId { get; set; }
    public TopografiaNivel? Nivel { get; set; }

    /// <summary>Padre en el arbol (null = raiz). Self-ref NO ACTION.</summary>
    public long? ParentId { get; set; }
    public TopografiaElemento? Parent { get; set; }
    public ICollection<TopografiaElemento> Children { get; set; } = [];

    /// <summary>Nombre del elemento. Ej: "Bodega Norte", "Estante Metalico 05".</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Sigla propia del elemento. Ej: "NOR", "EST05". Alimenta el codigo topografico.</summary>
    public string Sigla { get; set; } = null!;

    /// <summary>
    /// Numero maximo de unidades. Obligatorio solo si el nivel ControlaCapacidad; null en el resto.
    /// </summary>
    public int? Capacidad { get; set; }

    public TopografiaEstado Estado { get; set; } = TopografiaEstado.Disponible;
}
