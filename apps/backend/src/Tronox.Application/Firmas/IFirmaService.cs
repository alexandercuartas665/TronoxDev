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

    /// <summary>Usuarios del tenant asignables como firmantes (RF06), por PlatformUserId. Excluye al actor.</summary>
    Task<IReadOnlyList<FirmanteOpcionDto>> GetFirmantesAsignablesAsync(long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF07: crea un circuito multi-firmante (secuencial/paralelo) y activa a los firmantes que corresponda.</summary>
    Task<DocumentoResult<long>> CrearCircuitoAsync(CrearCircuitoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF07: circuitos iniciados por el usuario, con su progreso (bandeja Enviadas).</summary>
    Task<IReadOnlyList<CircuitoEnviadoDto>> ListarCircuitosEnviadosAsync(long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    /// <summary>consultarEstadoFirma: dimension de firma del documento + sus firmas registradas.</summary>
    Task<DocumentoResult<FirmaEstadoDto>> ConsultarEstadoFirmaAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>cancelarFirma: cancela una solicitud pendiente (solicitante o firmante). No borra (invariante 8).</summary>
    Task<DocumentoResult<bool>> CancelarFirmaAsync(long firmaId, long actorUserId, CancellationToken cancellationToken = default);

    // ---- Bandeja "Mis Firmas" (RF10) ----

    /// <summary>Lista una vista de la bandeja del usuario (Pendientes/Enviadas/Completadas/Rechazadas). Fail-closed.</summary>
    Task<IReadOnlyList<FirmaBandejaItemDto>> ListarBandejaAsync(BandejaFirma tab, long actorUserId, string? texto = null, CancellationToken cancellationToken = default);

    /// <summary>Conteos de las 4 tarjetas KPI de la bandeja.</summary>
    Task<FirmaResumenDto> ContarResumenAsync(long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF10: el firmante rechaza una solicitud pendiente (comentario obligatorio) -> Rechazado.</summary>
    Task<DocumentoResult<bool>> RechazarSolicitudAsync(long firmaId, string comentario, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF08 (stepper): cumple una solicitud pendiente propia SIN OTP. Sella el PDF y marca Firmado.</summary>
    Task<DocumentoResult<FirmaEjecutadaDto>> FirmarSolicitadaAsync(long firmaId, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default);

    /// <summary>RF08 (stepper, paso OTP): genera y envia un codigo de 6 digitos al firmante. Devuelve el correo enmascarado y la vigencia.</summary>
    Task<DocumentoResult<OtpEnvioDto>> GenerarOtpAsync(long firmaId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF08 (stepper, firmar con OTP): valida el codigo y, si es correcto, sella el PDF y marca Firmado.</summary>
    Task<DocumentoResult<FirmaEjecutadaDto>> FirmarConOtpAsync(long firmaId, string codigo, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default);

    // ---- Firma masiva por lote (RF09) ----

    /// <summary>RF09: indica si alguna de las firmas seleccionadas exige OTP (para decidir el paso OTP del lote).</summary>
    Task<bool> LoteRequiereOtpAsync(IReadOnlyList<long> firmaIds, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF09: genera y envia UN codigo OTP para todo el lote. Devuelve el LoteId + correo enmascarado.</summary>
    Task<DocumentoResult<OtpLoteEnvioDto>> GenerarOtpLoteAsync(IReadOnlyList<long> firmaIds, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF09: firma el lote secuencialmente (valida el OTP del lote si aplica). Los que fallan quedan Pendientes.</summary>
    Task<DocumentoResult<FirmaLoteResumenDto>> FirmarLoteAsync(IReadOnlyList<long> firmaIds, string? loteId, string? codigo, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default);

    // ---- Pista de auditoria de firma (RF12) ----

    /// <summary>RF12: eventos recientes de firma del tenant, del ledger append-only (mas recientes primero).</summary>
    Task<IReadOnlyList<PistaAuditoriaDto>> ListarPistaAuditoriaAsync(long actorUserId, int tope = 100, CancellationToken cancellationToken = default);

    // ---- Firma directa (slice 1, calca FirmaDirectaHelper: sin stepper, sin OTP) ----

    /// <summary>Identidad del firmante (snapshot) para la pantalla de confirmacion de la firma directa.</summary>
    Task<DocumentoResult<FirmanteSnapshotDto>> GetFirmanteSnapshotAsync(long docId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Firma directa: el usuario logueado firma su propio documento Terminado. Sella el PDF (cajita visual
    /// + hash), lo deja como version oficial en sitio (sin versionar), marca el documento Firmado y registra
    /// la firma. Requiere consentimiento explicito del firmante (RF08 - consentimiento).
    /// </summary>
    Task<DocumentoResult<FirmaEjecutadaDto>> FirmarDirectoAsync(long docId, long actorUserId, string? ip, string? sesionId, CancellationToken cancellationToken = default);

    // ---- Mi Firma: grafo / firma manuscrita (RF03 3.3.3) ----

    /// <summary>Grafo (firma manuscrita) vigente del usuario como data URI para mostrar, o null si no tiene.</summary>
    Task<string?> GetMiGrafoAsync(long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Guarda (upsert) el grafo del usuario a partir de un data URI o base64 PNG. Se pinta en la cajita al sellar.</summary>
    Task<DocumentoResult<bool>> GuardarMiGrafoAsync(string imagenDataUri, long actorUserId, CancellationToken cancellationToken = default);
}
