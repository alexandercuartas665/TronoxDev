using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Datos de la entidad titular del tenant (RQ01 - RF01 seccion 4.1.1). TENANT-SCOPED.
///
/// UNA SOLA FILA POR TENANT (criterio de aceptacion 1 de RF01): la pantalla es "editar", no
/// "listar/crear". La unicidad la garantiza un indice unico sobre tenant_id, no solo el
/// servicio: si un segundo camino intentara insertar otra, la base lo rechaza.
///
/// NUNCA se elimina (criterio 8 e invariante 8): solo cambia <see cref="Estado"/>.
///
/// Es el metadato RAIZ del sistema: <see cref="Sigla"/> alimenta el esquema de radicacion
/// (RF01-P.5) y <see cref="CodigoFondoAgn"/> se estampa en todos los expedientes y documentos,
/// asi que ninguno de los dos es cosmetico.
/// </summary>
public class Entidad : TenantEntity
{
    /// <summary>NIT sin puntos ni digito de verificacion (15). Unico por tenant.</summary>
    public string Nit { get; set; } = null!;

    /// <summary>
    /// Digito de verificacion del NIT (1 caracter). Se CALCULA con el algoritmo de la DIAN
    /// (EntidadRules.CalcularDigitoVerificacion) y se persiste para no recalcularlo en cada
    /// lectura ni en los documentos que lo imprimen.
    /// </summary>
    public string DigitoVerificacion { get; set; } = null!;

    public string RazonSocial { get; set; } = null!;

    /// <summary>
    /// Sigla de la entidad. MAXIMO 10 caracteres por la resolucion M01 (la spec decia 20):
    /// entra literal en el codigo de fondo AGN, y una sigla larga produce codigos malformados
    /// estampados en todos los expedientes. Alimenta ademas el esquema de radicacion.
    /// </summary>
    public string Sigla { get; set; } = null!;

    public TipoEntidad TipoEntidad { get; set; } = TipoEntidad.Publica;

    public string? NaturalezaJuridica { get; set; }

    /// <summary>
    /// Codigo DIVIPOLA del DANE (5 digitos). OBLIGATORIO si la entidad es Publica.
    /// Es la mitad territorial del codigo de fondo AGN.
    /// </summary>
    public string? CodigoDivipola { get; set; }

    // ---- Ubicacion (catalogos DIVIPOLA, selectores ENCADENADOS) ----
    // Nullable en base de datos, obligatorios por regla de negocio: una fila anterior a los
    // catalogos no puede quedar sin poder guardarse, pero EntidadRules los exige al guardar.
    public long? PaisId { get; set; }
    public Pais? Pais { get; set; }

    public long? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    public long? CiudadId { get; set; }
    public Municipio? Ciudad { get; set; }

    public string DireccionPrincipal { get; set; } = null!;

    public string? Telefono { get; set; }

    public string CorreoInstitucional { get; set; } = null!;

    public string? PaginaWeb { get; set; }

    public string RepresentanteLegal { get; set; } = null!;

    /// <summary>
    /// URL del logo. Se guarda la RUTA, jamas los bytes (invariante 9: binarios fuera de la
    /// base de datos). Hoy apunta a wwwroot/uploads; cuando entre MinIO cambia la ruta, no el
    /// modelo.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Codigo del fondo ante el AGN, patron CO-{DIVIPOLA}-{SIGLA}. AUTO-GENERADO (resolucion
    /// M01): no se captura a mano. OBLIGATORIO si la entidad es Publica.
    /// </summary>
    public string? CodigoFondoAgn { get; set; }

    /// <summary>
    /// Zona horaria IANA (ej. "America/Bogota"). CRITICO: los timestamps se persisten en UTC y
    /// se presentan en esta zona. Nunca se almacena hora local.
    /// </summary>
    public string ZonaHoraria { get; set; } = "America/Bogota";

    /// <summary>Idioma por defecto de la entidad (BCP-47, ej. "es-CO").</summary>
    public string IdiomaDefecto { get; set; } = "es-CO";

    public EntidadEstado Estado { get; set; } = EntidadEstado.Activo;
}
