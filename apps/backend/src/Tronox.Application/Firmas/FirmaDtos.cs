using Tronox.Domain.Enums;

namespace Tronox.Application.Firmas;

/// <summary>Solicitud de firma a un firmante (RQ05 - RF06). El firmante la cumple con el stepper OTP.</summary>
public sealed record SolicitarFirmaRequest(
    long DocId,
    long FirmanteUserId,
    TipoFirma TipoFirma,
    bool OtpRequerido,
    PrioridadTarea Prioridad = PrioridadTarea.Media,
    DateOnly? FechaLimite = null,
    string? Instrucciones = null,
    string? Tag = null);

/// <summary>Las 4 vistas de la bandeja "Mis Firmas" (RF10), calca los tabs del legacy.</summary>
public enum BandejaFirma
{
    /// <summary>Firmas que el usuario debe hacer (Pendiente, asignadas a el).</summary>
    Pendientes = 0,
    /// <summary>Solicitudes que el usuario hizo a otros.</summary>
    Enviadas = 1,
    /// <summary>Firmas que el usuario ya ejecuto (Firmado).</summary>
    Completadas = 2,
    /// <summary>Firmas rechazadas que lo involucran.</summary>
    Rechazadas = 3
}

/// <summary>Fila del grid de la bandeja "Mis Firmas" (RF10).</summary>
public sealed record FirmaBandejaItemDto(
    long FirmaId,
    long DocumentoId,
    string DocumentoNombre,
    string? ExpedienteCodigo,
    bool TieneBinario,
    TipoFirma TipoFirma,
    EstadoFirma Estado,
    bool OtpRequerido,
    string FirmanteNombre,
    string? SolicitanteNombre,
    PrioridadTarea Prioridad,
    DateOnly? FechaLimite,
    int? DiasRestantes,
    DateTimeOffset FechaSolicitud,
    DateTimeOffset? TimestampFirma,
    string? Instrucciones,
    string? ComentarioRechazo);

/// <summary>Conteos de las 4 tarjetas KPI de la bandeja (RF10). EnProgreso reservado a circuitos (diferido, 0).</summary>
public sealed record FirmaResumenDto(int Pendientes, int EnProgreso, int Firmados, int Rechazadas);

/// <summary>Una fila de firma de un documento (para consultar el estado).</summary>
public sealed record FirmaItemDto(
    long Id,
    long FirmanteUserId,
    string NombreFirmante,
    string? CargoFirmante,
    string? DependenciaFirmante,
    TipoFirma TipoFirma,
    EstadoFirma Estado,
    DateTimeOffset? TimestampFirma,
    bool OtpRequerido,
    string? HashDocumento);

/// <summary>Estado de firma de un documento: la dimension del documento + sus firmas registradas.</summary>
public sealed record FirmaEstadoDto(
    long DocId,
    EstadoFirmaDocumento DimensionFirma,
    IReadOnlyList<FirmaItemDto> Firmas);

/// <summary>Resultado de una firma directa ejecutada.</summary>
public sealed record FirmaEjecutadaDto(
    long FirmaId,
    string Hash,
    DateTimeOffset Timestamp);

/// <summary>Identidad del firmante para la pantalla de confirmacion (snapshot).</summary>
public sealed record FirmanteSnapshotDto(
    long UserId,
    string Nombre,
    string? Cargo,
    string? Dependencia);

/// <summary>Opcion del selector de firmante (RF06). Id = PlatformUserId (espacio que usa Firma).</summary>
public sealed record FirmanteOpcionDto(long PlatformUserId, string Nombre);

// ---- Circuitos multi-firmante (RF07) ----

/// <summary>Un firmante del circuito a crear (en orden para Secuencial).</summary>
public sealed record CircuitoFirmanteInput(long PlatformUserId, TipoFirma TipoFirma);

/// <summary>Solicitud de creacion de un circuito de firma (RF07).</summary>
public sealed record CrearCircuitoRequest(
    long DocId,
    ModoCircuito Modo,
    bool OtpRequerido,
    IReadOnlyList<CircuitoFirmanteInput> Firmantes,
    DateOnly? FechaLimite = null,
    string? Instrucciones = null);

/// <summary>Un firmante en la tarjeta de progreso del circuito (bandeja Enviadas).</summary>
public sealed record CircuitoFirmanteDto(
    int Orden,
    string Nombre,
    string? Cargo,
    TipoFirma TipoFirma,
    EstadoCircuitoFirmante Estado,
    DateTimeOffset? TimestampFirma);

/// <summary>Tarjeta de progreso de un circuito iniciado por el usuario (RF07, bandeja Enviadas).</summary>
public sealed record CircuitoEnviadoDto(
    long CircuitoId,
    long DocumentoId,
    string DocumentoNombre,
    string? ExpedienteCodigo,
    bool TieneBinario,
    ModoCircuito Modo,
    EstadoCircuito Estado,
    int Total,
    int Completados,
    DateTimeOffset FechaCreacion,
    string? MotivoCancelacion,
    IReadOnlyList<CircuitoFirmanteDto> Firmantes);

/// <summary>
/// Resultado de generar un OTP (RF08). El codigo viaja por correo; <see cref="CodigoDemo"/> solo se
/// rellena cuando el envio no fue posible (SMTP no configurado), para poder completar la firma en
/// entornos sin correo. En produccion con correo configurado llega null (no se revela el codigo).
/// </summary>
public sealed record OtpEnvioDto(string EmailEnmascarado, int MinutosVigencia, string? CodigoDemo);

// ---- Firma masiva por lote (RF09) ----

/// <summary>OTP de un lote de firma masiva (RF09): un solo codigo cubre todo el lote.</summary>
public sealed record OtpLoteEnvioDto(string LoteId, string EmailEnmascarado, int MinutosVigencia, string? CodigoDemo);

/// <summary>Resultado por documento de un lote de firma masiva.</summary>
public sealed record FirmaLoteItemDto(long FirmaId, string DocumentoNombre, bool Ok, string? Mensaje);

/// <summary>Resumen de un lote de firma masiva (N firmados / N con error).</summary>
public sealed record FirmaLoteResumenDto(int Total, int Exitosos, int Fallidos, IReadOnlyList<FirmaLoteItemDto> Items);

// ---- Pista de auditoria de firma (RF12) ----

/// <summary>Un evento de la pista de auditoria de firma (RF12), leido del ledger append-only.</summary>
public sealed record PistaAuditoriaDto(
    long Id,
    DateTimeOffset Fecha,
    string Actor,
    string Evento,
    string? Detalle,
    string? Ip);
