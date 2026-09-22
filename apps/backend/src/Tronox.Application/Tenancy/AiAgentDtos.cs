using Tronox.Domain.Enums;

namespace Tronox.Application.Tenancy;

// DTOs de la capa de agentes de IA (RQ16, port de ECOREX). Guid->long por convencion; se omiten
// los campos atados a modulos podados (tableros, reacciones de WhatsApp).

public sealed record AiAgentDto(
    long Id,
    string Name,
    string? Role,
    AiProvider Provider,
    string? Model,
    string SystemPrompt,
    bool IsActive,
    int SortOrder,
    int ResourceCount,
    IReadOnlyList<string>? DisabledTools = null);

public sealed record AiAgentResourceDto(
    long Id,
    long AgentId,
    string Name,
    AgentResourceType ResourceType,
    string? Detail,
    string? FileUrl,
    string? FileName,
    int SortOrder);

public sealed record AiAgentPromptDto(long Id, long AgentId, string Name, string? Rule, string Body, int SortOrder);

public sealed record AiAgentDetailDto(AiAgentDto Agent, IReadOnlyList<AiAgentResourceDto> Resources, IReadOnlyList<AiAgentPromptDto> Prompts);

public sealed record CreateAiAgentRequest(string Name, string? Role, AiProvider Provider, string? Model, string SystemPrompt, IReadOnlyList<string>? DisabledTools = null);
public sealed record UpdateAiAgentRequest(string Name, string? Role, AiProvider Provider, string? Model, string SystemPrompt, IReadOnlyList<string>? DisabledTools = null);

// --- Historial de versiones de prompts (red de seguridad) ---
public sealed record AgentPromptSnapshotDto(string Name, string? Rule, string Body, int SortOrder);
public sealed record AiAgentPromptVersionDto(int Index, DateTimeOffset SavedAt, string BasePrompt, IReadOnlyList<AgentPromptSnapshotDto> Prompts);

public sealed record CreateAgentResourceRequest(long AgentId, string Name, AgentResourceType ResourceType, string? Detail, string? FileUrl, string? FileName);
public sealed record UpdateAgentResourceRequest(string Name, AgentResourceType ResourceType, string? Detail, string? FileUrl, string? FileName);

public sealed record CreateAgentPromptRequest(long AgentId, string Name, string? Rule, string Body);
public sealed record UpdateAgentPromptRequest(string Name, string? Rule, string Body);

// --- Datos Cache del agente ---
public sealed record AiAgentCacheFieldDto(long Id, long AgentId, string FieldKey, string Label, string? Description, int SortOrder, bool IsUpdatable);
public sealed record CreateAgentCacheFieldRequest(long AgentId, string Label, string? Description, bool IsUpdatable = true);
public sealed record UpdateAgentCacheFieldRequest(string Label, string? Description, bool IsUpdatable = true);

public sealed record AiAgentCacheValueDto(string FieldKey, string Label, string? Description, string? Value, string? Source, DateTimeOffset? UpdatedAt);
public sealed record SetAgentCacheValueRequest(long AgentId, long SessionId, string FieldKey, string? Value, string? Source);
