namespace Tronox.Application.SeriesDocumentales;

/// <summary>
/// Catalogo de Series y Subseries (RQ02 - RF02): listado maestro intelectual de la entidad,
/// independiente de las dependencias. Es el insumo de RF04 (cruce Dependencia + Serie).
/// </summary>
public interface ISerieDocumentalService
{
    /// <summary>Arbol del catalogo (jerarquia ilimitada). Por defecto solo series Activas.</summary>
    Task<IReadOnlyList<SerieNodeDto>> GetTreeAsync(
        bool includeInactivas = false, CancellationToken cancellationToken = default);

    Task<SerieKpisDto> GetKpisAsync(CancellationToken cancellationToken = default);

    Task<SerieResult<SerieDto>> CreateAsync(
        SaveSerieRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<SerieResult<SerieDto>> UpdateAsync(
        long serieId, SaveSerieRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inactiva o reactiva una serie (invariante 8: nunca borrado fisico). Inactivar exige que no
    /// tenga subseries ACTIVAS (se inactivan primero). Una serie Inactiva no se ofrece en RF04.
    /// </summary>
    Task<SerieResult<bool>> SetEstadoAsync(
        long serieId, bool activar, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default);
}
