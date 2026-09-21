using Tronox.Domain.Enums;

namespace Tronox.Application.Workflows;

/// <summary>
/// Servicio de DISENO de flujos para el editor canvas bpmn-js (RQ11, port de ECOREX ADR-0022/
/// 0034). Complementa a IWorkflowEngine (que importa/publica/ejecuta): aqui viven el indice con
/// metricas reales y las mutaciones del grafo. REGLAS CLAVE:
/// - El grafo solo se edita en definiciones NO publicadas (borradores). Para editar una
///   publicada, EnsureDraftAsync crea (o reutiliza) la version borrador siguiente por el
///   camino de versionado del motor y copia lo que no viaja en el XML (Category, RestartNodeId,
///   AllowsAssignment, apariencia, asignaciones por nodo).
/// - El guardado desde bpmn-js (SaveBpmnAsync) re-sincroniza nodos/aristas desde el XML del
///   modeler; la parametrizacion por nodo que NO viaja en el XML se conserva por BpmnElementId.
///
/// NOTA de port: se omitieron los vinculos por nodo de formulario, reglas y agentes de IA
/// (submodulos podados/diferidos en TRONOX).
/// </summary>
public interface IWorkflowDesignService
{
    // ---- Indice ----

    /// <summary>KPIs y tarjetas del indice /flujos (metricas reales).</summary>
    Task<FlowIndexDto> ListForIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Canvas completo (nodos con coordenadas + aristas) o null.</summary>
    Task<FlowCanvasDto?> GetCanvasAsync(long definitionId, CancellationToken cancellationToken = default);

    /// <summary>XML BPMN 2.0 crudo de la definicion (para hidratar bpmn-js), o null.</summary>
    Task<string?> GetBpmnXmlAsync(long definitionId, CancellationToken cancellationToken = default);

    /// <summary>"Nuevo flujo": crea un borrador minimo startEvent -> endEvent.</summary>
    Task<WorkflowResult<FlowCanvasDto>> CreateDraftAsync(string name, string? category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve la definicion tal cual si es borrador; si esta publicada, devuelve (o crea,
    /// reusando el versionado del motor) la version borrador siguiente.
    /// </summary>
    Task<WorkflowResult<FlowCanvasDto>> EnsureDraftAsync(long definitionId, CancellationToken cancellationToken = default);

    // ---- Guardado desde bpmn-js (el editor produce el XML completo) ----

    /// <summary>
    /// Guarda el grafo dibujado en bpmn-js: re-sincroniza nodos/aristas y el layout (bpmndi)
    /// sobre la definicion borrador, a partir del XML del modeler. Si la definicion esta
    /// publicada, primero deriva/reusa la version borrador (EnsureDraft) y devuelve su Id. La
    /// parametrizacion por nodo (AllowsAssignment, RestartNodeId, apariencia) NO viaja en el
    /// XML: se conserva emparejando por BpmnElementId.
    /// </summary>
    Task<WorkflowResult<FlowCanvasDto>> SaveBpmnAsync(long definitionId, string bpmnXml, CancellationToken cancellationToken = default);

    // ---- Mutaciones del grafo (solo borradores; regeneran el BpmnXml) ----

    /// <summary>Agrega un nodo en (x, y). Genera BpmnElementId unico. Un solo startEvent por definicion.</summary>
    Task<WorkflowResult<FlowCanvasNodeDto>> AddNodeAsync(long definitionId, WorkflowNodeType nodeType, int x, int y, CancellationToken cancellationToken = default);

    Task<WorkflowResult<bool>> MoveNodeAsync(long nodeId, int x, int y, CancellationToken cancellationToken = default);

    Task<WorkflowResult<bool>> RenameNodeAsync(long nodeId, string? name, CancellationToken cancellationToken = default);

    /// <summary>Conecta dos nodos de la misma definicion (rechaza duplicados y self-loops).</summary>
    Task<WorkflowResult<FlowCanvasEdgeDto>> ConnectAsync(long sourceNodeId, long targetNodeId, CancellationToken cancellationToken = default);

    /// <summary>Borra el nodo con sus aristas. NUNCA el startEvent (es unico).</summary>
    Task<WorkflowResult<bool>> DeleteNodeAsync(long nodeId, CancellationToken cancellationToken = default);

    Task<WorkflowResult<bool>> DeleteEdgeAsync(long edgeId, CancellationToken cancellationToken = default);

    /// <summary>Condicion de la arista (formato del motor: "approval == 'Approved'"; vacio = rama default).</summary>
    Task<WorkflowResult<bool>> SetEdgeConditionAsync(long edgeId, string? conditionExpression, CancellationToken cancellationToken = default);

    /// <summary>Configuracion basica del nodo: AllowsAssignment + reinicio (reusa SetRestartTargetAsync del motor).</summary>
    Task<WorkflowResult<bool>> SetNodeConfigAsync(long nodeId, bool allowsAssignment, long? restartNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apariencia del nodo en el graficador: color de paleta (violet/blue/green/amber/rose/slate o null)
    /// y nota post-it. Son metadatos del nodo (no viajan en el XML BPMN); no regenera el XML.
    /// </summary>
    Task<WorkflowResult<bool>> SetNodeAppearanceAsync(long nodeId, string? color, string? note, CancellationToken cancellationToken = default);

    // ---- Propiedades y ciclo de vida de la definicion ----

    Task<WorkflowResult<bool>> UpdateDefinitionPropsAsync(long definitionId, string name, string? category, string? description, CancellationToken cancellationToken = default);

    /// <summary>Pausa una publicada: StartInstanceAsync del motor rechaza instancias nuevas.</summary>
    Task<WorkflowResult<bool>> PauseAsync(long definitionId, CancellationToken cancellationToken = default);

    Task<WorkflowResult<bool>> ResumeAsync(long definitionId, CancellationToken cancellationToken = default);

    // ---- Importar BPMN (XML del modeler / archivo .bpmn) ----

    /// <summary>
    /// Importa un XML BPMN 2.0 (el que produce bpmn-js o un archivo .bpmn) y crea una definicion
    /// nueva en Borrador (version max+1 si el ProcessCode ya existe, via motor). Deriva
    /// ProcessCode y nombre del XML si no vienen.
    /// </summary>
    Task<WorkflowResult<FlowCanvasDto>> ImportBpmnAsync(string bpmnXml, CancellationToken cancellationToken = default);
}
