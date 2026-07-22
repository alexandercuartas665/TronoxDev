using Tronox.Domain.Enums;

namespace Tronox.Application.Common;

/// <summary>
/// Registra acciones sensibles del Super Admin/sistema en super_admin_audit_logs.
/// Solo agrega la entrada al contexto; el caso de uso decide cuando persistir (SaveChanges).
/// </summary>
public interface IAuditWriter
{
    void Write(
        long actorUserId,
        string actionName,
        string entityName,
        long? entityId,
        object? previousValue,
        object? newValue,
        long? tenantId = null,
        string? reason = null,
        AuditActorType actorType = AuditActorType.Human);
}
