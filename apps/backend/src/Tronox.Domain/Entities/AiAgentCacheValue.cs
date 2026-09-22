using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Valor capturado para un campo de cache durante una sesion (RQ16, datos cache). Entidad TENANT-SCOPED.
/// La sesion se identifica por SessionId:
/// - En el modulo /agentes (pruebas) se usa el AgentId como SessionId.
/// - En atencion real se usa el identificador de la conversacion (ej. el correo/hilo) como SessionId.
/// </summary>
public class AiAgentCacheValue : TenantEntity
{
    public long AgentId { get; set; }
    public AiAgent? Agent { get; set; }

    /// <summary>Identificador de la sesion (AgentId para pruebas, ConversationId en atencion real).</summary>
    public long SessionId { get; set; }

    /// <summary>Clave del dato, coincide con AiAgentCacheField.FieldKey.</summary>
    public string FieldKey { get; set; } = null!;

    /// <summary>Valor capturado por el motor de inferencia.</summary>
    public string? Value { get; set; }

    /// <summary>Fuente del dato (ej. "manual", "inference", "system"). Util para auditoria.</summary>
    public string? Source { get; set; }
}
