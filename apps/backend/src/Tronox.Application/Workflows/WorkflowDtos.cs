using Tronox.Domain.Enums;

namespace Tronox.Application.Workflows;

// ---- WorkflowEngine (RQ11, port BPMN de ECOREX) ----

/// <summary>Solicitud de importacion de un XML BPMN 2.0 estandar (se guarda tal cual).</summary>
public sealed record ImportBpmnRequest(
    string ProcessCode,
    string Name,
    string BpmnXml,
    string? Description = null);

public sealed record WorkflowNodeDto(
    long Id, string BpmnElementId, string? Name, WorkflowNodeType NodeType,
    int? StepNumber, bool AllowsAssignment, long? RestartNodeId);

public sealed record WorkflowEdgeDto(
    long Id, long SourceNodeId, long TargetNodeId, string? BpmnElementId,
    string? Name, string? ConditionExpression);

public sealed record WorkflowDefinitionDto(
    long Id, string ProcessCode, string Name, string? Description, int Version,
    bool IsPublished, bool IsArchived,
    IReadOnlyList<WorkflowNodeDto> Nodes, IReadOnlyList<WorkflowEdgeDto> Edges);

/// <summary>Paso del historial con los datos de su nodo (para bandejas y tests).</summary>
public sealed record WorkflowStepDto(
    long Id, long InstanceId, long NodeId, string BpmnElementId, string? NodeName,
    WorkflowNodeType NodeType, int CycleIndex, bool IsCurrent, bool IsCycleStart,
    WorkflowStepStatus Status, long? AssignedToTenantUserId, long? ExecutedByTenantUserId,
    string? ApprovalResult, string? ApprovalComment, DateTimeOffset? CompletedAt);

public sealed record WorkflowInstanceDto(
    long Id, long DefinitionId, WorkflowInstanceStatus Status,
    DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, int CurrentCycle,
    IReadOnlyList<WorkflowStepDto> CurrentSteps);
