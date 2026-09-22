using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Agente de IA configurable del tenant (RQ16, capa de agentes; port de ECOREX.tareas). Entidad
/// TENANT-SCOPED. Define proveedor, modelo, prompt de sistema y si esta en produccion. Los recursos
/// (AiAgentResource) son los archivos/datos que el agente puede usar para responder. La invocacion en
/// TRONOX se cablea al canal de Correos/Radicacion (no a WhatsApp): un agente clasifica el correo y su
/// prompt deja de estar embebido en codigo.
/// </summary>
public class AiAgent : TenantEntity
{
    public string Name { get; set; } = null!;

    /// <summary>Rol/tipo descriptivo (clasificador, copiloto, seguimiento, etc.). Libre.</summary>
    public string? Role { get; set; }

    public AiProvider Provider { get; set; } = AiProvider.Claude;

    /// <summary>Modelo concreto del proveedor (opcional; si vacio se usa el por defecto del catalogo).</summary>
    public string? Model { get; set; }

    public string SystemPrompt { get; set; } = "";

    /// <summary>En produccion (encendido) o apagado. Interruptor por-agente.</summary>
    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Herramientas (function calling / "MCP") DESHABILITADAS para este agente (jsonb, lista de nombres).
    /// Null o vacio = todas las herramientas registradas estan habilitadas.
    /// </summary>
    public string? DisabledToolsJson { get; set; }

    /// <summary>
    /// Historial de versiones de los prompts (red de seguridad). Cada guardado guarda una instantanea
    /// {prompt base + prompts enrutados}, conservando las ultimas 5. Permite restaurar. Formato: arreglo
    /// JSON de { savedAt, basePrompt, prompts:[{ name, rule, body, sortOrder }] }.
    /// </summary>
    public string? PromptHistoryJson { get; set; }
}
