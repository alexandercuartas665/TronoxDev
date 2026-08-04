using Tronox.Domain.Enums;

namespace Tronox.Application.Expedientes;

/// <summary>Las cinco vistas contextuales de la bandeja principal (RQ03 - RF01).</summary>
public enum BandejaVista
{
    /// <summary>Creados por el usuario. Vista por defecto.</summary>
    Mis = 0,
    /// <summary>Compartidos explicitamente con el usuario (RF11). Diferido: por ahora vacia.</summary>
    Compartidos = 1,
    /// <summary>Clasificacion Publico.</summary>
    Publicos = 2,
    /// <summary>Fase Central (requiere submodulo de transferencias + permiso).</summary>
    Central = 3,
    /// <summary>Fase Historico (requiere submodulo de transferencias + permiso).</summary>
    Historico = 4
}

/// <summary>Filtros de la bandeja (RF01). Todos opcionales; se combinan con AND.</summary>
public sealed record ExpedienteFiltro(
    string? Texto = null,
    EstadoExpediente? Estado = null,
    FaseArchivo? Fase = null,
    long? NivelClasificacionId = null,
    long? DependenciaId = null,
    long? SerieId = null,
    DateOnly? AperturaDesde = null,
    DateOnly? AperturaHasta = null);

/// <summary>Fila del grid de la bandeja (RF01). Columnas fijas + las mas usadas configurables.</summary>
public sealed record ExpedienteBandejaItemDto(
    long Id,
    string Codigo,
    string Nombre,
    string SerieCodigo,
    string SerieNombre,
    string DependenciaNombre,
    DateOnly FechaApertura,
    DateOnly? FechaCierre,
    EstadoExpediente Estado,
    FaseArchivo Fase,
    string NivelNombre,
    int NivelOrden,
    string? CreadoPorNombre,
    string? FondoNombre,
    DateTimeOffset FechaCreacion,
    bool PuedeEditar);

/// <summary>Tarjetas de estadisticas de la franja superior de la bandeja.</summary>
public sealed record ExpedienteStatsDto(int Total, int Abiertos, int Cerrados);

/// <summary>Pagina de bandeja: filas visibles (ya filtradas fail-closed) + estadisticas.</summary>
public sealed record ExpedienteBandejaDto(
    IReadOnlyList<ExpedienteBandejaItemDto> Items,
    ExpedienteStatsDto Stats);

// ---- Opciones para el formulario de creacion (cascada Fondo -> Dependencia -> Serie) ----

public sealed record FondoOpcionDto(long Id, string Codigo, string Nombre);

public sealed record DependenciaOpcionDto(long Id, string Codigo, string Nombre, long? FondoId);

/// <summary>
/// Serie asignable en la creacion = una asignacion de TRD de la version Vigente para la dependencia
/// (RF03). Trae el nivel de clasificacion heredado (RF10) para precargar y validar el "solo elevar".
/// </summary>
public sealed record SerieOpcionDto(
    long TrdAsignacionId,
    string SerieCodigo,
    string SerieNombre,
    string CodigoCcd,
    long NivelHeredadoId,
    string NivelHeredadoNombre,
    int NivelHeredadoOrden);

public sealed record NivelClasificacionOpcionDto(long Id, string Codigo, string Nombre, int Orden);

/// <summary>Definicion de un metadato de expediente (contexto Expediente) segun la serie (DAT-04).</summary>
public sealed record MetadatoDefDto(
    long TrdMetadatoId,
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    long? ListaMaestraId,
    IReadOnlyList<MetadatoOpcionDto> OpcionesLista);

public sealed record MetadatoOpcionDto(string Clave, string Valor);

public sealed record MetadatoValorDto(long TrdMetadatoId, string Nombre, TipoDatoMetadato TipoDato, string? Valor);

// ---- Requests ----

public sealed record MetadatoInput(long TrdMetadatoId, string? Valor);

public sealed record CrearExpedienteRequest(
    long TrdAsignacionId,
    string Nombre,
    DateOnly FechaApertura,
    long NivelClasificacionId,
    IReadOnlyList<MetadatoInput> Metadatos);

public sealed record EditarExpedienteRequest(
    string Nombre,
    long NivelClasificacionId,
    IReadOnlyList<MetadatoInput> Metadatos);

// ---- Detalle (basico en este slice) ----

public sealed record ExpedienteDetalleDto(
    long Id,
    string Codigo,
    string Nombre,
    long TrdAsignacionId,
    string SerieCodigo,
    string SerieNombre,
    string DependenciaNombre,
    string? FondoNombre,
    string VersionTrd,
    EstadoExpediente Estado,
    FaseArchivo Fase,
    EstadoUbicacionExpediente EstadoUbicacion,
    long NivelClasificacionId,
    string NivelNombre,
    int NivelOrden,
    int NivelHeredadoOrden,
    DateOnly FechaApertura,
    DateOnly? FechaCierre,
    DateTimeOffset FechaCreacion,
    string? CreadoPorNombre,
    IReadOnlyList<MetadatoValorDto> Metadatos);
