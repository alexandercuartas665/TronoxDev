namespace Tronox.Application.Tenancy;

/// <summary>
/// CRUD de agentes de IA del tenant (RQ16). Gestiona el agente, sus recursos, prompts enrutados y el
/// historial de versiones de prompts. La inferencia vive en IAiInferenceService.
/// </summary>
public interface IAiAgentService
{
    Task<IReadOnlyList<AiAgentDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AiAgentDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<AiAgentDto?> CreateAsync(CreateAiAgentRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<AiAgentDto?> UpdateAsync(long id, UpdateAiAgentRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<AiAgentDto?> SetActiveAsync(long id, bool active, long actorUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default);
    Task<AiAgentDto?> DuplicateAsync(long sourceId, long actorUserId, CancellationToken cancellationToken = default);

    Task<AiAgentResourceDto?> AddResourceAsync(CreateAgentResourceRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<AiAgentResourceDto?> UpdateResourceAsync(long id, UpdateAgentResourceRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteResourceAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    Task<AiAgentPromptDto?> AddPromptAsync(CreateAgentPromptRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<AiAgentPromptDto?> UpdatePromptAsync(long id, UpdateAgentPromptRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<bool> DeletePromptAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAgentPromptVersionDto>> GetPromptHistoryAsync(long agentId, CancellationToken cancellationToken = default);
    Task<AiAgentDetailDto?> RestorePromptVersionAsync(long agentId, int versionIndex, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Bitacora de atencion: ultimos eventos (opcionalmente de un agente), mas recientes primero.</summary>
    Task<IReadOnlyList<AiAgentRunLogDto>> ListRunLogsAsync(long? agentId = null, int take = 200, CancellationToken cancellationToken = default);
}
