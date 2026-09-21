using Tronox.Domain.Enums;

namespace Tronox.Application.Validaciones;

/// <summary>Fila de la bandeja "Mis Tareas" (pendientes).</summary>
public sealed record TareaItemDto(
    long Id,
    long DocumentoId,
    string DocumentoNombre,
    string? ExpedienteCodigo,
    TipoValidacion Tipo,
    PrioridadTarea Prioridad,
    EstadoValidacion Estado,
    DateOnly? FechaLimite,
    int? DiasRestantes,
    string? SolicitanteNombre,
    DateTimeOffset FechaAsignacion,
    string? Instrucciones);

/// <summary>Fila del historial (tareas ya respondidas por el usuario).</summary>
public sealed record TareaHistorialDto(
    long Id,
    string DocumentoNombre,
    string? ExpedienteCodigo,
    TipoValidacion Tipo,
    string? SolicitanteNombre,
    EstadoValidacion Estado,
    string? Comentarios,
    DateTime? FechaRespuesta);

/// <summary>Conteos por tipo para los chips de la bandeja de pendientes.</summary>
public sealed record TareaContadoresDto(int Total, int Revision, int Aprobacion);

// ---- Solicitar (RF11) ----

public sealed record UsuarioAsignableDto(long Id, string Nombre);

public sealed record DocumentoSolicitarDto(long Id, string Nombre, EstadoDocumento Estado);

public sealed record SolicitarValidacionRequest(
    long DocumentoId,
    TipoValidacion Tipo,
    long UsuarioAsignadoId,
    PrioridadTarea Prioridad,
    DateOnly? FechaLimite,
    string? Instrucciones);
