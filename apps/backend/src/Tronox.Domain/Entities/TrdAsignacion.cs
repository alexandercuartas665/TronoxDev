using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Asignacion de una Serie a una Dependencia dentro de una version de TRD (RQ02 - RF04): el cruce
/// que produce el CCD y la TRD. TENANT-SCOPED.
///
/// Modelo: se COLAPSA la cabecera+detalle del legacy (GEN_TRD por dependencia + GEN_TRD_DETALLE por
/// serie) en una sola fila por (version, dependencia, serie). La "TRD por dependencia" es una vista
/// que se deriva agrupando por dependencia (mismo criterio de simplificacion que el arbol unico de
/// ADR-003). Una dependencia sin series simplemente no tiene asignaciones.
///
/// PERSONALIZACION POR DEPENDENCIA (RF04 3.4.2): la MISMA serie puede asignarse a varias
/// dependencias con tiempos/disposicion/clasificacion distintos (principio de procedencia). Lo que
/// NO se permite es la misma serie dos veces en la misma dependencia dentro de la misma version
/// (RF04 3.4.4-4): indice unico (version, dependencia, serie).
///
/// INMUTABILIDAD (DAT-03): cuando exista RQ03, cada expediente copiara el id de su asignacion de TRD
/// al nacer y esa referencia sera inmutable; esta tabla NO se recalcula sola.
/// </summary>
public class TrdAsignacion : TenantEntity
{
    /// <summary>Version de TRD bajo la que se crea (RF01). Solo En Construccion o Vigente.</summary>
    public long TrdVersionId { get; set; }
    public TrdVersion? TrdVersion { get; set; }

    /// <summary>Dependencia productora: nodo del arbol organizacional con clasificador Dependencia (RQ01).</summary>
    public long DependenciaOrgUnitId { get; set; }
    public OrgUnit? Dependencia { get; set; }

    /// <summary>Serie o subserie del catalogo (RF02). Solo Activas al asignarse.</summary>
    public long SerieDocumentalId { get; set; }
    public SerieDocumental? Serie { get; set; }

    /// <summary>
    /// Codigo CCD compuesto = codigoDependencia + "." + codigoSerie (RF04 3.4.1 paso 3). Se genera
    /// automaticamente y NO es editable por el usuario (RF04 3.4.4-2), salvo que la version este en
    /// modo EditarCodigo (ModoCodigoSerie).
    /// </summary>
    public string CodigoCcd { get; set; } = null!;

    /// <summary>Tiempo de retencion en Archivo de Gestion, en anios. Sin limite maximo (AGN 2.5).</summary>
    public int TiempoGestion { get; set; }

    /// <summary>Tiempo de retencion en Archivo Central, en anios. Sin limite maximo.</summary>
    public int TiempoCentral { get; set; }

    /// <summary>Disposicion final CT/S/E (mutuamente excluyente, RF04 3.4.1 paso 4).</summary>
    public DisposicionFinal DisposicionFinal { get; set; }

    /// <summary>Reproduccion tecnica del papel (M/D). Complementario a la disposicion final.</summary>
    public bool ReproduccionTecnica { get; set; }

    /// <summary>Serie con documentos de DD.HH / DIH.</summary>
    public bool SerieDdhhDih { get; set; }

    /// <summary>Descripcion del procedimiento asociado (opcional).</summary>
    public string? Procedimiento { get; set; }

    /// <summary>
    /// Nivel de clasificacion documental de la serie EN ESTA dependencia (RF04 3.4.1 paso 5).
    /// Punto de partida de la herencia en cascada (RQ02 seccion 2). Default: Interno.
    /// </summary>
    public long NivelClasificacionId { get; set; }
    public NivelClasificacion? NivelClasificacion { get; set; }

    /// <summary>
    /// Metadatos de la asignacion: incluye los del EXPEDIENTE (contexto = Expediente, RF04 paso 6) y
    /// los del DOCUMENTO (contexto = Documento, RF05) que ademas cuelgan de una tipologia. Se separan
    /// por Contexto/TrdTipologiaId al proyectar.
    /// </summary>
    public ICollection<TrdMetadato> Metadatos { get; set; } = [];

    /// <summary>
    /// Tipos documentales (tipologias) de esta asignacion Dependencia+Serie (RF05 3.5.1). Cada uno
    /// con su soporte, formato y metadatos de documento.
    /// </summary>
    public ICollection<TrdTipologia> Tipologias { get; set; } = [];

    /// <summary>
    /// Inactivacion en vez de borrado fisico (invariante 8). Sobre una version Vigente NO se puede
    /// eliminar una serie (RF01 3.1.3); en En Construccion se inactiva/reactiva.
    /// </summary>
    public bool IsArchived { get; set; }
}
