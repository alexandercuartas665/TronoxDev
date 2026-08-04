using Tronox.Domain.Enums;

namespace Tronox.Application.Plantillas;

/// <summary>Tarjeta del listado de plantillas (RF09).</summary>
public sealed record PlantillaItemDto(
    long Id,
    string Nombre,
    string? TipologiaNombre,
    PlantillaEstado Estado,
    int VariablesNum,
    int UsoContador,
    int TiposNum,
    DateTimeOffset FechaCreacion);

/// <summary>Detalle de una plantilla para edicion.</summary>
public sealed record PlantillaDetalleDto(
    long Id,
    string Nombre,
    string? Descripcion,
    string? ContenidoHtml,
    FormatoPapel FormatoPapel,
    OrientacionPapel Orientacion,
    MargenesPapel Margenes,
    string? Encabezado,
    string? PiePagina,
    PlantillaEstado Estado,
    int VariablesNum,
    int UsoContador,
    IReadOnlyList<long> TipologiaIds,
    IReadOnlyList<TipologiaOpcionDto> Tipos);

public sealed record TipologiaOpcionDto(long Id, string Nombre);

public sealed record SavePlantillaRequest(
    string Nombre,
    string? Descripcion,
    string? ContenidoHtml,
    IReadOnlyList<long> TipologiaIds,
    FormatoPapel FormatoPapel,
    OrientacionPapel Orientacion,
    MargenesPapel Margenes,
    string? Encabezado,
    string? PiePagina);

/// <summary>Variable de plantilla disponible en el panel del editor (RF09).</summary>
public sealed record VariableDto(string Grupo, string Token, string Etiqueta, bool Habilitada);
