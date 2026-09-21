using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Paso del historial de seguimiento de una instancia (RQ11, port de ECOREX).
/// APPEND-ONLY: el motor nunca borra filas ni reescribe pasos cerrados; cada reinicio
/// agrega filas nuevas con CycleIndex+1 (auditoria completa de todos los ciclos).
/// IsCurrent marca el paso que espera atencion (o recien completado sin avanzar aun).
/// TENANT-SCOPED.
///
/// NOTA de port: se omitieron los campos de agente de IA en nodos (ExecutedByAiAgentId,
/// AgentProposal*, AgentFailureReason, AgentAttemptedAt): el submodulo de agentes de ECOREX
/// (ola 2) no se porta en este slice.
/// </summary>
public class WorkflowStepHistory : TenantEntity
{
    public long InstanceId { get; set; }
    public WorkflowInstance? Instance { get; set; }

    public long NodeId { get; set; }
    public WorkflowNode? Node { get; set; }

    /// <summary>Iteracion del loop a la que pertenece el paso (0 = primer ciclo).</summary>
    public int CycleIndex { get; set; }

    /// <summary>El paso esta activo (pendiente de atencion o recien completado sin avanzar).</summary>
    public bool IsCurrent { get; set; }

    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Pending;

    /// <summary>Encargado del paso (TenantUser). Null = sin asignar.</summary>
    public long? AssignedToTenantUserId { get; set; }

    /// <summary>Quien ejecuto/resolvio el paso (puede diferir del asignado).</summary>
    public long? ExecutedByTenantUserId { get; set; }

    /// <summary>Primer nodo de un ciclo abierto por reinicio.</summary>
    public bool IsCycleStart { get; set; }

    /// <summary>Resultado de aprobacion en compuertas (ej. "Approved"/"Rejected").</summary>
    public string? ApprovalResult { get; set; }

    public string? ApprovalComment { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
