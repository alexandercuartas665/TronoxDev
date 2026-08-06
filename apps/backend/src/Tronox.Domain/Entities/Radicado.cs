using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Radicado: unidad de correspondencia de la ventanilla unica (RQ09). Espina dorsal del modulo de
/// radicacion; espejo del legacy RAD_RADICADOS (SQL Server, aislado por SUCURSAL) adaptado a Tronox:
/// TENANT-SCOPED (tenant_id + filtro global EF), enums como string, dependencias/funcionarios como FK
/// referenciales (no codigos sueltos) y remitente inline (como el legacy) hasta que exista RQ07 Terceros.
/// El Panel de Control (RF12) solo LEE esta tabla; los modulos de radicar/bandeja la escriben.
/// </summary>
public class Radicado : TenantEntity
{
    /// <summary>Consecutivo formateado segun el esquema de radicacion (RF01). Unico por tenant.</summary>
    public string NumeroRadicado { get; set; } = null!;

    public RadicadoTipo Tipo { get; set; }

    public RadicadoEstado Estado { get; set; } = RadicadoEstado.Borrador;

    public RadicadoCanal Canal { get; set; }

    public RadicadoPrioridad Prioridad { get; set; } = RadicadoPrioridad.Normal;

    /// <summary>Tipo de comunicacion radicable (RF01-2). FK a TipoComunicacion. NO ACTION.</summary>
    public long? TipoComunicacionId { get; set; }
    public TipoComunicacion? TipoComunicacion { get; set; }

    /// <summary>Asunto del radicado.</summary>
    public string? Asunto { get; set; }

    /// <summary>Descripcion ampliada (detalle).</summary>
    public string? Descripcion { get; set; }

    /// <summary>Numero de folios y de anexos, tipo de soporte (Fisico/Electronico/Hibrido).</summary>
    public int? Folios { get; set; }
    public int? NumAnexos { get; set; }
    public string? Soporte { get; set; }

    /// <summary>Nivel de clasificacion/reserva (FK NivelClasificacion, RF06 de RQ02). NO ACTION.</summary>
    public long? NivelReservaId { get; set; }
    public NivelClasificacion? NivelReserva { get; set; }

    // ---- Remitente (inline, espejo de RAD_RADICADOS.REMITENTE_*). Cuando exista RQ07 Terceros se
    // agrega RemitenteTerceroId como fuente unica (invariante 2, DAT-02); por ahora snapshot. ----
    public string? RemitenteNombre { get; set; }
    public bool Anonimo { get; set; }
    public string? RemitenteTipoDoc { get; set; }
    public string? RemitenteDocumento { get; set; }
    public string? RemitenteEmail { get; set; }
    public string? RemitenteTelefono { get; set; }

    // ---- Ruteo organico (FK a OrgUnit clasificador Dependencia; el legacy guardaba codigos). NO ACTION. ----
    public long? DependenciaDestinoId { get; set; }
    public OrgUnit? DependenciaDestino { get; set; }
    public long? DependenciaOrigenId { get; set; }
    public OrgUnit? DependenciaOrigen { get; set; }

    // ---- Funcionarios (FK a TenantUser). NO ACTION. ----
    public long? FuncionarioAsignadoId { get; set; }
    public long? FuncionarioOrigenId { get; set; }

    /// <summary>Usuario de ventanilla que radico.</summary>
    public long? UsuarioRadicaId { get; set; }

    // ---- Fechas del ciclo (SLA lo lleva RQ09, invariante 6; aqui solo se persisten los sellos). ----
    public DateTime FechaRadicacion { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public DateTime? FechaDistribucion { get; set; }

    // ---- Vinculacion padre/salidas (el legacy relaciona por NUMERO string; aqui FK self-ref robusta).
    // Padre = entrada a la que responde esta salida; las salidas de una entrada son los radicados que la
    // referencian. NO ACTION (self-ref, evita rutas de cascada multiples). ----
    public long? RadicadoRelacionadoId { get; set; }
    public Radicado? RadicadoRelacionado { get; set; }

    /// <summary>Solo en salidas (S): marca la respuesta definitiva vs parcial.</summary>
    public bool EsRespuestaDefinitiva { get; set; }

    // ---- Bloque de envio (se completa al portar rad_salida; el detalle lo lee para las salidas). ----
    public string? EstadoEnvio { get; set; }
    public string? CanalEnvio { get; set; }

    /// <summary>Pistas de trazabilidad del radicado (append-only, RNF-04).</summary>
    public ICollection<RadicadoTrazabilidad> Trazas { get; set; } = new List<RadicadoTrazabilidad>();

    /// <summary>Tareas de distribucion (RF07).</summary>
    public ICollection<RadicadoTarea> Tareas { get; set; } = new List<RadicadoTarea>();

    /// <summary>Archivos/anexos (referencia a object storage, invariante 9).</summary>
    public ICollection<RadicadoArchivo> Archivos { get; set; } = new List<RadicadoArchivo>();

    /// <summary>Comunicaciones/envios (RAD_COMUNICACIONES).</summary>
    public ICollection<RadicadoComunicacion> Comunicaciones { get; set; } = new List<RadicadoComunicacion>();
}
