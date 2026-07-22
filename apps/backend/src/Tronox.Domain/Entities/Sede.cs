using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Sede fisica de la entidad (RQ01 - RF01 seccion 4.1.2). TENANT-SCOPED.
///
/// Las sedes son OPCIONALES: una entidad puede operar sin ninguna, y en ese caso todos sus
/// fondos son transversales (Fondo.SedeId = null). Nunca se borran: se inactivan, y una sede
/// Inactiva no se ofrece al crear fondos.
/// </summary>
public class Sede : TenantEntity
{
    public string NombreSede { get; set; } = null!;

    /// <summary>Codigo de la sede. UNICO POR TENANT, no global.</summary>
    public string CodigoSede { get; set; } = null!;

    public string SiglaSede { get; set; } = null!;

    // --- Ubicacion DIVIPOLA ---
    // La spec los declara obligatorios, pero los catalogos DIVIPOLA (pais / departamento /
    // ciudad) todavia no existen como tablas: no hay a que apuntar ni FK que declarar. Se
    // dejan como columnas nullable sin FK -> punto de extension. Cuando los catalogos entren,
    // se convierten en FK NOT NULL y la validacion de obligatoriedad se activa en SedeRules.
    public long? PaisId { get; set; }
    public long? DepartamentoId { get; set; }
    public long? CiudadId { get; set; }

    public string Direccion { get; set; } = null!;

    public string? Telefono { get; set; }

    public string? CorreoSede { get; set; }

    public SedeEstado Estado { get; set; } = SedeEstado.Activo;
}
