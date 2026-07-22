using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

// ---- Niveles de clasificacion documental (RF01-P.3) ----

public sealed record NivelClasificacionDto(
    long Id,
    string Nombre,
    string Codigo,
    string? Descripcion,
    string? ColorEtiqueta,
    int NivelOrden,
    bool Activo);

public sealed record SaveNivelClasificacionRequest(
    long? Id,
    string Nombre,
    string Codigo,
    string? Descripcion,
    string? ColorEtiqueta,
    int NivelOrden,
    bool Activo);

// ---- Sedes (RF01 seccion 4.1.2) ----

public sealed record SedeDto(
    long Id,
    string NombreSede,
    string CodigoSede,
    string SiglaSede,
    long? PaisId,
    long? DepartamentoId,
    long? CiudadId,
    string Direccion,
    string? Telefono,
    string? CorreoSede,
    SedeEstado Estado);

public sealed record SaveSedeRequest(
    long? Id,
    string NombreSede,
    string CodigoSede,
    string SiglaSede,
    long? PaisId,
    long? DepartamentoId,
    long? CiudadId,
    string Direccion,
    string? Telefono,
    string? CorreoSede,
    SedeEstado Estado);

// ---- Fondos documentales (RF02) ----

public sealed record FondoDto(
    long Id,
    string CodigoFondo,
    string NombreFondo,
    string? Descripcion,
    long? SedeId,
    string? NombreSede,
    FondoTipo TipoFondo,
    FondoEstado Estado,
    DateOnly FechaApertura,
    DateOnly? FechaCierre,
    string? EntidadOrigen)
{
    /// <summary>
    /// SedeId null significa fondo TRANSVERSAL a toda la entidad (semantica explicita de la
    /// spec RF02), no "sin asignar".
    /// </summary>
    public bool EsTransversal => SedeId is null;

    /// <summary>Un fondo Cerrado es de solo lectura: consulta y exportacion si, altas no.</summary>
    public bool EsSoloLectura => Estado == FondoEstado.Cerrado;
}

public sealed record SaveFondoRequest(
    long? Id,
    string CodigoFondo,
    string NombreFondo,
    string? Descripcion,
    long? SedeId,
    FondoTipo TipoFondo,
    FondoEstado Estado,
    DateOnly FechaApertura,
    DateOnly? FechaCierre,
    string? EntidadOrigen);

// ---- Subfondos (RF02 seccion 5.2.2) ----

public sealed record SubfondoDto(
    long Id,
    long FondoId,
    string CodigoSubfondo,
    string NombreSubfondo,
    SubfondoEstado Estado);

public sealed record SaveSubfondoRequest(
    long? Id,
    long FondoId,
    string CodigoSubfondo,
    string NombreSubfondo,
    SubfondoEstado Estado);
