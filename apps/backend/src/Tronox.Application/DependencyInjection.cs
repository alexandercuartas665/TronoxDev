using Tronox.Application.Admin;
using Tronox.Application.Auth;
using Tronox.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Tronox.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // --- Identidad, autenticacion y auditoria ---
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<ISelfSignupService, SelfSignupService>();
        services.AddScoped<IAccountActivationService, AccountActivationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IGoogleSignInService, GoogleSignInService>();
        services.AddScoped<IAuditAdminService, AuditAdminService>();

        // --- Plataforma / Console (base de RQ14): tenants, planes, suscripciones, operadores ---
        services.AddScoped<ITenantAdminService, TenantAdminService>();
        services.AddScoped<IPlanAdminService, PlanAdminService>();
        services.AddScoped<ISubscriptionAdminService, SubscriptionAdminService>();
        services.AddScoped<IPaymentAdminService, PaymentAdminService>();
        services.AddScoped<IPaymentReceiptService, PaymentReceiptService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IPlatformOperatorService, PlatformOperatorService>();
        services.AddScoped<IPlatformBrandingService, PlatformBrandingService>();

        // --- Configuracion del tenant (base de RQ01): correo saliente y acceso Google ---
        services.AddScoped<IEmailConfigService, EmailConfigService>();
        services.AddScoped<IGoogleAuthConfigService, GoogleAuthConfigService>();

        // --- Usuarios y consecutivos del tenant ---
        services.AddScoped<Tenancy.ITenantUserService, Tenancy.TenantUserService>();
        services.AddScoped<Tenancy.IBusinessUnitService, Tenancy.BusinessUnitService>();
        // Emision de consecutivos con bloqueo pesimista. Es la pieza sobre la que RQ09
        // construira el consecutivo de radicacion con scope (tenant, tipo, anio).
        services.AddScoped<Tenancy.ISequenceService, Tenancy.SequenceService>();

        // --- Notificaciones in-app ---
        services.AddScoped<Notifications.INotificationService, Notifications.NotificationService>();

        // --- Menu configurable por tenant (base de RF09, ver ADR-001) ---
        services.AddScoped<MenuConfig.IMenuConfigService, MenuConfig.MenuConfigService>();

        // --- Roles y matriz de permisos Modulo x Accion (base de RF05) ---
        // El catalogo de modulos se deriva del menu, no de una lista paralela.
        services.AddScoped<Roles.IRolService, Roles.RolService>();

        // --- Configuracion archivistica (base de RF01-P.3 y RF02) ---
        // Niveles de clasificacion documental (los 4 canonicos los siembra el alta del tenant,
        // ver IClasificacionProvisioningService en Infrastructure), sedes, fondos y subfondos.
        services.AddScoped<Archivistica.INivelClasificacionService, Archivistica.NivelClasificacionService>();
        services.AddScoped<Archivistica.ISedeService, Archivistica.SedeService>();
        services.AddScoped<Archivistica.IFondoService, Archivistica.FondoService>();
        services.AddScoped<Archivistica.ISubfondoService, Archivistica.SubfondoService>();

        // --- Estructura organizacional: UN SOLO arbol con clasificador (RF03/RF04, ADR-003) ---
        // La logica de arbol (ciclos, resolver de dependencia, subarbol afectado) es PURA y
        // estatica en Organization.OrgUnitTree / OrgStructureRules: no se registra ni se
        // inyecta, se testea sin base de datos y se puede cachear por tenant.
        services.AddScoped<Organization.IOrgUnitService, Organization.OrgUnitService>();

        // --- Registro de modulos por tenant ---
        services.AddScoped<Modules.IModuleRegistryService, Modules.ModuleRegistryService>();

        // --- Configuracion multi-proveedor de IA y medicion de consumo (base de RQ16) ---
        services.AddScoped<IAiServerConfigService, AiServerConfigService>();
        services.AddScoped<Tenancy.IAiUsageService, Tenancy.AiUsageService>();

        return services;
    }
}
