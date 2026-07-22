using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Registro de auditoria de acciones sensibles del Super Admin / sistema (sec.12).
/// PreviousValue/NewValue se almacenan como jsonb. Entidad global.
/// </summary>
public class SuperAdminAuditLog : BaseEntity
{
    public long ActorUserId { get; set; }
    public AuditActorType ActorType { get; set; } = AuditActorType.Human;
    public string ActionName { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public long? EntityId { get; set; }
    public long? TenantId { get; set; }
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}
