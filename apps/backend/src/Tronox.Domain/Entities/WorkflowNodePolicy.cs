using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Asignacion por nodo (RQ11, port de ECOREX ADR-0035): que Dependencia o Cargo del
/// organigrama (RQ01) atiende un paso Task del flujo. NO se asignan usuarios directos: el
/// resolver expande la unidad a los TenantUserIds candidatos (Funcionarios descendientes +
/// miembros + responsable). Solo se admiten OrgUnit con Classifier Dependencia o Cargo (un
/// Funcionario NUNCA es asignable; se valida en el servicio). FK al nodo en cascada; a la
/// unidad NO ACTION. Unico por (WorkflowNodeId, OrgUnitId). TENANT-SCOPED.
/// </summary>
public class WorkflowNodePolicy : TenantEntity
{
    public long WorkflowNodeId { get; set; }
    public WorkflowNode? WorkflowNode { get; set; }

    public long OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }

    /// <summary>Orden de la unidad entre las asignadas al nodo.</summary>
    public int SortOrder { get; set; }
}
