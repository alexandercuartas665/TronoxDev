namespace Tronox.Application.Radicacion.Correos;

/// <summary>
/// Clasificador de correos con IA (RQ16, port del CLASIFICADOR PQRS de VISAL): decide si un correo es una
/// PQRS-F y extrae los datos del peticionario. Usa el gateway de IA global (IAiProviderClient) con la key
/// descifrada, y registra el consumo de tokens. El system prompt PQRS es fijo del sistema.
/// </summary>
public interface ICorreoClasificadorIa
{
    Task<ClasificacionCorreoResult> ClasificarAsync(string? remitente, string? asunto, string cuerpo, CancellationToken ct = default);
}

/// <summary>Resultado de la clasificacion IA de un correo.</summary>
public sealed record ClasificacionCorreoResult(
    bool Ok,
    bool EsPqr,
    string? Tipo,           // Peticion | Queja | Reclamo | Sugerencia | Felicitacion
    string? Servicio,
    string? Descripcion,
    string? Nombres,
    string? Identificacion,
    string? Celular,
    string? Email,
    string? AtributoCalidad,
    string? Json,
    int InputTokens,
    int OutputTokens,
    string? Error)
{
    public int TokensTotal => InputTokens + OutputTokens;
    public static ClasificacionCorreoResult Fail(string error) =>
        new(false, false, null, null, null, null, null, null, null, null, null, 0, 0, error);
}
