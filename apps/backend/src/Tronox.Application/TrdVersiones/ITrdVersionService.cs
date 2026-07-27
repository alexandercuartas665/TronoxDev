namespace Tronox.Application.TrdVersiones;

/// <summary>
/// Versiones de la Tabla de Retencion Documental (RQ02 - RF01). Marco legal sobre el que RF04
/// construye la TRD. Solo una version Vigente por tenant a la vez.
/// </summary>
public interface ITrdVersionService
{
    Task<IReadOnlyList<TrdVersionDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<TrdVersionDto?> GetAsync(long versionId, CancellationToken cancellationToken = default);

    Task<TrdVersionKpisDto> GetKpisAsync(CancellationToken cancellationToken = default);

    Task<TrdVersionResult<TrdVersionDto>> CreateAsync(
        SaveTrdVersionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Edita los datos de una version. Solo permitido en estado EnConstruccion.</summary>
    Task<TrdVersionResult<TrdVersionDto>> UpdateAsync(
        long versionId, SaveTrdVersionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa una version (pasa a Vigente). La Vigente anterior del tenant, si existe, pasa
    /// AUTOMATICAMENTE a Historico (RF01 3.1.4-3). Solo procede desde EnConstruccion.
    /// </summary>
    Task<TrdVersionResult<TrdVersionDto>> ActivarAsync(
        long versionId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Descarta una version (pasa a Inactivo). Solo procede desde EnConstruccion.</summary>
    Task<TrdVersionResult<TrdVersionDto>> DescartarAsync(
        long versionId, long actorUserId, string? motivo = null, CancellationToken cancellationToken = default);
}
