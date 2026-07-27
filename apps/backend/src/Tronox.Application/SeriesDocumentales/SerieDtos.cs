using Tronox.Domain.Enums;

namespace Tronox.Application.SeriesDocumentales;

/// <summary>Nodo del arbol del catalogo (serie o subserie) con sus hijos anidados (RF02 3.2.2).</summary>
public sealed record SerieNodeDto(
    long Id,
    long? ParentId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    SerieEstado Estado,
    IReadOnlyList<SerieNodeDto> Children)
{
    public bool EsInactiva => Estado == SerieEstado.Inactivo;
    public bool EsSerie => ParentId is null;
    public int ChildCount => Children.Count;
}

/// <summary>Vista plana de una serie/subserie (para el detalle y el resultado de un guardado).</summary>
public sealed record SerieDto(
    long Id,
    long? ParentId,
    string? ParentNombre,
    string Codigo,
    string Nombre,
    string? Descripcion,
    SerieEstado Estado)
{
    public bool EsInactiva => Estado == SerieEstado.Inactivo;
    public bool EsSerie => ParentId is null;
}

/// <summary>KPIs del catalogo: series principales, subseries, activas e inactivas.</summary>
public sealed record SerieKpisDto(int Series, int Subseries, int Activas, int Inactivas);

/// <summary>
/// Alta/edicion de una serie o subserie (RF02 3.2.1). ParentId null = Serie principal; con valor
/// = Subserie. El estado no se edita aqui: se cambia con SetEstadoAsync (invariante 8: nunca
/// borrado, se inactiva).
/// </summary>
public sealed record SaveSerieRequest(
    string Codigo,
    string Nombre,
    string? Descripcion = null,
    long? ParentId = null);
