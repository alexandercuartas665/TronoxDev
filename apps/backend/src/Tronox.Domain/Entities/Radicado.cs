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

    /// <summary>Asunto/descripcion breve del radicado.</summary>
    public string? Asunto { get; set; }

    // ---- Remitente (inline, espejo de RAD_RADICADOS.REMITENTE_NOMBRE/ANONIMO). Cuando exista RQ07
    // Terceros se agrega RemitenteTerceroId como fuente unica (invariante 2); por ahora snapshot. ----
    public string? RemitenteNombre { get; set; }
    public bool Anonimo { get; set; }

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

    /// <summary>Pistas de trazabilidad del radicado (append-only, RNF-04).</summary>
    public ICollection<RadicadoTrazabilidad> Trazas { get; set; } = new List<RadicadoTrazabilidad>();
}
