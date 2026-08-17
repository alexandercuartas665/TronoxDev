using Tronox.Domain.Enums;

namespace Tronox.Application.Expedientes;

// ---- Ubicacion fisica (RF12) ----

public sealed record TopografiaOpcionDto(long Id, string Codigo, string Nombre);

public sealed record UbicacionActualDto(
    long TopografiaElementoId, string Ubicacion, FaseArchivo Fase, string? AsignadoPor, DateTimeOffset Fecha);

public sealed record UbicacionHistorialItemDto(
    long Id, string Ubicacion, FaseArchivo Fase, string? AsignadoPor, DateTimeOffset Fecha, string? Observacion);

public sealed record ExpedienteUbicacionDto(
    UbicacionActualDto? Actual, IReadOnlyList<UbicacionHistorialItemDto> Historial);

// ---- Vinculos (RF14) ----

public sealed record VinculoDto(
    long VinculoId, long OtroExpedienteId, string OtroCodigo, string OtroNombre, EstadoExpediente OtroEstado,
    string? VinculadoPor, DateTimeOffset Fecha, string? Observacion);

public sealed record VinculoBusquedaDto(long Id, string Codigo, string Nombre, EstadoExpediente Estado);

// ---- Trazabilidad (RF09) ----

public sealed record TrazaItemDto(
    string Accion, string? Usuario, DateTimeOffset Fecha, string? Detalle);

// ---- Cierre / reapertura (RF08) ----

public sealed record CierreItemDto(
    int NumeroCierre, string HashSha256, string? Usuario, DateTimeOffset Fecha, bool EsReapertura, string? Justificacion);
