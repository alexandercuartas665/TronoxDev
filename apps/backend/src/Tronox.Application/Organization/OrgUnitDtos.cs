using Tronox.Domain.Enums;

namespace Tronox.Application.Organization;

/// <summary>Unidad plana del organigrama (fila de detalle / edicion).</summary>
public sealed record OrgUnitDto(
    long Id,
    string Name,
    OrgUnitKind Kind,
    long? ParentId,
    string? ParentName,
    long? ResponsibleTenantUserId,
    string? ResponsibleName,
    string? Description,
    int SortOrder,
    bool IsArchived,
    int MemberCount,
    // Asignacion por nodo (ADR-0035): clasificador semantico + usuario ocupante (Funcionario).
    OrgUnitClassifier Classifier = OrgUnitClassifier.Dependencia,
    long? TenantUserId = null,
    string? OccupantName = null);

/// <summary>Nodo del arbol del organigrama (hijos ordenados por SortOrder y nombre).</summary>
public sealed record OrgUnitNodeDto(
    long Id,
    string Name,
    OrgUnitKind Kind,
    long? ParentId,
    long? ResponsibleTenantUserId,
    string? ResponsibleName,
    string? Description,
    int SortOrder,
    bool IsArchived,
    int MemberCount,
    IReadOnlyList<OrgUnitNodeDto> Children,
    // Asignacion por nodo (ADR-0035): clasificador semantico + usuario ocupante (Funcionario).
    OrgUnitClassifier Classifier = OrgUnitClassifier.Dependencia,
    long? TenantUserId = null,
    string? OccupantName = null);

/// <summary>Miembro de una unidad con los datos de presentacion del usuario.</summary>
public sealed record OrgUnitMemberDto(
    long Id,
    long OrgUnitId,
    long TenantUserId,
    string Email,
    string? DisplayName,
    string? Role,
    bool IsResponsible = false);

/// <summary>Alta/edicion de una unidad del organigrama.</summary>
public sealed record SaveOrgUnitRequest(
    string Name,
    OrgUnitKind Kind = OrgUnitKind.Area,
    long? ParentId = null,
    long? ResponsibleTenantUserId = null,
    string? Description = null,
    int SortOrder = 0,
    // Asignacion por nodo (ADR-0035): clasificador + usuario ocupante (solo Funcionario).
    OrgUnitClassifier Classifier = OrgUnitClassifier.Dependencia,
    long? TenantUserId = null);

/// <summary>KPIs de cabecera del modulo (Dependencias / Usuarios / Areas, como el prototipo).</summary>
public sealed record OrgKpisDto(int TotalUnits, int AssignedUsers, int Areas);
