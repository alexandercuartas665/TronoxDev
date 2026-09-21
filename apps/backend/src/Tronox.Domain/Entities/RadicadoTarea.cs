using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Tarea de distribucion de un radicado (RAD_TAREAS). Representa el encargo de gestion dirigido a una
/// dependencia y (opcional) a un funcionario. Una por dependencia destinataria; al redistribuir se
/// cierran las activas (Activa=0, estado Reasignada) y se crea una nueva. Adaptaciones de invariante:
/// dependencia -> FK OrgUnit, funcionario/distribuido_por -> FK TenantUser (el legacy usaba nombre de
/// texto libre). TENANT-SCOPED. Cascade con el radicado.
/// </summary>
public class RadicadoTarea : TenantEntity
{
    public long RadicadoId { get; set; }
    public Radicado? Radicado { get; set; }

    /// <summary>Dependencia destino (OrgUnit classifier Dependencia). NO ACTION.</summary>
    public long DependenciaId { get; set; }
    public OrgUnit? Dependencia { get; set; }

    /// <summary>Funcionario asignado (TenantUser). Null = pendiente de asignar por el jefe. NO ACTION.</summary>
    public long? FuncionarioId { get; set; }

    public string? Instrucciones { get; set; }

    public RadicadoPrioridad Prioridad { get; set; } = RadicadoPrioridad.Normal;

    public RadicadoTareaEstado Estado { get; set; } = RadicadoTareaEstado.Asignada;

    /// <summary>Vigencia de la tarea (columna aparte del Estado, fiel al legacy).</summary>
    public bool Activa { get; set; } = true;

    /// <summary>Procedencia de la tarea (default "Distribucion").</summary>
    public string Origen { get; set; } = "Distribucion";

    /// <summary>Usuario que distribuyo (TenantUser), para notificar en rechazo. NO ACTION.</summary>
    public long? DistribuidoPorId { get; set; }

    public DateTime FechaAsignacion { get; set; }

    /// <summary>Fecha de aceptacion/rechazo/reasignacion.</summary>
    public DateTime? FechaGestion { get; set; }

    /// <summary>Observacion del rechazo o justificacion de la reasignacion.</summary>
    public string? Observacion { get; set; }
}
