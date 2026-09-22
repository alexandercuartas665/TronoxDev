using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Tercero: directorio maestro UNICO de personas naturales y juridicas externas a la entidad (RQ07,
/// invariante DAT-02). Fuente unica de verdad; los modulos (Radicacion, PQRSD, Contratos, RRHH, Portal)
/// referencian el tercero por su Id, no crean tablas propias de personas externas.
///
/// La ficha se adapta al <see cref="Subtipo"/>. El contacto principal (email/telefono/direccion/municipio)
/// vive inline; los contactos multiples, etiquetas (roles contextuales), metadatos dinamicos (RQ02) y la
/// auditoria append-only propia quedan como fases siguientes (satelites de la spec, aun no materializados).
/// TENANT-SCOPED. Unicidad por (tenant, tipo_documento, numero_documento): nunca se duplica un documento.
/// </summary>
public class Tercero : TenantEntity
{
    public TerceroSubtipo Subtipo { get; set; }

    // ---- Identificacion. ----
    /// <summary>Codigo del tipo de documento (CC, CE, PA, TI, RC, PEP, PPT, NIT, DE). Maestro tipos_documento diferido.</summary>
    public string TipoDocumento { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    /// <summary>Digito de verificacion, solo NIT.</summary>
    public string? DigitoVerificador { get; set; }

    // ---- Nombres (segun subtipo). ----
    public string? RazonSocial { get; set; }
    public string? Nombre { get; set; }
    public string? Apellidos { get; set; }
    public string? NombreComercial { get; set; }

    // ---- Contacto principal (inline; contactos multiples diferidos a terceros_contactos). ----
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    /// <summary>Municipio DIVIPOLA del contacto principal (FK Municipio, catalogo global).</summary>
    public long? MunicipioId { get; set; }
    public Municipio? Municipio { get; set; }
    /// <summary>Pais de constitucion/origen (extranjeros). FK Pais.</summary>
    public long? PaisId { get; set; }
    public Pais? Pais { get; set; }

    // ---- Datos juridicos (opcionales). ----
    public string? SitioWeb { get; set; }
    public string? SectorEconomico { get; set; }
    public string? RegimenTributario { get; set; }
    // Juridica publica.
    public string? SectorAdministrativo { get; set; }
    /// <summary>Orden de la entidad publica: Nacional / Territorial.</summary>
    public string? OrdenEntidad { get; set; }
    public string? NaturalezaJuridica { get; set; }

    /// <summary>Representante legal: otra Persona Natural del mismo catalogo (autorreferencia). NO ACTION.</summary>
    public long? RepresentanteLegalId { get; set; }
    public Tercero? RepresentanteLegal { get; set; }

    // ---- Estado y trazabilidad. ----
    public TerceroEstado Estado { get; set; } = TerceroEstado.Activo;
    /// <summary>false = creado desde radicacion con datos minimos (indicador "Incompleto").</summary>
    public bool PerfilCompleto { get; set; }
    public string? Observaciones { get; set; }
    /// <summary>Motivo de la ultima inactivacion (obligatorio al inactivar, RF01-5). La eliminacion no existe.</summary>
    public string? MotivoInactivacion { get; set; }
    /// <summary>Origen del alta: Directorio (manual), Radicacion, Salida, Importacion.</summary>
    public string Origen { get; set; } = "Directorio";
}
