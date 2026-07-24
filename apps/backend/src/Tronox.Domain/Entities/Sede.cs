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
    // Los catalogos (Pais / Departamento / Municipio) YA existen y estas tres columnas son FK
    // reales contra ellos. Siguen NULLABLE, no NOT NULL como pedia la spec: las sedes creadas
    // antes del catalogo no tienen ubicacion, y volverlas obligatorias en base de datos las
    // dejaria sin poder guardarse. La obligatoriedad se hara cumplir en ArchivisticaRules
    // cuando se decida migrar los datos existentes.
    public long? PaisId { get; set; }
    public long? DepartamentoId { get; set; }
    public long? CiudadId { get; set; }

    public string Direccion { get; set; } = null!;

    public string? Telefono { get; set; }

    public string? CorreoSede { get; set; }

    public SedeEstado Estado { get; set; } = SedeEstado.Activo;
}
