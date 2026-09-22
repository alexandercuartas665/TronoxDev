namespace Tronox.Application.Radicacion.Correos;

/// <summary>
/// Clasificador de correos con IA (RQ16, port del CLASIFICADOR PQRS de VISAL): decide si un correo es una
/// PQRS-F y extrae los datos del peticionario. Si el buzon apunta a un AGENTE (agentId), usa su
/// proveedor/modelo/comportamiento editable; si no, cae al primer proveedor habilitado con el comportamiento
/// por defecto. El contrato de salida JSON siempre se anexa (fijo del sistema) para que la respuesta sea
/// parseable. Registra el consumo de tokens.
/// </summary>
public interface ICorreoClasificadorIa
{
    Task<ClasificacionCorreoResult> ClasificarAsync(string? remitente, string? asunto, string cuerpo, long? agentId = null, CancellationToken ct = default);
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
