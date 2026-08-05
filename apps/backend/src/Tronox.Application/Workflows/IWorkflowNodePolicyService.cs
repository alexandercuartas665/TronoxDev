namespace Tronox.Application.Workflows;

/// <summary>
/// Asignacion por nodo (RQ11 / ADR-0035): que Dependencias/Cargos del organigrama atienden un
/// paso Task del flujo. NO se asignan usuarios directos; el resolver (INodeAssigneeResolver)
/// expande cada unidad a sus TenantUserIds candidatos. Solo se admiten unidades con Classifier
/// Dependencia o Cargo (un Funcionario nunca es asignable). Tenant-scoped por el filtro global.
/// </summary>
public interface IWorkflowNodePolicyService
{
    /// <summary>Dependencias/Cargos asignados al nodo, con el conteo de candidatos resueltos.</summary>
    Task<IReadOnlyList<NodePolicyDto>> ListNodePoliciesAsync(long nodeId, CancellationToken cancellationToken = default);

    /// <summary>Unidades asignables (Classifier Dependencia|Cargo, no archivadas) para el selector.</summary>
    Task<IReadOnlyList<AssignableOrgUnitDto>> ListAssignableUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>Asigna una Dependencia|Cargo al nodo. Rechaza Funcionario y duplicados.</summary>
    Task<WorkflowResult<NodePolicyDto>> AddNodePolicyAsync(long nodeId, long orgUnitId, CancellationToken cancellationToken = default);

    Task<WorkflowResult<bool>> RemoveNodePolicyAsync(long policyId, CancellationToken cancellationToken = default);
}
