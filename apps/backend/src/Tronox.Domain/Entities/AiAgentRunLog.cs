using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Bitacora de atencion del agente de IA (RQ16). Entidad TENANT-SCOPED. Persiste, por conversacion, el
/// rastro del proceso: entradas recibidas, prompts enviados a la IA, herramientas ejecutadas (con
/// argumentos y resultado) y respuestas. Equivale al panel "PROMPTS enviados a la IA" del chat de
/// prueba, pero guardado para revisar la atencion real. ConversationId referencia el origen (ej. el
/// correo radicado); 0 para pruebas sueltas.
/// </summary>
public class AiAgentRunLog : TenantEntity
{
    public long ConversationId { get; set; }
    public long AgentId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
    public AiAgentRunLogKind Kind { get; set; }

    /// <summary>Titulo corto del evento (ej. "Herramienta: radicar").</summary>
    public string Title { get; set; } = null!;

    /// <summary>Contenido principal (prompt enviado, argumentos de la herramienta, texto recibido).</summary>
    public string? Content { get; set; }

    /// <summary>Respuesta asociada (texto del LLM, resultado JSON de la herramienta).</summary>
    public string? Response { get; set; }
}
