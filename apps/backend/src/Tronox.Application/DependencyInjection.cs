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
        // Datos de la Entidad (RF01 4.1.1): UNA fila por tenant. La logica pura (digito
        // verificador del NIT, generacion del codigo de fondo AGN, obligatoriedad condicional
        // si la entidad es Publica) vive en Archivistica.EntidadRules: estatica, sin EF y
        // testeable sin base de datos, por eso no se registra.
        services.AddScoped<Archivistica.IEntidadService, Archivistica.EntidadService>();
        // Catalogos territoriales DIVIPOLA (pendiente P-02 de RQ01): solo lectura y GLOBALES.
        services.AddScoped<Archivistica.IDivipolaService, Archivistica.DivipolaService>();
        services.AddScoped<Archivistica.ISedeService, Archivistica.SedeService>();
        services.AddScoped<Archivistica.IFondoService, Archivistica.FondoService>();
        services.AddScoped<Archivistica.ISubfondoService, Archivistica.SubfondoService>();

        // --- Estructura organizacional: UN SOLO arbol con clasificador (RF03/RF04, ADR-003) ---
        // La logica de arbol (ciclos, resolver de dependencia, subarbol afectado) es PURA y
        // estatica en Organization.OrgUnitTree / OrgStructureRules: no se registra ni se
        // inyecta, se testea sin base de datos y se puede cachear por tenant.
        services.AddScoped<Organization.IOrgUnitService, Organization.OrgUnitService>();

        // --- Configuracion documental (base de RQ02) ---
        // Versiones de la TRD (RF01): marco legal, una sola Vigente por tenant. Y catalogo de
        // series y subseries (RF02). Las validaciones y maquinas de estado/arbol son PURAS y
        // estaticas (TrdVersionRules / SerieRules): se testean sin base de datos, no se registran.
        services.AddScoped<TrdVersiones.ITrdVersionService, TrdVersiones.TrdVersionService>();
        services.AddScoped<SeriesDocumentales.ISerieDocumentalService, SeriesDocumentales.SerieDocumentalService>();
        services.AddScoped<Listas.IListaMaestraService, Listas.ListaMaestraService>();
        // Construccion de la TRD (RF04): cruce Dependencia x Serie con tiempos, disposicion,
        // clasificacion y metadatos. La logica pura (CCD, permisos por estado) vive en
        // Trd.TrdConstruccionRules; se testea sin base de datos y no se registra.
        services.AddScoped<Trd.ITrdConstruccionService, Trd.TrdConstruccionService>();
        // Topografia fisica (RF06): jerarquia de niveles + arbol de elementos con codigo topografico.
        // La logica pura (codigo, capacidad, ciclos) vive en Topografia.TopografiaRules.
        services.AddScoped<Topografia.ITopografiaService, Topografia.TopografiaService>();
        // Gestion integral de expedientes (RQ03): bandeja, creacion con codigo estructurado, herencia
        // de clasificacion (RF10, fail-closed) y metadatos EAV. La logica pura vive en ExpedienteRules.
        services.AddScoped<Expedientes.IExpedienteService, Expedientes.ExpedienteService>();
        // Gestion integral de documentos (RQ04): Mis Borradores (binario en object storage, ADR-009),
        // archivar en expediente (RF16). La logica pura (extension/tamano/hash) vive en DocumentoRules.
        services.AddScoped<Documentos.IDocumentoService, Documentos.DocumentoService>();
        // Tareas de validacion (RQ04 - RF11/RF12): solicitar revision/aprobacion + bandeja Mis Tareas.
        // La logica pura (comentario obligatorio, dias restantes) vive en ValidacionRules.
        services.AddScoped<Validaciones.IValidacionService, Validaciones.ValidacionService>();
        // Plantillas documentales (RQ04 - RF09): CRUD + asociacion N:N a tipologias + variables.
        // La logica pura (conteo de variables, catalogo base) vive en PlantillaRules.
        services.AddScoped<Plantillas.IPlantillaService, Plantillas.PlantillaService>();
        // Motor de formularios dinamicos (RQ08, port ECOREX): definicion + arbol (constructor) y
        // respuestas (captura + validacion servidor). La validacion pura vive en FormFieldValidator.
        services.AddScoped<Forms.IFormDefinitionService, Forms.FormDefinitionService>();
        services.AddScoped<Forms.IFormResponseService, Forms.FormResponseService>();
        // Formularios avanzados (fidelidad): condiciones autocontenidas, tokens de publicacion y
        // framework de lookups (sin fuentes registradas aun: Tercero -> RQ07).
        services.AddScoped<Forms.IFormConditionService, Forms.FormConditionService>();
        services.AddScoped<Forms.IFormTokenService, Forms.FormTokenService>();
        services.AddScoped<Forms.Lookups.IFormLookupService, Forms.Lookups.FormLookupService>();

        // --- Motor de flujos BPMN (RQ11, port del motor de ECOREX) ---
        // El hook de reglas es NoOp (motor de Reglas podado): todos los pasos Task son humanos.
        services.AddScoped<Workflows.IWorkflowRuleHook, Workflows.NoOpWorkflowRuleHook>();
        services.AddScoped<Workflows.IWorkflowEngine, Workflows.WorkflowEngine>();
        services.AddScoped<Workflows.IWorkflowDesignService, Workflows.WorkflowDesignService>();
        services.AddScoped<Workflows.INodeAssigneeResolver, Workflows.NodeAssigneeResolver>();
        services.AddScoped<Workflows.IWorkflowNodePolicyService, Workflows.WorkflowNodePolicyService>();

        // --- Configuracion de Radicacion (RQ09 RF01): consecutivos + SLA (singleton), catalogo de
        // tipos de comunicacion (13 base sembrados al crear la config), buzones de correo (clave
        // cifrada AES-256), notificaciones por evento y bitacora de migracion historica. ---
        services.AddScoped<Radicacion.IRadicacionConfigService, Radicacion.RadicacionConfigService>();
        services.AddScoped<Radicacion.ITipoComunicacionService, Radicacion.TipoComunicacionService>();
        services.AddScoped<Radicacion.IBuzonCorreoService, Radicacion.BuzonCorreoService>();
        // Panel de Control operativo (RQ09 RF12-1): dashboard de radicacion (solo lectura).
        services.AddScoped<Radicacion.IRadicacionPanelService, Radicacion.RadicacionPanelService>();
        // Bandeja + distribucion + detalle (RQ09 RF07/RF11), port de rad_bandeja/rad_tramites/rad_detalle.
        services.AddScoped<Radicacion.RadicacionVisibilidadService>();
        services.AddScoped<Radicacion.IRadicacionBandejaService, Radicacion.RadicacionBandejaService>();
        services.AddScoped<Radicacion.IRadicacionDistribucionService, Radicacion.RadicacionDistribucionService>();
        services.AddScoped<Radicacion.IRadicadoDetalleService, Radicacion.RadicadoDetalleService>();
        // Fundaciones compartidas: calendario habil (festivos) + orquestador de radicacion (consecutivo+SLA).
        services.AddScoped<Radicacion.ICalendarioHabilService, Radicacion.CalendarioHabilService>();
        services.AddScoped<Radicacion.IRadicadorService, Radicacion.RadicadorService>();
        // Correos por Revisar (RQ09 RF04), port de rad_correos.
        services.AddScoped<Radicacion.IRadicacionCorreosService, Radicacion.RadicacionCorreosService>();
        // Configuracion PQR (RQ09 RF01): Prioridades + Portal Web (port de rad_config).
        services.AddScoped<Radicacion.IConfiguracionPqrService, Radicacion.ConfiguracionPqrService>();

        // --- Registro de modulos por tenant ---
        services.AddScoped<Modules.IModuleRegistryService, Modules.ModuleRegistryService>();

        // --- Configuracion multi-proveedor de IA y medicion de consumo (base de RQ16) ---
        services.AddScoped<IAiServerConfigService, AiServerConfigService>();
        services.AddScoped<Tenancy.IAiUsageService, Tenancy.AiUsageService>();

        return services;
    }
}
