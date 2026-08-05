namespace Tronox.Application.Workflows;

/// <summary>
/// Resuelve los TenantUserIds candidatos que pueden atender un nodo Task del flujo, segun las
/// unidades del organigrama asignadas al nodo (WorkflowNodePolicy). Logica de arbol delegada a
/// OrgAssigneeTree (pura). RQ11, port de ECOREX ADR-0035.
/// </summary>
public interface INodeAssigneeResolver
{
    Task<IReadOnlyList<long>> ResolveCandidatesAsync(long workflowNodeId, CancellationToken cancellationToken = default);
}
