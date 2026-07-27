using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Serie o subserie del CATALOGO documental (RQ02 - RF02). TENANT-SCOPED (filtro global por
/// TenantId). Es el LISTADO MAESTRO INTELECTUAL de la entidad: independiente de las dependencias
/// que las producen. Aqui NO se vinculan dependencias, NO se asignan tiempos de retencion ni
/// clasificacion documental; eso ocurre en RF04 (cruce Dependencia + Serie) que produce el CCD y
/// la TRD. Este catalogo es el insumo de RF04.
///
/// Jerarquia ILIMITADA por auto-relacion (adjacency-list): ParentId null = Serie principal; con
/// valor = Subserie (o sub-subserie, sin limite de profundidad). Self-ref NO ACTION: inactivar o
/// borrar un padre jamas arrastra a sus hijos por cascada (invariante 8: nunca hay borrado fisico).
///
/// Equivale a la tabla legacy GEN_TRD_CATALOGO_SERIES de doc_catalogoTRD.aspx.
/// </summary>
public class SerieDocumental : TenantEntity
{
    /// <summary>
    /// Codigo archivistico. Ej: "01", "01.1", "01.1.1". UNICO POR NIVEL JERARQUICO dentro del
    /// tenant (RF02 criterio 2): el mismo codigo puede existir bajo padres distintos, pero no
    /// entre hermanos.
    /// </summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Nombre de la serie/subserie. Ej: "Actas", "Historias Laborales".</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Descripcion archivistica (opcional).</summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Padre en el arbol (null = Serie principal; con valor = Subserie). Self-ref NO ACTION.
    /// </summary>
    public long? ParentId { get; set; }
    public SerieDocumental? Parent { get; set; }
    public ICollection<SerieDocumental> Children { get; set; } = [];

    public SerieEstado Estado { get; set; } = SerieEstado.Activo;
}
