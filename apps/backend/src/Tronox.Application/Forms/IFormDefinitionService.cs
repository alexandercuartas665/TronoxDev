namespace Tronox.Application.Forms;

/// <summary>
/// CRUD de definiciones de formulario dinamico con su arbol de contenedores y preguntas (RQ08, port
/// ECOREX / ADR-0015). Valida FieldCode unico por definicion, opciones obligatorias en
/// Select/MultiCheck/Radio/GridDetail y pattern compilable. Los cambios estructurales sobre una
/// definicion Active incrementan Revision (snapshot logico de version de negocio). Todo tenant-scoped
/// por el filtro global.
///
/// Port fiel de ECOREX con Guid->long. DIFERIDO en TRONOX: el vinculo a nodos de flujo BPMN
/// (AssignToWorkflowNode/GetWorkflowNodeForm) y las "Reglas" via motor externo (podado; ahora viven en
/// IFormConditionService).
/// </summary>
public interface IFormDefinitionService
{
    Task<IReadOnlyList<FormDefinitionListItemDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>Definicion completa con contenedores y preguntas ordenados. Null si no existe.</summary>
    Task<FormDefinitionDetailDto?> GetAsync(long definitionId, CancellationToken cancellationToken = default);

    Task<FormResult<FormDefinitionDetailDto>> CreateAsync(CreateFormDefinitionRequest request, CancellationToken cancellationToken = default);

    Task<FormResult<FormDefinitionDetailDto>> UpdateHeaderAsync(long definitionId, UpdateFormDefinitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Configura la transaccionalidad del formulario (ola F3): IsTransactional + modo de identidad + campo clave.</summary>
    Task<FormResult<FormDefinitionDetailDto>> SetTransactionalAsync(long definitionId, SetFormTransactionalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Promueve/retira el formulario como modulo (ola F4): marca IsModule y guarda icono/columnas/filtros de la bandeja.</summary>
    Task<FormResult<FormDefinitionDetailDto>> SetModuleAsync(long definitionId, SetFormModuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Draft/Inactive -> Active, validando la estructura (preguntas, opciones, patterns).</summary>
    Task<FormResult<FormDefinitionDetailDto>> ActivateAsync(long definitionId, CancellationToken cancellationToken = default);

    /// <summary>Active -> Inactive: deja de aceptar respuestas nuevas.</summary>
    Task<FormResult<FormDefinitionDetailDto>> DeactivateAsync(long definitionId, CancellationToken cancellationToken = default);

    Task<FormResult<bool>> SetArchivedAsync(long definitionId, bool archived, CancellationToken cancellationToken = default);

    // ---- Contenedores ----

    Task<FormResult<FormContainerDto>> AddContainerAsync(long definitionId, SaveFormContainerRequest request, CancellationToken cancellationToken = default);

    Task<FormResult<FormContainerDto>> UpdateContainerAsync(long containerId, SaveFormContainerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Borra el contenedor; sus preguntas y sub-contenedores pasan al padre (o a la raiz).</summary>
    Task<FormResult<bool>> DeleteContainerAsync(long containerId, CancellationToken cancellationToken = default);

    /// <summary>Reordena el contenedor entre sus hermanos (paso a paso).</summary>
    Task<FormResult<bool>> MoveContainerAsync(long containerId, bool moveUp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mueve el contenedor a otro padre (o a la raiz con null) en la posicion index, renumerando ambos
    /// grupos de hermanos (drag and drop del constructor, ADR-0021).
    /// </summary>
    Task<FormResult<bool>> MoveContainerToAsync(long containerId, long? parentId, int index, CancellationToken cancellationToken = default);

    // ---- Preguntas ----

    Task<FormResult<FormQuestionDto>> AddQuestionAsync(long definitionId, SaveFormQuestionRequest request, CancellationToken cancellationToken = default);

    Task<FormResult<FormQuestionDto>> UpdateQuestionAsync(long questionId, SaveFormQuestionRequest request, CancellationToken cancellationToken = default);

    Task<FormResult<bool>> DeleteQuestionAsync(long questionId, CancellationToken cancellationToken = default);

    /// <summary>Reordena la pregunta dentro de su contenedor (paso a paso).</summary>
    Task<FormResult<bool>> MoveQuestionAsync(long questionId, bool moveUp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mueve la pregunta a otro contenedor (o a la raiz con null) en la posicion index, renumerando
    /// ambos grupos de hermanos (drag and drop del constructor, ADR-0021).
    /// </summary>
    Task<FormResult<bool>> MoveQuestionToAsync(long questionId, long? containerId, int index, CancellationToken cancellationToken = default);
}
