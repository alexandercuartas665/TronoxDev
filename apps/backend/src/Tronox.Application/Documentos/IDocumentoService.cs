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

    /// <summary>Documentos archivados de un expediente (RQ03, pestana Documentos del detalle). Excluye anulados e historicos.</summary>
    Task<IReadOnlyList<ExpedienteDocumentoDto>> ListarPorExpedienteAsync(long expedienteId, long actorUserId, CancellationToken cancellationToken = default);

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

    // ---- Editar metadatos (RF04/RF05, calcado de exp_visor_data.ashx) ----

    /// <summary>Estado inicial del editor de metadatos: nombre/fecha/tipo actual + tipologias activas + campos con valores.</summary>
    Task<DocumentoResult<DocEditarMetadatosDto>> GetEditarMetadatosAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Metadatos de una tipologia con el valor actual del documento (al cambiar el tipo en el editor).</summary>
    Task<IReadOnlyList<DocMetadatoValorDefDto>> GetMetadatosTipologiaConValoresAsync(long docId, long trdTipologiaId, CancellationToken cancellationToken = default);

    /// <summary>Guarda nombre + fecha + tipo documental + valores de metadatos (op=guardar); audita el diff.</summary>
    Task<DocumentoResult<bool>> GuardarMetadatosAsync(GuardarMetadatosRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Descarga el binario del documento (respeta propiedad/clasificacion).</summary>
    Task<DocumentoResult<DocumentoDescargaDto>> DescargarAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Elimina un borrador (unico borrado FISICO del sistema): solo Borrador del creador.</summary>
    Task<DocumentoResult<bool>> EliminarBorradorAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Editor de texto interno (RF08) ----

    /// <summary>Reabre un borrador en el editor: devuelve nombre + cuerpo HTML. Solo Borrador del creador.</summary>
    Task<DocumentoResult<EditorContenidoDto>> AbrirEditorAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Guarda el cuerpo HTML del editor (crea el borrador si DocId es null/0). No genera PDF. Devuelve el DocId.</summary>
    Task<DocumentoResult<long>> GuardarContenidoAsync(GuardarContenidoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Genera el PDF del HTML (Chromium), lo sube al object storage y deja el borrador CON binario (sigue Borrador). Devuelve el DocId.</summary>
    Task<DocumentoResult<long>> GenerarPdfDesdeEditorAsync(GuardarContenidoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Compartir (RF07) ----

    /// <summary>Buscador de destinatarios del modal Compartir: usuarios (no compartidos aun) + roles.</summary>
    Task<IReadOnlyList<DestinoBusquedaDto>> BuscarDestinosCompartirAsync(long docId, string? criterio, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Otorga acceso a los destinatarios (expande roles a usuarios). Devuelve cuantos recibieron algo nuevo.</summary>
    Task<DocumentoResult<int>> CompartirAsync(CompartirRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Revoca (soft-delete) todo el acceso de un beneficiario sobre el documento.</summary>
    Task<DocumentoResult<bool>> RevocarComparticionAsync(long docId, long beneficiarioPlatformUserId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Accesos activos de un documento, para la lista del modal.</summary>
    Task<IReadOnlyList<CompartidoActivoDto>> ListarActivosComparticionAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Bandeja "Compartidos conmigo": documentos compartidos activos con el usuario actual.</summary>
    Task<IReadOnlyList<CompartidoConmigoDto>> ListarCompartidosConmigoAsync(long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    /// <summary>Contadores de las 3 bandejas (para los tabs): borradores, archivados por mi, compartidos conmigo.</summary>
    Task<(int Borradores, int Archivados, int Compartidos)> ContarBandejasAsync(long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Envia el documento como adjunto por correo (RF05 cob*): valida acceso+binario, descarga y adjunta.</summary>
    Task<DocumentoResult<bool>> EnviarPorCorreoAsync(long docId, string para, string asunto, string? mensaje, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Copia para imprimir (RF05): descarga el binario, estampa "COPIA NO CONTROLADA..." al pie (PDF) y audita "Impresion".</summary>
    Task<DocumentoResult<DocumentoDescargaDto>> GetCopiaImpresionAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Archivar (RF16) ----

    Task<IReadOnlyList<ExpedienteDestinoDto>> GetExpedientesDestinoAsync(long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipologiaOpcionDto>> GetTipologiasExpedienteAsync(long expedienteId, long actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocMetadatoDefDto>> GetMetadatosTipologiaAsync(long trdTipologiaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NivelDocOpcionDto>> GetNivelesAsync(CancellationToken cancellationToken = default);

    Task<DocumentoResult<DocumentoDetalleDto>> ArchivarAsync(
        ArchivarRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Incorpora UN documento directo en un expediente (Carga de Archivos): sube, folia y archiva.</summary>
    Task<DocumentoResult<bool>> IncorporarEnExpedienteAsync(
        IncorporarDocRequest request, long actorUserId, CancellationToken cancellationToken = default);
}
