using Tronox.Domain.Enums;

namespace Tronox.Application.Topografia;

/// <summary>Tipo de nivel configurado (RF06 3.6.1).</summary>
public sealed record TopografiaNivelDto(
    long Id,
    string NombreNivel,
    string SiglaBase,
    int Orden,
    bool ControlaCapacidad);

/// <summary>Nodo del arbol topografico con su codigo, ocupacion y estado efectivo (RF06 3.6.4).</summary>
public sealed record TopografiaElementoNodeDto(
    long Id,
    long NivelId,
    string NivelNombre,
    long? ParentId,
    string Nombre,
    string Sigla,
    string CodigoTopografico,
    int? Capacidad,
    int Ocupacion,
    bool ControlaCapacidad,
    TopografiaEstado Estado,
    IReadOnlyList<TopografiaElementoNodeDto> Children)
{
    public bool EsInactivo => Estado == TopografiaEstado.Inactivo;
    public int? PorcentajeOcupacion => Capacidad is int c && c > 0 ? (int)Math.Round(Ocupacion * 100.0 / c) : null;
}

/// <summary>KPIs del modulo: niveles configurados, elementos totales, y elementos llenos.</summary>
public sealed record TopografiaKpisDto(int Niveles, int Elementos, int Llenos, int Inactivos);

public sealed record SaveNivelRequest(
    string NombreNivel,
    string SiglaBase,
    int Orden,
    bool ControlaCapacidad = false);

/// <summary>
/// Alta/edicion de un elemento (RF06 3.6.2). En edicion, NivelId y ParentId se ignoran (el tipo y
/// el contenedor quedan fijos, paridad con el legacy).
/// </summary>
public sealed record SaveElementoRequest(
    long NivelId,
    long? ParentId,
    string Nombre,
    string Sigla,
    int? Capacidad = null);
