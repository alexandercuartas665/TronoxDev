using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Nodo de la estructura organizacional del tenant (RQ01 - RF03/RF04, ADR-003).
///
/// UN SOLO ARBOL (adjacency list con ParentId autorreferencial) donde cada nodo declara su
/// clasificador: Dependencia, Cargo o Funcionario. NO hay doble clasificacion: el backbone
/// ECOREX arrastraba un "Kind" (Area/Team) superpuesto al clasificador sobre el mismo
/// ParentId, deuda tecnica que el propio ECOREX declaro; TRONOX usa una sola clasificacion.
///
/// Comportamientos que se conservan del backbone:
/// - Self-FK con ON DELETE RESTRICT: un nodo con hijos no se borra en cascada.
/// - Nunca hay borrado fisico: se archiva (IsArchived), y archivar exige no tener
///   descendientes activos (invariante 8).
/// - La validacion de ciclos es pura y fail-closed (OrgUnitTree.WouldCreateCycle).
///
/// TENANT-SCOPED.
/// </summary>
public class OrgUnit : TenantEntity
{
    /// <summary>Nombre del nodo (200). Obligatorio en cualquier clasificador.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Clasificador del nodo (ADR-003). UNICA clasificacion del modelo.</summary>
    public OrgUnitClassifier Classifier { get; set; } = OrgUnitClassifier.Dependencia;

    /// <summary>Nodo padre (null = raiz del organigrama). Jerarquia ILIMITADA en profundidad.</summary>
    public long? ParentId { get; set; }
    public OrgUnit? Parent { get; set; }

    // ---- Atributos de nodo Dependencia (RF03) ----

    /// <summary>
    /// Fondo documental al que pertenece la dependencia (principio de procedencia).
    /// OBLIGATORIO en nodos Dependencia; se ignora (y se persiste en null) en Cargo y
    /// Funcionario. FK NO ACTION: un fondo nunca arrastra dependencias por cascada.
    /// </summary>
    public long? FondoId { get; set; }
    public Fondo? Fondo { get; set; }

    /// <summary>
    /// Codigo de la dependencia (20). UNICO ENTRE HERMANOS bajo el mismo padre dentro del
    /// tenant, NO global: dos ramas distintas del arbol pueden reutilizar el mismo codigo.
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>Inicio de vigencia. OBLIGATORIA en nodos Dependencia.</summary>
    public DateOnly? VigenteDesde { get; set; }

    /// <summary>Fin de vigencia. NULL = la dependencia sigue vigente.</summary>
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>
    /// Dependencia que sucede a esta tras una fusion o reestructuracion (autorreferencial,
    /// nullable). FK NO ACTION: la sucesora nunca arrastra a la sucedida.
    /// </summary>
    public long? SucesoraId { get; set; }
    public OrgUnit? Sucesora { get; set; }

    // ---- Atributos de nodo Cargo (RF04) ----

    /// <summary>Codigo interno del cargo (20). Opcional.</summary>
    public string? CodigoCargo { get; set; }

    /// <summary>Codigo DAFP del cargo (20). Opcional: solo aplica a entidades publicas.</summary>
    public string? CodigoDafp { get; set; }

    /// <summary>
    /// Nivel jerarquico del cargo. OBLIGATORIO en nodos Cargo.
    /// REGLA DE ORO (RF04): el cargo es METADATO ORGANIZACIONAL y NO controla permisos.
    /// Los permisos vienen unicamente de la matriz de RF05.
    /// </summary>
    public NivelJerarquico? NivelJerarquico { get; set; }

    // ---- Comunes ----

    /// <summary>
    /// Usuario del tenant que ocupa este puesto. SOLO se usa cuando Classifier=Funcionario;
    /// null para Dependencia y Cargo. FK NO ACTION.
    /// </summary>
    public long? TenantUserId { get; set; }

    /// <summary>Responsable del nodo (TenantUser del mismo tenant, opcional).</summary>
    public long? ResponsibleTenantUserId { get; set; }

    public string? Description { get; set; }

    /// <summary>Orden entre hermanos dentro del mismo padre.</summary>
    public int SortOrder { get; set; }

    public bool IsArchived { get; set; }
}
