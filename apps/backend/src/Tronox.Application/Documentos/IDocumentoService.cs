namespace Tronox.Application.Documentos;

/// <summary>
/// Casos de uso de "Mis Documentos" (RQ04 - RF15/RF16). Primer slice: Mis Borradores (crear con
/// binario en object storage o marcar Fisico, listar, editar, descargar, eliminar) + Archivar en un
/// expediente existente + Archivados por mi. Diferido: compartir (RF07), versionado (RF03), busqueda
/// avanzada (RF14), OCR, editor/plantillas (RF08/RF10), restricciones (RF13), referencias (RF17).
/// </summary>
public interface IDocumentoService
{
    Task<IReadOnlyList<BorradorItemDto>> ListarBorradoresAsync(long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArchivadoItemDto>> ListarArchivadosPorMiAsync(long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    Task<DocumentoResult<DocumentoDetalleDto>> GetDetalleAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Crea un borrador con binario: sube al object storage y calcula el hash.</summary>
    Task<DocumentoResult<DocumentoDetalleDto>> CrearBorradorBinarioAsync(
        string nombre, DateOnly? fechaDocumento, byte[] contenido, string nombreArchivo,
        long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Crea un borrador Fisico (papel, sin binario todavia).</summary>
    Task<DocumentoResult<DocumentoDetalleDto>> CrearBorradorFisicoAsync(
        CrearBorradorFisicoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<DocumentoResult<DocumentoDetalleDto>> EditarBorradorAsync(
        long id, string nombre, DateOnly? fechaDocumento, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Descarga el binario del documento (respeta propiedad/clasificacion).</summary>
    Task<DocumentoResult<DocumentoDescargaDto>> DescargarAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Elimina un borrador (unico borrado FISICO del sistema): solo Borrador del creador.</summary>
    Task<DocumentoResult<bool>> EliminarBorradorAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Archivar (RF16) ----

    Task<IReadOnlyList<ExpedienteDestinoDto>> GetExpedientesDestinoAsync(long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipologiaOpcionDto>> GetTipologiasExpedienteAsync(long expedienteId, long actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocMetadatoDefDto>> GetMetadatosTipologiaAsync(long trdTipologiaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NivelDocOpcionDto>> GetNivelesAsync(CancellationToken cancellationToken = default);

    Task<DocumentoResult<DocumentoDetalleDto>> ArchivarAsync(
        ArchivarRequest request, long actorUserId, CancellationToken cancellationToken = default);
}
