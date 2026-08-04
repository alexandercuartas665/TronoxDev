using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Expediente electronico: el CONTENEDOR archivistico (RQ03). Agrupa documentos (RQ04) bajo una
/// serie/subserie de la TRD, en una dependencia productora (principio de procedencia). TENANT-SCOPED.
///
/// INMUTABILIDAD TRD (DAT-03 / RF04 legacy): al nacer copia el id de su asignacion de TRD
/// (<see cref="TrdAsignacionId"/>) y esa referencia NO se recalcula nunca (Acuerdo AGN 002/2014). De
/// la asignacion se derivan version, dependencia, serie/subserie y el CCD; no se denormalizan aqui.
///
/// CLASIFICACION (RF10): el nivel propio del expediente parte del heredado de la asignacion y solo se
/// puede ELEVAR, nunca bajar. Por eso <see cref="NivelClasificacionId"/> es columna propia, no derivada.
///
/// CODIGO (RF04): estructurado [Dependencia]-[Serie][Subserie]-[Anio]-[Consecutivo], inmutable.
/// </summary>
public class Expediente : TenantEntity
{
    /// <summary>Codigo estructurado, unico por tenant, INMUTABLE tras la creacion (RF04).</summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Nombre del expediente, asignado manualmente por el usuario (RF03).</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>
    /// Asignacion de TRD (Dependencia+Serie en una version) bajo la que nace. INMUTABLE (DAT-03).
    /// Fuente de version, dependencia, serie/subserie, CCD y nivel de clasificacion heredado.
    /// </summary>
    public long TrdAsignacionId { get; set; }
    public TrdAsignacion? TrdAsignacion { get; set; }

    /// <summary>
    /// Nivel de clasificacion propio del expediente (RF10). Parte del heredado de la asignacion y solo
    /// se puede elevar. Determina la visibilidad fail-closed en la bandeja y en la busqueda.
    /// </summary>
    public long NivelClasificacionId { get; set; }
    public NivelClasificacion? NivelClasificacion { get; set; }

    /// <summary>Estado de tramite (RF13): Abierto / Cerrado.</summary>
    public EstadoExpediente Estado { get; set; } = EstadoExpediente.Abierto;

    /// <summary>Fase del ciclo vital (RF13): Gestion / Central / Historico.</summary>
    public FaseArchivo Fase { get; set; } = FaseArchivo.Gestion;

    /// <summary>Estado de ubicacion fisica (RF12).</summary>
    public EstadoUbicacionExpediente EstadoUbicacion { get; set; } = EstadoUbicacionExpediente.SinUbicar;

    /// <summary>Fecha de apertura del expediente (RF03). Default hoy, editable, no futura.</summary>
    public DateOnly FechaApertura { get; set; }

    /// <summary>Fecha de cierre (RF08). Null mientras esta Abierto.</summary>
    public DateOnly? FechaCierre { get; set; }

    /// <summary>Metadatos dinamicos del expediente segun la serie/subserie (DAT-04, motor RQ02).</summary>
    public ICollection<ExpedienteMetadato> Metadatos { get; set; } = [];

    // ---- Eliminacion logica (invariante 8: sin borrado fisico; RF01 accion Eliminar deja auditoria) ----

    /// <summary>Eliminacion logica con motivo y auditoria. Nunca hay DELETE fisico.</summary>
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
    public long? EliminadoPorUserId { get; set; }
    public string? JustificacionEliminacion { get; set; }
}
