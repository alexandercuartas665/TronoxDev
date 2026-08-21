using Tronox.Domain.Enums;

namespace Tronox.Application.Firmas;

/// <summary>Solicitud de firma a un firmante (RQ05 - RF06). El firmante la cumple con el stepper OTP.</summary>
public sealed record SolicitarFirmaRequest(
    long DocId,
    long FirmanteUserId,
    TipoFirma TipoFirma,
    bool OtpRequerido);

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
