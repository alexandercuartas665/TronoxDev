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
