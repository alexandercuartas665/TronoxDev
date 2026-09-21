namespace Tronox.Application.Workflows;

/// <summary>
/// Motor de flujos BPMN del tenant activo (RQ11, port del motor BPMN de ECOREX). Importa XML
/// BPMN 2.0 estandar (guardado tal cual, portabilidad bpmn.io), publica versiones, arranca
/// instancias y avanza casos con la semantica heredada: tope de 50 iteraciones, historial
/// append-only, ciclos por RestartNodeId (CycleIndex+1) y hook de reglas autonomas
/// (IWorkflowRuleHook). Todo transaccional y con resultados tipados; cero SQL crudo.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Importa un XML BPMN 2.0 y materializa definicion + nodos + aristas. Valida
    /// exactamente 1 startEvent, al menos 1 endEvent, ids unicos y aristas coherentes.
    /// Si el ProcessCode ya existe, crea una version nueva (max+1) NO publicada.
    /// </summary>
    Task<WorkflowResult<WorkflowDefinitionDto>> ImportBpmnAsync(ImportBpmnRequest request, CancellationToken cancellationToken = default);

    /// <summary>Publica la definicion; despublica cualquier otra version del mismo ProcessCode.</summary>
    Task<WorkflowResult<WorkflowDefinitionDto>> PublishAsync(long definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configura el destino de reinicio de un nodo. Los reinicios no forman parte del XML
    /// BPMN estandar, por eso se configuran tras importar.
    /// </summary>
    Task<WorkflowResult<bool>> SetRestartTargetAsync(long nodeId, long? restartNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Arranca una instancia: crea el caso Running con el paso del startEvent (ciclo 0) y
    /// avanza automaticamente (los startEvent se completan solos), dejando current el/los
    /// siguientes. Detecta si el llamador ya abrio una transaccion y se une a ella.
    /// </summary>
    Task<WorkflowResult<WorkflowInstanceDto>> StartInstanceAsync(
        long definitionId, CancellationToken cancellationToken = default);

    /// <summary>Pasos IsCurrent de la instancia con los datos de su nodo.</summary>
    Task<IReadOnlyList<WorkflowStepDto>> GetCurrentStepsAsync(long instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completa un paso current y Pending (con resultado/comentario de aprobacion para
    /// compuertas) y ejecuta el avance en cascada. StuckDetected si se alcanza el tope de 50.
    /// </summary>
    Task<WorkflowResult<WorkflowInstanceDto>> CompleteStepAsync(
        long instanceId, long stepId, long? executedByTenantUserId,
        string? approvalResult = null, string? approvalComment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rechaza un paso current y REACTIVA el paso anterior creando una fila nueva Pending
    /// del nodo previo (historial append-only: el paso rechazado se conserva).
    /// </summary>
    Task<WorkflowResult<WorkflowInstanceDto>> RejectStepAsync(
        long instanceId, long stepId, long? tenantUserId, string reason,
        CancellationToken cancellationToken = default);
}
