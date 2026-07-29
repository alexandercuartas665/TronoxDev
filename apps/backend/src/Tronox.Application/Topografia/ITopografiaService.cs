namespace Tronox.Application.Topografia;

/// <summary>
/// Topografia fisica del archivo (RQ02 - RF06): jerarquia configurable de niveles + arbol de
/// elementos fisicos, con codigo topografico automatico. Base para ubicar expedientes (RQ03, Hito 2).
/// </summary>
public interface ITopografiaService
{
    // ---- Niveles (configuracion) ----

    Task<IReadOnlyList<TopografiaNivelDto>> ListNivelesAsync(CancellationToken cancellationToken = default);

    /// <summary>True si ya existen elementos: la config de niveles queda bloqueada (RF06 3.6.6-1).</summary>
    Task<bool> HayElementosAsync(CancellationToken cancellationToken = default);

    Task<TopografiaResult<TopografiaNivelDto>> CreateNivelAsync(
        SaveNivelRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TopografiaResult<TopografiaNivelDto>> UpdateNivelAsync(
        long nivelId, SaveNivelRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TopografiaResult<bool>> DeleteNivelAsync(
        long nivelId, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Elementos (arbol) ----

    Task<IReadOnlyList<TopografiaElementoNodeDto>> GetArbolAsync(
        bool includeInactivos = true, CancellationToken cancellationToken = default);

    Task<TopografiaKpisDto> GetKpisAsync(CancellationToken cancellationToken = default);

    Task<TopografiaResult<TopografiaElementoNodeDto>> CreateElementoAsync(
        SaveElementoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TopografiaResult<TopografiaElementoNodeDto>> UpdateElementoAsync(
        long elementoId, SaveElementoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Inactiva o reactiva un elemento (invariante 8: nunca borrado si tiene contenido).</summary>
    Task<TopografiaResult<bool>> SetEstadoAsync(
        long elementoId, bool activar, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default);
}
