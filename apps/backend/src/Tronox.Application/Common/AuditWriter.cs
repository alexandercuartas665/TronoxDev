using System.Text.Json;
using Tronox.Domain.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Common;

public sealed class AuditWriter : IAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _db;

    public AuditWriter(IApplicationDbContext db) => _db = db;

    public void Write(
        long actorUserId,
        string actionName,
        string entityName,
        BaseEntity entity,
        object? previousValue,
        object? newValue,
        long? tenantId = null,
        string? reason = null,
        AuditActorType actorType = AuditActorType.Human)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // El valor auditado se serializa AHORA (es el estado en el momento del hecho); lo unico
        // que se difiere es el id, que la base aun no ha generado.
        var log = Build(actorUserId, actionName, entityName, entityId: null,
            previousValue, newValue, tenantId: null, reason, actorType);

        _db.DeferUntilIdsAssigned(() =>
        {
            log.EntityId = entity.Id;
            log.TenantId = ResolveTenantId(entity, tenantId);
            _db.SuperAdminAuditLogs.Add(log);
        });
    }

    public void Write(
        long actorUserId,
        string actionName,
        string entityName,
        long? entityId,
        object? previousValue,
        object? newValue,
        long? tenantId = null,
        string? reason = null,
        AuditActorType actorType = AuditActorType.Human)
    {
        _db.SuperAdminAuditLogs.Add(Build(actorUserId, actionName, entityName, entityId,
            previousValue, newValue, tenantId, reason, actorType));
    }

    /// <summary>
    /// Tenant del asiento: el explicito si es utilizable, si no el de la propia entidad. El
    /// TenantId de una entidad recien agregada tambien puede valer 0 (lo sella el interceptor
    /// durante SaveChanges), por eso se resuelve diferido igual que el EntityId.
    /// </summary>
    private static long? ResolveTenantId(BaseEntity entity, long? explicitTenantId)
    {
        if (explicitTenantId is long given && given != 0)
        {
            return given;
        }

        return entity switch
        {
            ITenantScoped scoped when scoped.TenantId != 0 => scoped.TenantId,
            Tenant tenant => tenant.Id,
            _ => explicitTenantId
        };
    }

    private static SuperAdminAuditLog Build(
        long actorUserId,
        string actionName,
        string entityName,
        long? entityId,
        object? previousValue,
        object? newValue,
        long? tenantId,
        string? reason,
        AuditActorType actorType) => new()
        {
            ActorUserId = actorUserId,
            ActorType = actorType,
            ActionName = actionName,
            EntityName = entityName,
            EntityId = entityId,
            TenantId = tenantId,
            PreviousValue = previousValue is null ? null : JsonSerializer.Serialize(previousValue, JsonOptions),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            Reason = reason
        };
}
