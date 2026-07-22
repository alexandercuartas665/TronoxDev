using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Application.Common;

/// <summary>
/// Registra acciones sensibles del Super Admin/sistema en super_admin_audit_logs.
/// Solo agrega la entrada al contexto; el caso de uso decide cuando persistir (SaveChanges).
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Forma PREFERENTE: recibe la ENTIDAD auditada, no su id. El Id es identity de la base y
    /// vale 0 hasta que EF lo materializa durante SaveChanges, asi que auditar un alta con
    /// "entidad.Id" produce un asiento que no identifica nada (incumple RNF-04). Pasando la
    /// entidad, el asiento se resuelve y se inserta cuando el id real ya existe, dentro de la
    /// misma transaccion (ver IApplicationDbContext.DeferUntilIdsAssigned).
    ///
    /// Sirve igual para altas y para modificaciones/bajas: si la entidad ya tiene id, se usa tal
    /// cual. Cuando tengas la entidad a mano, usa SIEMPRE esta forma.
    ///
    /// tenantId: si se omite (o llega en 0) se deduce de la propia entidad cuando esta es
    /// tenant-scoped o es el propio Tenant, tambien de forma diferida.
    /// </summary>
    void Write(
        long actorUserId,
        string actionName,
        string entityName,
        BaseEntity entity,
        object? previousValue,
        object? newValue,
        long? tenantId = null,
        string? reason = null,
        AuditActorType actorType = AuditActorType.Human);

    /// <summary>
    /// Forma por id: usar SOLO cuando no se tiene la entidad a mano (p.ej. se recibio un id por
    /// parametro y ya existe en la base). NO usarla para altas: el id de una entidad recien
    /// agregada todavia vale 0.
    /// </summary>
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
