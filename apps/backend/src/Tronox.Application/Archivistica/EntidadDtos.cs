using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

// ---- Datos de la Entidad (RF01 seccion 4.1.1) ----

public sealed record EntidadDto(
    long Id,
    string Nit,
    string DigitoVerificacion,
    string RazonSocial,
    string Sigla,
    TipoEntidad TipoEntidad,
    string? NaturalezaJuridica,
    string? CodigoDivipola,
    long? PaisId,
    string? PaisNombre,
    long? DepartamentoId,
    string? DepartamentoNombre,
    long? CiudadId,
    string? CiudadNombre,
    string DireccionPrincipal,
    string? Telefono,
    string CorreoInstitucional,
    string? PaginaWeb,
    string RepresentanteLegal,
    string? LogoUrl,
    string? CodigoFondoAgn,
    string ZonaHoraria,
    string IdiomaDefecto,
    EntidadEstado Estado)
{
    /// <summary>NIT con su digito de verificacion, como se imprime en documentos oficiales.</summary>
    public string NitCompleto => $"{Nit}-{DigitoVerificacion}";

    /// <summary>Criterio 4 de RF01: en una entidad Publica, DIVIPOLA y AGN son obligatorios.</summary>
    public bool RequiereDatosAgn => EntidadRules.RequiereDatosAgn(TipoEntidad);
}

/// <summary>
/// Alta/edicion de la entidad. NO lleva Id: hay UNA sola entidad por tenant (criterio 1 de
/// RF01), asi que el servicio la resuelve por tenant y decide si crea o actualiza.
///
/// Tampoco lleva CodigoFondoAgn: se GENERA a partir de DIVIPOLA + sigla (resolucion M01).
/// El unico camino para fijarlo a mano es <see cref="CodigoFondoAgnManual"/>, reservado al
/// caso excepcional documentado del Super Administrador de plataforma.
/// </summary>
public sealed record SaveEntidadRequest(
    string Nit,
    string DigitoVerificacion,
    string RazonSocial,
    string Sigla,
    TipoEntidad TipoEntidad,
    string? NaturalezaJuridica,
    string? CodigoDivipola,
    long? PaisId,
    long? DepartamentoId,
    long? CiudadId,
    string DireccionPrincipal,
    string? Telefono,
    string CorreoInstitucional,
    string? PaginaWeb,
    string RepresentanteLegal,
    string? LogoUrl,
    string ZonaHoraria,
    string IdiomaDefecto,
    EntidadEstado Estado,
    string? CodigoFondoAgnManual = null);

// ---- Catalogos territoriales DIVIPOLA (pendiente P-02 de RQ01) ----

public sealed record PaisDto(long Id, string CodigoIso2, string CodigoIso3, string Nombre);

public sealed record DepartamentoDto(long Id, long PaisId, string CodigoDane, string Nombre);

public sealed record MunicipioDto(
    long Id, long DepartamentoId, string CodigoDivipola, string Nombre, bool EsCapital);
