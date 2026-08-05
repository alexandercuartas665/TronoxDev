using Tronox.Domain.Enums;

namespace Tronox.Application.Workflows;

// ---- DTOs del editor de flujos (pantalla 'flujos', RQ11, port BPMN de ECOREX) ----

/// <summary>KPIs del indice (fila de 4 tarjetas).</summary>
public sealed record FlowIndexKpisDto(
    int Flows, int RunningFlows, int ActiveInstances, int MonthExecutions);

/// <summary>
/// Tarjeta del indice: UNA por ProcessCode (la version publicada o, si no hay, la mas
/// reciente). Metricas REALES agregadas sobre TODAS las versiones del proceso:
/// RunningInstances = instancias Running; MonthExecutions = instancias iniciadas en el mes
/// calendario UTC en curso; SuccessRate = Completed / (Completed + Stuck + Cancelled) en %
/// redondeado (las Running no cuentan; 0 si no hay instancias terminadas).
/// </summary>
public sealed record FlowCardDto(
    long DefinitionId, string ProcessCode, int Version, string Name, string? Category,
    string Estado, int NodeCount, int RunningInstances, int MonthExecutions, int SuccessRate);

public sealed record FlowIndexDto(FlowIndexKpisDto Kpis, IReadOnlyList<FlowCardDto> Cards);

/// <summary>Nodo del canvas con layout y apariencia.</summary>
public sealed record FlowCanvasNodeDto(
    long Id, string BpmnElementId, string? Name, WorkflowNodeType NodeType,
    int X, int Y, int W, int H, bool AllowsAssignment, long? RestartNodeId,
    // Apariencia del nodo en el graficador (color de paleta + nota post-it). Metadatos, no viajan en el XML.
    string? Color = null, string? Note = null);

public sealed record FlowCanvasEdgeDto(
    long Id, long SourceNodeId, long TargetNodeId, string? BpmnElementId,
    string? Name, string? ConditionExpression);

/// <summary>
/// Canvas completo de una definicion. IsEditable = !IsPublished (el grafo solo se edita en
/// borradores; editar una publicada pasa por EnsureDraftAsync, que reusa el versionado del motor).
/// </summary>
public sealed record FlowCanvasDto(
    long DefinitionId, string ProcessCode, int Version, string Name, string? Category,
    string? Description, bool IsPublished, bool IsPaused, bool IsArchived,
    string Estado, bool IsEditable,
    IReadOnlyList<FlowCanvasNodeDto> Nodes, IReadOnlyList<FlowCanvasEdgeDto> Edges);
