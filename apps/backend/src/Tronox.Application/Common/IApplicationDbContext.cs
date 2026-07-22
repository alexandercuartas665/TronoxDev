using Tronox.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Tronox.Application.Common;

/// <summary>
/// Abstraccion del DbContext para los casos de uso de Application, sin acoplar a la
/// implementacion concreta de Infrastructure. Expone solo los conjuntos que la capa necesita.
/// </summary>
public interface IApplicationDbContext
{
    // Plataforma y tenants (base de RQ14 - TRONOX Console).
    DbSet<PlatformUser> PlatformUsers { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantConfiguration> TenantConfigurations { get; }
    DbSet<TenantSequence> TenantSequences { get; }
    DbSet<TenantModule> TenantModules { get; }
    DbSet<TenantApiConfig> TenantApiConfigs { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<TenantPayment> TenantPayments { get; }
    DbSet<SaasPlan> SaasPlans { get; }
    DbSet<SaasPlanLimit> SaasPlanLimits { get; }
    DbSet<ModuleDefinition> ModuleDefinitions { get; }
    DbSet<PlatformBranding> PlatformBrandings { get; }

    // Identidad y acceso del tenant (base de RQ01 - RF06/RF07).
    DbSet<TenantUser> TenantUsers { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<AccountActivationCode> AccountActivationCodes { get; }
    DbSet<GoogleAuthConfig> GoogleAuthConfigs { get; }

    // Menu configurable por tenant (base de RQ01 - RF09, ver ADR-001).
    DbSet<MenuView> MenuViews { get; }
    DbSet<MenuNode> MenuNodes { get; }

    // Roles y matriz de permisos Modulo x Accion (base de RQ01 - RF05).
    DbSet<Rol> Roles { get; }
    DbSet<RolPermiso> RolPermisos { get; }

    // Configuracion archivistica (base de RQ01 - RF01-P.3 y RF02): niveles de clasificacion
    // documental, sedes, fondos y subfondos. Todas tenant-scoped.
    DbSet<NivelClasificacion> NivelesClasificacion { get; }
    DbSet<Sede> Sedes { get; }
    DbSet<Fondo> Fondos { get; }
    DbSet<Subfondo> Subfondos { get; }

    // Estructura organizacional (base de RQ01 - RF03/RF04).
    DbSet<OrgUnit> OrgUnits { get; }
    DbSet<OrgUnitMember> OrgUnitMembers { get; }
    DbSet<BusinessUnit> BusinessUnits { get; }

    // Gateway de IA multi-proveedor y consumo (base de RQ16).
    DbSet<AiProviderConfig> AiProviderConfigs { get; }
    DbSet<AiUsageLog> AiUsageLogs { get; }

    // Correo saliente por tenant (base de RQ01 - RF01-P.2).
    DbSet<EmailConfig> EmailConfigs { get; }

    // Notificaciones y pista de auditoria (RNF-04: append-only).
    DbSet<Notification> Notifications { get; }
    DbSet<SuperAdminAuditLog> SuperAdminAuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre una transaccion explicita para casos de uso multi-paso (ej. emitir un consecutivo
    /// de radicado e insertar el radicado de forma atomica). Los casos simples siguen usando
    /// SaveChangesAsync solo.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si ya hay una transaccion abierta sobre la conexion. Permite que un caso de
    /// uso anidado se una a la transaccion del llamador en vez de intentar abrir otra.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Registra trabajo que solo puede ejecutarse cuando los ids de identidad YA existen.
    /// El Id de toda entidad es BIGINT de identidad generado por la base: antes de SaveChanges
    /// vale 0, y EF lo asigna DURANTE SaveChanges (un interceptor SavingChanges todavia veria 0).
    ///
    /// Las acciones registradas se ejecutan al final del SaveChanges en curso, cuando los ids
    /// reales ya estan materializados, y lo que produzcan se persiste en un segundo guardado
    /// DENTRO de la misma transaccion: si el llamador ya abrio una, se usa esa; si no, el
    /// contexto abre una propia para que ambos guardados sean atomicos.
    ///
    /// Uso tipico: escribir la pista de auditoria de un alta (ver IAuditWriter), donde el
    /// EntityId del asiento no se conoce hasta despues del INSERT.
    /// </summary>
    void DeferUntilIdsAssigned(Action work);
}
