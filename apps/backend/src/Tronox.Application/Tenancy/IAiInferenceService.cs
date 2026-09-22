namespace Tronox.Application.Tenancy;

/// <summary>
/// Inferencia de agentes del tenant (RQ16, port de ECOREX): arma el prompt con la config del agente
/// (prompt base + enrutador + recursos + estado de cache) y llama al proveedor con function calling,
/// ejecutando las herramientas in-process. La cuenta del proveedor (API key/modelo) la define el
/// Super Admin en Servidores de IA.
/// </summary>
public interface IAiInferenceService
{
    /// <summary>
    /// Conversacion de prueba contra el agente (modulo /agentes). La sesion de cache es el AgentId.
    /// systemPromptOverride permite probar un prompt aun sin guardar.
    /// </summary>
    Task<AiChatResult> TestChatAsync(long agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null,
        long actorUserId = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atencion real. La sesion de cache es la conversacion (ej. el correo). autonomous decide si el agente
    /// ejecuta acciones de verdad (true) o solo registra solicitudes para que un humano las confirme (false).
    /// El resultado incluye DebugPrompts (prompts + herramientas) para persistir la bitacora.
    /// </summary>
    Task<AiChatResult> RespondAsync(long agentId, long sessionId, IReadOnlyList<AiChatTurn> turns, bool autonomous,
        long actorUserId, CancellationToken cancellationToken = default);
}
