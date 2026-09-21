namespace Tronox.Application.Trd;

/// <summary>
/// Construccion de la TRD (RQ02 - RF04): cruce Dependencia x Serie dentro de una version, con sus
/// tiempos de retencion, disposicion final, clasificacion y metadatos de expediente. Produce el CCD
/// y la TRD. Consume RF01 (version), RF02 (catalogo de series), RQ01 (dependencias) y RF03 (listas).
/// </summary>
public interface ITrdConstruccionService
{
    /// <summary>Cabecera de la version (identidad + si es editable). Null si no existe.</summary>
    Task<TrdVersionCabeceraDto?> GetVersionAsync(long versionId, CancellationToken cancellationToken = default);

    /// <summary>Una fila por dependencia con su conteo de series asignadas en esta version.</summary>
    Task<IReadOnlyList<DependenciaTrdResumenDto>> GetDependenciasResumenAsync(
        long versionId, CancellationToken cancellationToken = default);

    /// <summary>Asignaciones (series) de una dependencia en una version, con sus metadatos.</summary>
    Task<IReadOnlyList<TrdAsignacionDto>> GetAsignacionesAsync(
        long versionId, long dependenciaId, bool includeArchived = false,
        CancellationToken cancellationToken = default);

    // ---- Asignaciones (cruce) ----

    Task<TrdResult<TrdAsignacionDto>> AddAsignacionAsync(
        long versionId, AddAsignacionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Edita las reglas ESTRUCTURALES (tiempos, disposicion, clasificacion, flags,
    /// procedimiento). Solo En Construccion (RF01 3.1.3).</summary>
    Task<TrdResult<TrdAsignacionDto>> UpdateAsignacionAsync(
        long asignacionId, UpdateAsignacionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Edita SOLO el procedimiento. Permitido tambien sobre una version Vigente (RF01 3.1.3).</summary>
    Task<TrdResult<TrdAsignacionDto>> UpdateProcedimientoAsync(
        long asignacionId, string? procedimiento, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Inactiva o reactiva una asignacion. Eliminar/inactivar solo En Construccion (RF01 3.1.3).</summary>
    Task<TrdResult<bool>> SetAsignacionArchivedAsync(
        long asignacionId, bool archived, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default);

    // ---- Metadatos del expediente ----

    Task<TrdResult<TrdMetadatoDto>> AddMetadatoAsync(
        long asignacionId, SaveMetadatoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TrdResult<TrdMetadatoDto>> UpdateMetadatoAsync(
        long metadatoId, SaveMetadatoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TrdResult<bool>> SetMetadatoArchivedAsync(
        long metadatoId, bool archived, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Tipologias documentales (RF05) ----

    Task<TrdResult<TrdTipologiaDto>> AddTipologiaAsync(
        long asignacionId, SaveTipologiaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TrdResult<TrdTipologiaDto>> UpdateTipologiaAsync(
        long tipologiaId, SaveTipologiaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<TrdResult<bool>> SetTipologiaArchivedAsync(
        long tipologiaId, bool archived, long actorUserId, string? motivo = null, CancellationToken cancellationToken = default);

    // ---- Metadatos del documento (RF05 3.5.3): cuelgan de una tipologia ----

    Task<TrdResult<TrdMetadatoDto>> AddMetadatoDocumentoAsync(
        long tipologiaId, SaveMetadatoRequest request, long actorUserId, CancellationToken cancellationToken = default);
}
