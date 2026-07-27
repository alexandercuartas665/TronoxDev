using Tronox.Domain.Enums;

namespace Tronox.Application.Listas;

/// <summary>Vista de una lista con sus opciones y su estado de usabilidad (RF03).</summary>
public sealed record ListaMaestraDto(
    long Id,
    string NombreLista,
    string? Descripcion,
    ListaEstado Estado,
    IReadOnlyList<ListaOpcionDto> Opciones)
{
    public bool EsInactiva => Estado == ListaEstado.Inactivo;
    public int OpcionesActivas => Opciones.Count(o => o.Estado == ListaEstado.Activo);
    /// <summary>RF03 3.3.4-2: usable en un metadato si esta Activa y tiene >= 2 opciones activas.</summary>
    public bool EsUsable => !EsInactiva && OpcionesActivas >= ListaRules.MinOpcionesActivas;
}

public sealed record ListaOpcionDto(
    long Id,
    long ListaMaestraId,
    string Clave,
    string Valor,
    int Orden,
    ListaEstado Estado)
{
    public bool EsInactiva => Estado == ListaEstado.Inactivo;
}

/// <summary>KPIs: total de listas, listas usables (>= 2 opciones activas) e inactivas.</summary>
public sealed record ListaKpisDto(int Total, int Usables, int Inactivas);

public sealed record SaveListaRequest(string NombreLista, string? Descripcion = null);

/// <summary>Alta/edicion de una opcion. El orden se asigna automaticamente al crear.</summary>
public sealed record SaveOpcionRequest(string Clave, string Valor);
