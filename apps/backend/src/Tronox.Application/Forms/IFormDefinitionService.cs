namespace Tronox.Application.Forms;

/// <summary>
/// CRUD de definiciones de formulario dinamico con su arbol de contenedores y preguntas (RQ08, port
/// ECOREX / ADR-0015). Valida FieldCode unico por definicion, opciones obligatorias en
/// Select/MultiCheck/Radio y pattern compilable. Los cambios estructurales sobre una definicion Active
/// incrementan Revision. Tenant-scoped por el filtro global. Primer slice: sin transaccionalidad,
/// modulo, ni vinculo a nodos de workflow.
/// </summary>
public interface IFormDefinitionService
{
    Task<IReadOnlyList<FormDefinitionListItemDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>Definicion completa con contenedores y preguntas ordenados. Null si no existe.</summary>
    Task<FormDefinitionDetailDto?> GetAsync(long definitionId, CancellationToken cancellationToken = default);

    Task<FormResult<FormDefinitionDetailDto>> CreateAsync(CreateFormDefinitionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<FormResult<FormDefinitionDetailDto>> UpdateHeaderAsync(long definitionId, UpdateFormDefinitionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Draft/Inactive -> Active, validando la estructura (preguntas, opciones, patterns).</summary>
    Task<FormResult<FormDefinitionDetailDto>> ActivateAsync(long definitionId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Active -> Inactive: deja de aceptar respuestas nuevas.</summary>
    Task<FormResult<FormDefinitionDetailDto>> DeactivateAsync(long definitionId, long actorUserId, CancellationToken cancellationToken = default);

    Task<FormResult<bool>> SetArchivedAsync(long definitionId, bool archived, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Contenedores ----

    Task<FormResult<FormContainerDto>> AddContainerAsync(long definitionId, SaveFormContainerRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<FormResult<FormContainerDto>> UpdateContainerAsync(long containerId, SaveFormContainerRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<FormResult<bool>> DeleteContainerAsync(long containerId, long actorUserId, CancellationToken cancellationToken = default);
    Task<FormResult<bool>> MoveContainerAsync(long containerId, bool moveUp, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>Mueve el contenedor a otro padre (o a la raiz con null) en la posicion index (drag and drop).</summary>
    Task<FormResult<bool>> MoveContainerToAsync(long containerId, long? parentId, int index, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Preguntas ----

    Task<FormResult<FormQuestionDto>> AddQuestionAsync(long definitionId, SaveFormQuestionRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<FormResult<FormQuestionDto>> UpdateQuestionAsync(long questionId, SaveFormQuestionRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<FormResult<bool>> DeleteQuestionAsync(long questionId, long actorUserId, CancellationToken cancellationToken = default);
    Task<FormResult<bool>> MoveQuestionAsync(long questionId, bool moveUp, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>Mueve la pregunta a otro contenedor (o a la raiz con null) en la posicion index (drag and drop).</summary>
    Task<FormResult<bool>> MoveQuestionToAsync(long questionId, long? containerId, int index, long actorUserId, CancellationToken cancellationToken = default);
}
