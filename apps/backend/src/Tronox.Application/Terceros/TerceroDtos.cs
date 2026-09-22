using Tronox.Domain.Enums;

namespace Tronox.Application.Terceros;

/// <summary>Fila del catalogo de terceros (RF01 bandeja).</summary>
public sealed record TerceroListItemDto(
    long Id, string Nombre, TerceroSubtipo Subtipo, string SubtipoLabel, string TipoDocumento,
    string NumeroDocumento, string? Contacto, TerceroEstado Estado, bool PerfilCompleto);

/// <summary>Filtro del catalogo. Buscar cruza documento/nombre/razon/comercial/email desde el 3er caracter.</summary>
public sealed record TerceroFiltro(
    string? Buscar = null,
    string? Grupo = null,       // natural | juridica | (null = todos)
    TerceroEstado? Estado = null,
    bool? SoloIncompletos = null,
    int Top = 100);

public sealed record TerceroListResult(IReadOnlyList<TerceroListItemDto> Items, int Total);

/// <summary>Ficha completa para crear/editar un tercero (RF01 formulario dinamico por subtipo).</summary>
public sealed record TerceroFichaDto(
    long Id,
    TerceroSubtipo Subtipo,
    string TipoDocumento,
    string NumeroDocumento,
    string? DigitoVerificador,
    string? RazonSocial,
    string? Nombre,
    string? Apellidos,
    string? NombreComercial,
    string? Email,
    string? Telefono,
    string? Direccion,
    long? MunicipioId,
    long? PaisId,
    string? SitioWeb,
    string? SectorEconomico,
    string? RegimenTributario,
    string? SectorAdministrativo,
    string? OrdenEntidad,
    string? NaturalezaJuridica,
    long? RepresentanteLegalId,
    string? Observaciones,
    TerceroEstado Estado,
    bool PerfilCompleto);

/// <summary>Datos minimos para crear/actualizar un tercero desde otro modulo (RF01-4 creacion rapida).</summary>
public sealed record CrearRapidoRequest(
    TerceroSubtipo Subtipo, string TipoDocumento, string NumeroDocumento,
    string? Nombre, string? Apellidos, string? RazonSocial,
    string? Email, string? Telefono, long? MunicipioId, string Origen);

/// <summary>Sugerencia de autocompletado desde el catalogo (dedup por documento).</summary>
public sealed record TerceroSugerenciaDto(
    long Id, TerceroSubtipo Subtipo, string TipoDocumento, string NumeroDocumento,
    string? Nombre, string? Apellidos, string? RazonSocial, string? Email, string? Telefono, long? MunicipioId);

public sealed record TerceroResult(bool Ok, string? Error = null, long? Id = null)
{
    public static TerceroResult Fail(string error) => new(false, error);
    public static TerceroResult Success(long id) => new(true, null, id);
}
