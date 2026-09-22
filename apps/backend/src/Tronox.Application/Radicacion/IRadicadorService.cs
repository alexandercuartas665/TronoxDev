using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Orquestador de creacion de radicados (RQ09). Centraliza el consecutivo (ISequenceService, SELECT FOR
/// UPDATE, scope tenant/tipo/anio), el calculo de vencimiento SLA (calendario habil) y el armado del
/// numero de radicado. Lo reutilizan "radicar desde correo" (RF04) y el asistente rad_radicar (futuro).
/// </summary>
public interface IRadicadorService
{
    Task<RadicarResult> RadicarAsync(RadicarNuevoRequest request, CancellationToken ct = default);

    /// <summary>Radica subiendo primero los documentos electronicos a object storage (RF02, paso 3.5 del
    /// asistente): calcula contentType/SHA-256/folios, sube cada archivo y los cuelga del radicado. Los
    /// folios totales se derivan de los archivos si el request no trae folios.</summary>
    Task<RadicarResult> RadicarConArchivosAsync(RadicarNuevoRequest request,
        IReadOnlyList<AdjuntoBytes> archivos, bool estampar = false, double estampaX = 62, double estampaY = 6,
        CancellationToken ct = default);

    /// <summary>Guarda el asistente en curso como BORRADOR (calca "Guardar borrador" del legacy): persiste lo
    /// capturado sin consumir consecutivo (numero temporal) ni calcular SLA, con estado Borrador para
    /// retomarlo despues. Validacion minima: no exige asunto/remitente completos.</summary>
    Task<RadicarResult> GuardarBorradorAsync(RadicarNuevoRequest request, CancellationToken ct = default);

    /// <summary>Adjunta documentos escaneados/electronicos a un radicado YA existente (paso Digitalizacion del
    /// asistente, RF06-3 / rad_op.ashx?action=adjuntar): sube cada archivo a object storage (invariante 9) y lo
    /// cuelga del radicado con traza. Se usa despues de radicar para digitalizar el soporte fisico.</summary>
    Task<RadicarResult> AdjuntarAsync(long radicadoId, IReadOnlyList<AdjuntoBytes> archivos, CancellationToken ct = default);
}

/// <summary>Archivo crudo (bytes en memoria) que el asistente sube al radicar. La subida a object storage
/// (invariante 9) y el calculo de key/SHA/folios los hace el servicio, no la UI.</summary>
public sealed record AdjuntoBytes(string Nombre, byte[] Contenido, string? MimeType = null);

/// <summary>Datos para crear un radicado nuevo. Los adjuntos ya deben estar en object storage (StorageKey).</summary>
public sealed record RadicarNuevoRequest(
    RadicadoTipo Tipo,
    long TipoComunicacionId,
    string? Asunto,
    string? Descripcion,
    RadicadoCanal Canal,
    bool Anonimo,
    string? RemitenteNombre,
    string? RemitenteEmail,
    string? RemitenteTipoDoc = null,
    string? RemitenteDocumento = null,
    string? RemitenteTelefono = null,
    long? NivelReservaId = null,
    long? RadicadoRelacionadoId = null,
    string Soporte = "Electronico",
    RadicadoPrioridad Prioridad = RadicadoPrioridad.Normal,
    IReadOnlyList<RadicarAdjunto>? Adjuntos = null,
    int? Folios = null,
    int? NumAnexos = null,
    long? DependenciaOrigenId = null,
    long? FuncionarioOrigenId = null,
    DateOnly? FechaDocumento = null,
    string? Observaciones = null,
    string? RemitenteMunicipio = null,
    // ---- Solo salidas (rad_salida_wizard): canal de envio y marca de respuesta definitiva. ----
    string? CanalEnvio = null,
    bool EsRespuestaDefinitiva = false);

/// <summary>Referencia a un adjunto ya subido a object storage, para colgarlo del radicado.</summary>
public sealed record RadicarAdjunto(string Nombre, string? Extension, string? MimeType, long TamanoBytes,
    string? StorageBucket, string? StorageKey, string? Sha256);

public sealed record RadicarResult(bool Ok, string? Error = null, long? RadicadoId = null, string? Numero = null)
{
    public static RadicarResult Fail(string error) => new(false, error);
    public static RadicarResult Success(long id, string numero) => new(true, null, id, numero);
}
