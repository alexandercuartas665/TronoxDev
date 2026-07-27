using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Version de la Tabla de Retencion Documental (RQ02 - RF01). TENANT-SCOPED. Es el MARCO LEGAL
/// sobre el que RF04 construye el cruce Dependencia + Serie con sus tiempos de retencion y
/// disposicion final. Bloqueante para RF04.
///
/// Solo puede existir UNA version Vigente por tenant (RF01 3.1.4-2), garantizado por un indice
/// unico parcial en base de datos y por el servicio (que voltea la Vigente anterior a Historico al
/// activar una nueva). Nunca hay borrado fisico (invariante 8): una version se descarta pasando a
/// Inactivo. Cada expediente queda amarrado a la version Vigente al momento de crearse, y esa
/// referencia es INMUTABLE (DAT-03 / RF01 3.1.4-5): se resuelve en RQ03.
///
/// Equivale a la tabla legacy GEN_TRD_VERSION de doc_versionesTRD.aspx.
/// </summary>
public class TrdVersion : TenantEntity
{
    /// <summary>Codigo de la version. Ej: "TRD-2026-v1". UNICO POR TENANT (RF01 3.1.4-1).</summary>
    public string CodigoVersion { get; set; } = null!;

    /// <summary>Descripcion o justificacion de la version (opcional).</summary>
    public string? Descripcion { get; set; }

    /// <summary>Acto administrativo que la aprueba. Ej: "Resolucion 001 de 2026" (opcional).</summary>
    public string? ActoAdministrativo { get; set; }

    /// <summary>Fecha de inicio de vigencia (obligatoria).</summary>
    public DateOnly FechaVigenciaDesde { get; set; }

    /// <summary>Fecha del acto administrativo (opcional).</summary>
    public DateOnly? FechaAprobacion { get; set; }

    /// <summary>
    /// Fecha de convalidacion ante el ente rector. Solo aplica a entidades PUBLICAS (RF01 3.1.4-6):
    /// el dato tipo_entidad vive en RQ01 (Datos de la Entidad).
    /// </summary>
    public DateOnly? FechaConvalidacion { get; set; }

    public TrdVersionEstado Estado { get; set; } = TrdVersionEstado.EnConstruccion;
}
