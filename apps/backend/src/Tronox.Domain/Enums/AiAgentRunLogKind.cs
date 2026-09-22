namespace Tronox.Domain.Enums;

/// <summary>Tipo de evento en la bitacora de atencion del agente de IA (RQ16, capa de agentes).</summary>
public enum AiAgentRunLogKind
{
    /// <summary>Mensaje/entrada recibida (correo entrante, turno de prueba).</summary>
    Inbound = 0,
    /// <summary>Prompt enviado al proveedor de IA (principal o extractor de cache).</summary>
    Prompt,
    /// <summary>El modelo solicito y se ejecuto una herramienta (con argumentos y resultado).</summary>
    Tool,
    /// <summary>Respuesta del agente entregada al canal.</summary>
    Reply,
    /// <summary>Nota informativa del proceso (cache vaciada, sesion cerrada, etc.).</summary>
    Info,
    /// <summary>Error durante la atencion.</summary>
    Error
}
