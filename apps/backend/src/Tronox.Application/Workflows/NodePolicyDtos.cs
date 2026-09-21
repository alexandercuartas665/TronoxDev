using Tronox.Domain.Enums;

namespace Tronox.Application.Workflows;

/// <summary>
/// Dependencia/Cargo asignada a un nodo de flujo (fila del panel "Asignar responsables" del
/// editor, RQ11 / ADR-0035). CandidateCount = TenantUserIds distintos que resolveria esta unidad.
/// </summary>
public sealed record NodePolicyDto(
    long PolicyId,
    long OrgUnitId,
    string OrgUnitName,
    OrgUnitClassifier Classifier,
    int CandidateCount);

/// <summary>Unidad candidata a asignar (Dependencia|Cargo) para el selector del editor.</summary>
public sealed record AssignableOrgUnitDto(
    long Id,
    string Name,
    OrgUnitClassifier Classifier,
    long? ParentId,
    int Depth);
