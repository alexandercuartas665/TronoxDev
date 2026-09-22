using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Prompt enrutado de un agente de IA (RQ16). Entidad TENANT-SCOPED. Un agente tiene un prompt base
/// (AiAgent.SystemPrompt) y opcionalmente varios prompts con nombre + regla de uso; el enrutador aplica
/// el prompt cuyo criterio coincide con el mensaje entrante (ej. "cuando sea una queja de facturacion").
/// </summary>
public class AiAgentPrompt : TenantEntity
{
    public long AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Regla/criterio en lenguaje natural que indica cuando usar este prompt.</summary>
    public string? Rule { get; set; }

    public string Body { get; set; } = "";

    public int SortOrder { get; set; }
}
