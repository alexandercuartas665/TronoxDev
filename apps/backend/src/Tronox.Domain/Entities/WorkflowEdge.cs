using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Arista (sequenceFlow BPMN) entre dos nodos de una definicion de flujo (RQ11, port de
/// ECOREX). En compuertas exclusivas, ConditionExpression decide la rama; una arista sin
/// condicion es la rama por defecto. TENANT-SCOPED.
/// </summary>
public class WorkflowEdge : TenantEntity
{
    public long DefinitionId { get; set; }
    public WorkflowDefinition? Definition { get; set; }

    public long SourceNodeId { get; set; }
    public WorkflowNode? SourceNode { get; set; }

    public long TargetNodeId { get; set; }
    public WorkflowNode? TargetNode { get; set; }

    /// <summary>Id del sequenceFlow en el XML BPMN (ej. "Flow_0wlkxd6").</summary>
    public string? BpmnElementId { get; set; }

    public string? Name { get; set; }

    /// <summary>
    /// Expresion simple evaluada contra el resultado de aprobacion del paso origen
    /// (formato soportado: "approval == 'Approved'" / "approval != 'X'"; vacio = default).
    /// </summary>
    public string? ConditionExpression { get; set; }
}
