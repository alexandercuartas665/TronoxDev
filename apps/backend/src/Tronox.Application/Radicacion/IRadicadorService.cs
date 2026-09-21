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
}

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
    IReadOnlyList<RadicarAdjunto>? Adjuntos = null);

/// <summary>Referencia a un adjunto ya subido a object storage, para colgarlo del radicado.</summary>
public sealed record RadicarAdjunto(string Nombre, string? Extension, string? MimeType, long TamanoBytes,
    string? StorageBucket, string? StorageKey, string? Sha256);

public sealed record RadicarResult(bool Ok, string? Error = null, long? RadicadoId = null, string? Numero = null)
{
    public static RadicarResult Fail(string error) => new(false, error);
    public static RadicarResult Success(long id, string numero) => new(true, null, id, numero);
}
