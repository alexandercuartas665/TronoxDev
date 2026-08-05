using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Enlace entre una respuesta de formulario y el paso de una instancia de flujo BPMN (RQ08 x RQ11):
/// cuando un nodo tiene formulario asignado (<see cref="WorkflowNodeForm"/>), al llegar el paso se
/// crea un enlace Pending; al enviar la respuesta pasa a Completed y el motor completa el paso
/// (propagando el resultado de aprobacion para las compuertas). TENANT-SCOPED.
/// </summary>
public class FormFlowLink : TenantEntity
{
    public long FormResponseId { get; set; }
    public FormResponse? FormResponse { get; set; }

    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public long WorkflowNodeId { get; set; }
    public WorkflowNode? WorkflowNode { get; set; }

    public FormFlowLinkStatus Status { get; set; } = FormFlowLinkStatus.Pending;
}
