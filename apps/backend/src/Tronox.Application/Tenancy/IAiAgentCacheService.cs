namespace Tronox.Application.Tenancy;

/// <summary>
/// Datos cache de un agente (RQ16): definicion de campos que el agente captura y sus valores por sesion.
/// Respeta IsUpdatable (los campos sticky no se sobrescriben una vez capturados).
/// </summary>
public interface IAiAgentCacheService
{
    Task<IReadOnlyList<AiAgentCacheFieldDto>> ListFieldsAsync(long agentId, CancellationToken cancellationToken = default);
    Task<AiAgentCacheFieldDto?> CreateFieldAsync(CreateAgentCacheFieldRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<AiAgentCacheFieldDto?> UpdateFieldAsync(long fieldId, UpdateAgentCacheFieldRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFieldAsync(long fieldId, long actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAgentCacheValueDto>> GetValuesAsync(long agentId, long sessionId, CancellationToken cancellationToken = default);
    Task<AiAgentCacheValueDto?> SetValueAsync(SetAgentCacheValueRequest request, CancellationToken cancellationToken = default);
    Task<int> ClearValuesAsync(long agentId, long sessionId, long actorUserId, CancellationToken cancellationToken = default);
}
