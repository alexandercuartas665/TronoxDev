using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Instancia (caso) de un flujo en ejecucion, anclada a una version concreta de la
/// definicion. Concurrencia optimista portable (Version, patron IVersioned que incrementa
/// el interceptor de auditoria). TENANT-SCOPED.
///
/// NOTA de port (RQ11): ECOREX ligaba la instancia 1:1 a un TaskItem del modulo Tareas
/// (podado en TRONOX). Ese acople se elimino: la instancia corre autonoma. La integracion
/// con "Mis Tareas" (RQ10) es una ola posterior.
/// </summary>
public class WorkflowInstance : TenantEntity, IVersioned
{
    public long DefinitionId { get; set; }
    public WorkflowDefinition? Definition { get; set; }

    public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.Running;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Ciclo mas alto abierto por reinicios (0 = primer ciclo).</summary>
    public int CurrentCycle { get; set; }

    /// <summary>Token de concurrencia optimista portable (lo incrementa el interceptor).</summary>
    public long Version { get; set; }
}
