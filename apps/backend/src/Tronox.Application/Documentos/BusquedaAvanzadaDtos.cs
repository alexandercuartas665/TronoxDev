namespace Tronox.Application.Documentos;

/// <summary>
/// Filtros de la busqueda avanzada de documentos (RF14), calca la clase Filtros del legacy. Las listas
/// vacias/nulas no filtran. Dependencia y Serie/Subserie quedan diferidos (requieren resolver la cadena
/// de asignacion TRD del expediente); el resto de dimensiones estan cubiertas.
/// </summary>
public sealed record BuscarAvanzadoRequest(
    string? Texto,
    IReadOnlyList<long>? TipologiaIds,
    DateOnly? FechaDocDesde,
    DateOnly? FechaDocHasta,
    DateTimeOffset? FechaIncDesde,
    DateTimeOffset? FechaIncHasta,
    string? Usuario,
    string? Soporte,
    IReadOnlyList<string>? Clasificaciones,
    IReadOnlyList<string>? Estados);

/// <summary>Fila del grid de resultados de busqueda (RF14), columnas calcadas de doc_bandeja aspx:311-345.</summary>
public sealed record ResultadoBusquedaDto(
    long DocId,
    string Nombre,
    string? Formato,
    bool TieneBinario,
    string? TipologiaNombre,
    string? ExpedienteCodigo,
    string? ExpedienteNombre,
    DateOnly? FechaDocumento,
    DateTimeOffset? FechaIncorporacion,
    string Soporte,
    string Estado,
    string CreadoPor);

/// <summary>Opcion de tipologia para el filtro multi-seleccion del panel avanzado.</summary>
public sealed record TipologiaFiltroDto(long Id, string Nombre);
