using Tronox.Application.Documentos;

namespace Tronox.Application.Firmas;

/// <summary>
/// Servicio de firma electronica (RQ05). Expone el CONTRATO ESTABLE de firma (invariante 5): los tres
/// metodos de solicitud/consulta/cancelacion NO cambian de firma; si falta algo se agrega. En el primer
/// slice se implementa ademas la firma directa (auto-firma); la solicitud queda registrada pero el
/// stepper de 5 pasos (Lectura/Identidad/Consentimiento/OTP/Resultado) que la CUMPLE es del modulo RQ05
/// completo (ver ADR-017).
/// </summary>
public interface IFirmaService
{
    // ---- Contrato estable RQ05 (invariante 5) ----

    /// <summary>solicitarFirma: registra una solicitud de firma a un firmante (Pendiente). Devuelve el id.</summary>
    Task<DocumentoResult<long>> SolicitarFirmaAsync(SolicitarFirmaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>consultarEstadoFirma: dimension de firma del documento + sus firmas registradas.</summary>
    Task<DocumentoResult<FirmaEstadoDto>> ConsultarEstadoFirmaAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>cancelarFirma: cancela una solicitud pendiente (solicitante o firmante). No borra (invariante 8).</summary>
    Task<DocumentoResult<bool>> CancelarFirmaAsync(long firmaId, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Firma directa (slice 1, calca FirmaDirectaHelper: sin stepper, sin OTP) ----

    /// <summary>Identidad del firmante (snapshot) para la pantalla de confirmacion de la firma directa.</summary>
    Task<DocumentoResult<FirmanteSnapshotDto>> GetFirmanteSnapshotAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Firma directa: el usuario logueado firma su propio documento Terminado. Sella el PDF (cajita visual
    /// + hash), lo deja como version oficial en sitio (sin versionar), marca el documento Firmado y registra
    /// la firma. Requiere consentimiento explicito del firmante (RF08 - consentimiento).
    /// </summary>
    Task<DocumentoResult<FirmaEjecutadaDto>> FirmarDirectoAsync(long docId, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default);
}
