// Contratos del gateway de IA multi-proveedor heredado del backbone (base de RQ16).
// Se conservan los tipos de transporte y el contrato del cliente HTTP multi-proveedor; el
// motor conversacional de ECOREX (agentes, tool-cache, sesiones) NO se hereda: RQ16 define
// asistencia inline y chat documental, con otro diseno.

using Ecorex.Domain.Enums;

namespace Ecorex.Application.Tenancy;

/// <summary>
/// Un turno de la conversacion. Role: "user" (cliente) o "model" (asistente).
/// Attachments es opcional: lleva los recursos adjuntos en ese turno.
/// El proveedor de IA ignora este campo.
/// </summary>
public sealed record AiChatTurn(string Role, string Text, IReadOnlyList<AiChatAttachment>? Attachments = null);

/// <summary>Recurso entregado en el chat (imagen, video, pdf, ubicacion o texto).</summary>
public sealed record AiChatAttachment(string Name, AgentResourceType ResourceType, string? FileUrl, string? FileName, string? Detail);

/// <summary>
/// Entrada del log de depuracion de prompts: una por cada llamada al proveedor de IA, con su
/// fecha/hora, el contenido enviado y, opcionalmente, la respuesta del LLM.
/// </summary>
public sealed record AiDebugPrompt(string Title, DateTimeOffset SentAt, string Content, string? Response = null);

/// <summary>Resultado de una llamada de inferencia, con el consumo de tokens y los recursos a adjuntar.</summary>
/// <param name="DebugPrompts">
/// Log de los prompts enviados a la IA en esta vuelta (uno o mas, en orden cronologico). Util
/// para depurar; en uso real conviene no enviarlo al cliente final.
/// </param>
public sealed record AiChatResult(bool Ok, string? Text, string? Error, int InputTokens = 0, int OutputTokens = 0,
    IReadOnlyList<AiChatAttachment>? Attachments = null, IReadOnlyList<AiDebugPrompt>? DebugPrompts = null);

// ===== Function calling (herramientas in-process) =====

/// <summary>Definicion de una herramienta invocable: nombre, descripcion y JSON Schema de parametros.</summary>
public sealed record AiToolSpec(string Name, string? Description, string ParametersJsonSchema);

/// <summary>Una llamada a herramienta solicitada por el modelo: id de correlacion, nombre y argumentos en JSON.</summary>
public sealed record AiToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// Mensaje de una conversacion con herramientas. Role: "user", "assistant"/"model" o "tool".
/// - assistant con ToolCalls: el modelo pidio ejecutar una o mas herramientas.
/// - tool: resultado de una herramienta (ToolCallId + ToolName + Text con el JSON de salida).
/// </summary>
public sealed record AiToolMessage(string Role, string? Text, IReadOnlyList<AiToolCall>? ToolCalls = null,
    string? ToolCallId = null, string? ToolName = null);

/// <summary>Respuesta del proveedor en modo herramientas: texto final (si lo hay) y/o herramientas a ejecutar.</summary>
public sealed record AiCompletion(bool Ok, string? Text, string? Error, int InputTokens, int OutputTokens,
    IReadOnlyList<AiToolCall> ToolCalls);

/// <summary>Parte de un prompt MULTIMODAL: texto O imagen (base64 + mime). Para clasificacion por vision.</summary>
public sealed record AiVisionPart(string? Text = null, string? ImageBase64 = null, string? ImageMime = null);

/// <summary>
/// Cliente HTTP que habla con cada proveedor de IA (Gemini, OpenAI/ChatGPT, DeepSeek, Claude).
/// Recibe la API key ya descifrada; no persiste ni loggea secretos.
/// </summary>
public interface IAiProviderClient
{
    Task<AiChatResult> CompleteAsync(
        AiProvider provider,
        string apiKey,
        string? baseUrl,
        string model,
        string systemPrompt,
        IReadOnlyList<AiChatTurn> turns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que CompleteAsync pero habilitando function calling: el modelo puede responder con texto
    /// final o con una o mas llamadas a herramienta (AiToolCall). El motor ejecuta las herramientas
    /// in-process y vuelve a llamar con los resultados hasta que el modelo entrega su respuesta final.
    /// </summary>
    Task<AiCompletion> CompleteWithToolsAsync(
        AiProvider provider,
        string apiKey,
        string? baseUrl,
        string model,
        string systemPrompt,
        IReadOnlyList<AiToolMessage> messages,
        IReadOnlyList<AiToolSpec> tools,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completa un prompt MULTIMODAL (texto + imagenes) en un unico turno de usuario y devuelve el texto.
    /// Pensado para clasificacion por vision. Solo Gemini y Claude; otros proveedores devuelven error.
    /// </summary>
    Task<AiChatResult> CompleteVisionAsync(
        AiProvider provider,
        string apiKey,
        string? baseUrl,
        string model,
        string systemPrompt,
        IReadOnlyList<AiVisionPart> content,
        CancellationToken cancellationToken = default);
}
