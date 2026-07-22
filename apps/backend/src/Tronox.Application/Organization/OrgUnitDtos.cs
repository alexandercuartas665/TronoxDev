using Tronox.Domain.Enums;

namespace Tronox.Application.Organization;

/// <summary>
/// Nodo plano del arbol organizacional (fila de detalle / edicion). UNA sola clasificacion
/// (Classifier); el Kind del backbone no se replica (ADR-003).
/// </summary>
public sealed record OrgUnitDto(
    long Id,
    string Name,
    OrgUnitClassifier Classifier,
    long? ParentId,
    string? ParentName,
    long? ResponsibleTenantUserId,
    string? ResponsibleName,
    string? Description,
    int SortOrder,
    bool IsArchived,
    int MemberCount,
    long? TenantUserId = null,
    string? OccupantName = null,
    // Atributos de Dependencia (RF03).
    long? FondoId = null,
    string? FondoNombre = null,
    string? Codigo = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null,
    long? SucesoraId = null,
    // Atributos de Cargo (RF04). NivelJerarquico es metadato, NO controla permisos.
    string? CodigoCargo = null,
    string? CodigoDafp = null,
    NivelJerarquico? NivelJerarquico = null);

/// <summary>Nodo del arbol organizacional (hijos ordenados por SortOrder y nombre).</summary>
public sealed record OrgUnitNodeDto(
    long Id,
    string Name,
    OrgUnitClassifier Classifier,
    long? ParentId,
    long? ResponsibleTenantUserId,
    string? ResponsibleName,
    string? Description,
    int SortOrder,
    bool IsArchived,
    int MemberCount,
    IReadOnlyList<OrgUnitNodeDto> Children,
    long? TenantUserId = null,
    string? OccupantName = null,
    long? FondoId = null,
    string? Codigo = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null,
    long? SucesoraId = null,
    string? CodigoCargo = null,
    string? CodigoDafp = null,
    NivelJerarquico? NivelJerarquico = null);

/// <summary>Miembro de un nodo con los datos de presentacion del usuario.</summary>
public sealed record OrgUnitMemberDto(
    long Id,
    long OrgUnitId,
    long TenantUserId,
    string Email,
    string? DisplayName,
    string? Role,
    bool IsResponsible = false);

/// <summary>Alta/edicion de un nodo del arbol organizacional.</summary>
public sealed record SaveOrgUnitRequest(
    string Name,
    OrgUnitClassifier Classifier = OrgUnitClassifier.Dependencia,
    long? ParentId = null,
    long? ResponsibleTenantUserId = null,
    string? Description = null,
    int SortOrder = 0,
    long? TenantUserId = null,
    // Dependencia (RF03): FondoId, Codigo y VigenteDesde son obligatorios en este clasificador.
    long? FondoId = null,
    string? Codigo = null,
    DateOnly? VigenteDesde = null,
    DateOnly? VigenteHasta = null,
    long? SucesoraId = null,
    // Cargo (RF04): NivelJerarquico obligatorio; los codigos son opcionales.
    string? CodigoCargo = null,
    string? CodigoDafp = null,
    NivelJerarquico? NivelJerarquico = null);

/// <summary>
/// Resultado de reubicar un nodo Cargo (ADR-003, Addendum punto 2). Mover un Cargo cambia la
/// visibilidad documental de TODOS sus ocupantes sin que nadie edite esos usuarios, asi que el
/// servicio devuelve cuantos quedan afectados para que la UI pueda avisar antes/despues, y
/// deja el movimiento en la pista de auditoria.
/// </summary>
/// <param name="AffectedUserCount">
/// Usuarios anclados a este Cargo o a cualquier nodo de su subarbol (los sub-cargos se mueven
/// con el), cuya dependencia derivada cambia por este movimiento.
/// </param>
public sealed record MoveCargoResultDto(
    long UnitId,
    long? PreviousParentId,
    long? NewParentId,
    long? PreviousDependenciaId,
    long? NewDependenciaId,
    int AffectedUserCount);

/// <summary>KPIs de cabecera del modulo: nodos activos, usuarios asignados y dependencias.</summary>
public sealed record OrgKpisDto(int TotalUnits, int AssignedUsers, int Dependencias);
