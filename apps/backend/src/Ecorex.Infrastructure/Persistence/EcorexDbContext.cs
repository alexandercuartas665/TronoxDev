using System.Reflection;
using Ecorex.Application.Common;
using Ecorex.Domain.Common;
using Ecorex.Domain.Entities;
using Ecorex.Domain.Enums;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ecorex.Infrastructure.Persistence;

public class EcorexDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    private readonly ITenantContext _tenantContext;

    public EcorexDbContext(DbContextOptions<EcorexDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Constructor para contextos derivados por proveedor (p.ej. SqlServerEcorexDbContext),
    /// que existen unicamente para separar las migraciones por motor (ADR-001).
    /// </summary>
    protected EcorexDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Globales (administradas por Super Admin / plataforma)
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SaasPlan> SaasPlans => Set<SaasPlan>();
    public DbSet<SaasPlanLimit> SaasPlanLimits => Set<SaasPlanLimit>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantPayment> TenantPayments => Set<TenantPayment>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<PlatformBranding> PlatformBrandings => Set<PlatformBranding>();
    public DbSet<EmailConfig> EmailConfigs => Set<EmailConfig>();
    public DbSet<GoogleAuthConfig> GoogleAuthConfigs => Set<GoogleAuthConfig>();
    public DbSet<TenantApiConfig> TenantApiConfigs => Set<TenantApiConfig>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AccountActivationCode> AccountActivationCodes => Set<AccountActivationCode>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<SuperAdminAuditLog> SuperAdminAuditLogs => Set<SuperAdminAuditLog>();

    // Llaves de Data Protection compartidas entre apps (Api, SuperAdmin, Workers) para
    // que los secretos cifrados (Wompi, Evolution) se descifren en cualquiera de ellas.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Tenant-scoped (con filtro global de consulta)
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<TenantConfiguration> TenantConfigurations => Set<TenantConfiguration>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    // Extraccion de datos / web scraping acotado (modulo 000730, ADR-0025).
    // Flujos de extraccion por navegador (modulo 000730, capitulo "Extraccion de Datos").
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    // Modulo Tableros (Kanban de tareas/proyectos por agencia).

    // Nucleo de tareas/proyectos (FASE 3, ADR-0013): TaskItem de primera clase con
    // consecutivo por tenant, estados propios, proyectos con ACL y worklogs.
    public DbSet<Notification> Notifications => Set<Notification>();

    // Tableros de actividades unificados (ADR-0020): checklist y asignados M:N del TaskItem.
    public DbSet<TenantSequence> TenantSequences => Set<TenantSequence>();

    // Motor de flujos BPMN (FASE 4, ADR-0014): definiciones versionadas, grafo materializado
    // (nodos + aristas), instancias por caso y historial de pasos append-only.

    // Formularios dinamicos (FASE 4, ADR-0015): definiciones con arbol contenedores ->
    // preguntas, respuestas como documento JSON, tokens de publicacion por URL y vinculos
    // formulario <-> paso de flujo.

    // Motor de reglas (FASE 4 ola 3, ADR-0016): documentos de reglas, reglas con verbo
    // tipado, historial append-only con TTL y vinculos a preguntas de formulario y nodos
    // de flujo.

    // Modulos de sistema (FASE 5, ADR-0017): organigrama del tenant (Dependencias, legacy
    // 000850) y registro de modulos (Modulos web, legacy 000109). ModuleDefinition es el
    // catalogo GLOBAL de plataforma (sin TenantId ni query filter); TenantModule es el
    // estado por tenant (scoped).
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<OrgUnitMember> OrgUnitMembers => Set<OrgUnitMember>();
    public DbSet<ModuleDefinition> ModuleDefinitions => Set<ModuleDefinition>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();

    // Inventarios (grupo Sistema - Inventarios): catalogos normalizados (bodegas, marcas,
    // grupos, subgrupos, tipos) + items con imagenes por URL y existencias por bodega.

    // Configuracion de la entidad (agencias/areas/sucursales del tenant + campos dinamicos).

    // Contenedor de datos (un DataModel agrupa varias tablas EAV + config de importacion).

    // Plantillas HSM de WhatsApp (ADR-0029): mensajes plantilla con ciclo de aprobacion.

    // Menu configurable por perfil (Ola 1): vistas del menu por tenant y sus nodos (arbol).
    public DbSet<MenuView> MenuViews => Set<MenuView>();
    public DbSet<MenuNode> MenuNodes => Set<MenuNode>();

    // Roles de permisos dinamicos (Ola B1, ADR-0032): matriz Modulo x Accion por tenant.
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();

    // Directorio General (modulo 000232): terceros (empresas / personas) con perfiles de
    // negocio, contactos embebidos y fichas dinamicas (jsonb). Multi-tenant (filtro global).
    // ---- Gestor de Clientes (000740) ----

    // Conceptos de actividades (modulo 000270): catalogo de dos niveles Categoria ->
    // Subcategoria (concepto). Multi-tenant (filtro global por reflexion).

    // Motor de programaciones (modulo 000889 "Programar actividad"): "cron de negocio" gobernado.
    // Cabecera + reglas de recurrencia 1:N + canales N + bitacora de ejecucion (KPIs/idempotencia).

    /// <summary>
    /// Transaccion explicita para casos de uso multi-paso (IApplicationDbContext).
    /// </summary>
    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    /// <summary>Hay una transaccion abierta (los casos de uso anidados se unen a ella).</summary>
    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Todos los enums se persisten como texto (legibles y estables ante reordenamientos).
        configurationBuilder.Properties<TenantStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TenantKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<SubscriptionStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<BillingFrequency>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PlatformRole>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<LimitEnforcementMode>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AuditActorType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TenantRole>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PlatformUserStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<LeadVisibility>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WhatsAppLineStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<LeadStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FollowUpTaskStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MessageDirection>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MessageMediaType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AiProvider>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AgentResourceType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AutomationTrigger>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AutomationAction>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WompiEnvironment>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WompiIntegrationStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<EvolutionIntegrationStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WebhookProcessingStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PipelineFieldType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<BusinessUnitModalKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TaskActivityType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<NotificationKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<DofaQuadrant>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OportunidadEtapa>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<OportunidadEstadoTipo>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<CitaTipo>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AiAgentRunLogKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WhatsAppProvider>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ProjectStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TaskPriority>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TaskItemStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WorkLogKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WorkflowNodeType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WorkflowInstanceStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WorkflowStepStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormContainerType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormControlType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormResponseStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormFlowLinkStatus>().HaveConversion<string>().HaveMaxLength(40);
        // Origen de datos / lookup (ola F1): enums persistidos como string para DAL dual.
        configurationBuilder.Properties<FormSourceKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormFieldPresentation>().HaveConversion<string>().HaveMaxLength(40);
        // Calculo / agregacion (ola F2).
        configurationBuilder.Properties<FormAggregate>().HaveConversion<string>().HaveMaxLength(40);
        // Transaccionalidad (ola F3).
        configurationBuilder.Properties<FormIdentityMode>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<FormRecordStatus>().HaveConversion<string>().HaveMaxLength(40);
        // Transversales (ola F6).
        configurationBuilder.Properties<FormDefaultDynamic>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<RuleStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<RuleTriggerKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<RuleExecutionStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<OrgUnitKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<OrgUnitClassifier>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ModuleArea>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TaskBoardStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TaskBoardKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScrapeSourceKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScrapeSourceStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScrapeRunStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScrapeStepKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScrapeWarningAction>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WhatsAppTemplateCategory>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WhatsAppTemplateHeaderType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<WhatsAppTemplateStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MenuNodeKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MenuNodeState>().HaveConversion<string>().HaveMaxLength(40);
        // Directorio General (000232): Tipo/Estado/IdTipo como texto legible. TerceroPerfil NO
        // se convierte a texto: es [Flags] y se guarda como int para filtrar por bits en SQL.
        configurationBuilder.Properties<TerceroTipo>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TerceroEstado>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TerceroIdTipo>().HaveConversion<string>().HaveMaxLength(40);
        // Campos configurables por ficha (000232): el tipo del campo se guarda como texto legible.
        configurationBuilder.Properties<TerceroFieldType>().HaveConversion<string>().HaveMaxLength(40);
        // Configuracion de la entidad (000615): naturaleza de la entidad (Sede/Area) como texto.
        configurationBuilder.Properties<EntidadKind>().HaveConversion<string>().HaveMaxLength(20);
        // Contenedor de datos: enums como texto.
        configurationBuilder.Properties<DataContainerColumnType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<DataModelRelationKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<DataSourceKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ConnectorAuthKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ImportScheduleKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ImportRunTrigger>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ImportRunResult>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ConnectorKind>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<DbEngine>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<DestinationKind>().HaveConversion<string>().HaveMaxLength(40);
        // Motor de programaciones (000889 "Programar actividad"): enums como texto legible.
        configurationBuilder.Properties<ScheduledJobType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScheduledJobStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScheduledJobPriority>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScheduledJobFrequency>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScheduledJobChannelType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ScheduledJobRunResult>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DAL dual (ADR-001): el modelo es neutro salvo los puntos marcados con este flag,
        // donde cada proveedor (PostgreSQL / SQL Server) recibe su tipo o sintaxis equivalente.
        var isNpgsql = Database.IsNpgsql();

        ConfigureEntities(modelBuilder, isNpgsql);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ConfigureEntities(ModelBuilder modelBuilder, bool isNpgsql)
    {
        // jsonb existe solo en PostgreSQL; en SQL Server el equivalente practico es nvarchar(max).
        var jsonColumnType = isNpgsql ? "jsonb" : "nvarchar(max)";
        // "text" en SQL Server esta deprecado y no soporta operadores de comparacion (=);
        // el equivalente correcto es nvarchar(max).
        var longTextColumnType = isNpgsql ? "text" : "nvarchar(max)";
        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.LegalName).HasMaxLength(250);
            b.Property(x => x.TaxId).HasMaxLength(80);
            b.Property(x => x.Country).HasMaxLength(80);
            // Zona horaria IANA del tenant (regla 9): la usa el motor de programaciones (000889).
            b.Property(x => x.TimeZoneId).HasMaxLength(60);
            b.Property(x => x.Currency).HasMaxLength(10);
            b.Property(x => x.City).HasMaxLength(120);
            b.Property(x => x.Address).HasMaxLength(300);
            b.Property(x => x.Phone).HasMaxLength(80);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
            b.Property(x => x.PublicBookingToken).HasMaxLength(64);
            b.Property(x => x.PublicBookingBaseUrl).HasMaxLength(300);
            b.HasIndex(x => x.PublicBookingToken).IsUnique()
                .HasFilter(isNpgsql ? "public_booking_token IS NOT NULL" : "[public_booking_token] IS NOT NULL");
        });

        modelBuilder.Entity<SaasPlan>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Currency).HasMaxLength(10);
            b.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
            b.Property(x => x.YearlyPrice).HasPrecision(12, 2);
            b.HasMany(x => x.Limits)
                .WithOne(x => x.Plan!)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaasPlanLimit>(b =>
        {
            b.Property(x => x.LimitKey).HasMaxLength(150).IsRequired();
            b.Property(x => x.LimitUnit).HasMaxLength(50);
            b.HasIndex(x => new { x.PlanId, x.LimitKey }).IsUnique();
        });

        modelBuilder.Entity<TenantSubscription>(b =>
        {
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PaymentMethodLabel).HasMaxLength(80);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<TenantPayment>(b =>
        {
            b.Property(x => x.Amount).HasPrecision(12, 2);
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            b.Property(x => x.ProviderReference).HasMaxLength(200);
            b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<PlatformUser>(b =>
        {
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200);
            b.Property(x => x.GoogleSubject).HasMaxLength(255);
            b.Property(x => x.AuthProvider).HasMaxLength(50).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => x.GoogleSubject).IsUnique()
                .HasFilter(isNpgsql ? "google_subject IS NOT NULL" : "[google_subject] IS NOT NULL");
        });

        modelBuilder.Entity<SuperAdminAuditLog>(b =>
        {
            b.Property(x => x.ActionName).HasMaxLength(200).IsRequired();
            b.Property(x => x.EntityName).HasMaxLength(150).IsRequired();
            b.Property(x => x.IpAddress).HasMaxLength(80);
            b.Property(x => x.PreviousValue).HasColumnType(jsonColumnType);
            b.Property(x => x.NewValue).HasColumnType(jsonColumnType);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<PlatformBranding>(b =>
        {
            b.Property(x => x.PlatformName).HasMaxLength(120).IsRequired();
            b.Property(x => x.Tagline).HasMaxLength(160);
            b.Property(x => x.LoginLogoUrl).HasMaxLength(500);
            b.Property(x => x.LoginHeadline).HasMaxLength(160);
            b.Property(x => x.LoginSubtext).HasMaxLength(600);
        });

        modelBuilder.Entity<EmailConfig>(b =>
        {
            b.Property(x => x.SmtpHost).HasMaxLength(200);
            b.Property(x => x.SmtpUser).HasMaxLength(200);
            b.Property(x => x.FromEmail).HasMaxLength(200);
            b.Property(x => x.FromName).HasMaxLength(160);
        });

        modelBuilder.Entity<PasswordResetToken>(b =>
        {
            b.Property(x => x.TokenHash).HasMaxLength(80).IsRequired();
            b.HasIndex(x => x.TokenHash);
            b.HasIndex(x => x.PlatformUserId);
        });

        modelBuilder.Entity<AccountActivationCode>(b =>
        {
            b.Property(x => x.CodeHash).HasMaxLength(80).IsRequired();
            b.HasIndex(x => x.PlatformUserId);
        });

        modelBuilder.Entity<GoogleAuthConfig>(b =>
        {
            b.Property(x => x.ClientId).HasMaxLength(300);
        });

        modelBuilder.Entity<TenantApiConfig>(b =>
        {
            b.Property(x => x.ApiKeyHash).HasMaxLength(80);
            b.HasIndex(x => x.ApiKeyHash);
            b.HasIndex(x => x.TenantId).IsUnique();
        });



        modelBuilder.Entity<AiProviderConfig>(b =>
        {
            b.Property(x => x.Model).HasMaxLength(120);
            b.Property(x => x.BaseUrl).HasMaxLength(500);
            b.HasIndex(x => x.Provider).IsUnique();
        });


        modelBuilder.Entity<TenantUser>(b =>
        {
            b.Property(x => x.Email).HasMaxLength(256).IsRequired();
            b.Property(x => x.InvitationToken).HasMaxLength(128);
            b.Property(x => x.DocumentCode).HasMaxLength(60);
            b.Property(x => x.Phone).HasMaxLength(40);
            b.HasOne(x => x.PlatformUser).WithMany().HasForeignKey(x => x.PlatformUserId).OnDelete(DeleteBehavior.Restrict);
            // Asignacion usuario->vista del menu (Ola 1). NO ACTION: borrar una vista no arrastra
            // al usuario por cascada (la app deja MenuViewId en null antes de borrar la vista).
            b.HasOne(x => x.MenuView).WithMany().HasForeignKey(x => x.MenuViewId).OnDelete(DeleteBehavior.Restrict);
            // Rol de permisos (Ola B1). NO ACTION: borrar un rol no arrastra al usuario por cascada
            // (la app bloquea el borrado de un rol con usuarios asignados). Ver ADR-0032.
            b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PlatformUserId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            b.HasIndex(x => x.InvitationToken);
            b.HasIndex(x => x.MenuViewId);
            b.HasIndex(x => x.RolId);
        });

        modelBuilder.Entity<TenantConfiguration>(b =>
        {
            b.Property(x => x.ConfigKey).HasMaxLength(150).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.ConfigKey }).IsUnique();
        });



        // Consola SQL admin (000077): auditoria append-only, NO tenant-scoped (evento cross-tenant
        // que el Owner/Admin puede ejecutar; el tenant del actor se guarda como dato, no como filtro).


        modelBuilder.Entity<BusinessUnit>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Color).HasMaxLength(20).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.SortOrder });
        });







        // Extraccion de datos / web scraping acotado (modulo 000730, ADR-0025).


        // Flujos de extraccion por navegador (modulo 000730, capitulo "Extraccion de Datos", Ola 1).



















        modelBuilder.Entity<AiUsageLog>(b =>
        {
            b.Property(x => x.Model).HasMaxLength(120);
            b.Property(x => x.Source).HasMaxLength(40);
            b.Property(x => x.EstimatedCostUsd).HasPrecision(12, 6);
            b.HasIndex(x => new { x.TenantId, x.AgentId });
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });


        // ---- Modulo Tableros (Kanban) ----










        // ---- Nucleo de tareas/proyectos (FASE 3, ADR-0013) ----




        // Proyectos P1: hitos del proyecto (legacy PROYECTOS_HITO). Cascada al borrar el proyecto.

        // Proyectos P2: presupuesto/costos (legacy PROYECTOS_PRESUPUESTO + PROYECTOS_COS). Una sola ruta
        // de cascada (Project->BudgetItem), valida en ambos motores.

        // Proyectos P2: analisis DOFA/SWOT (legacy PROYECTOS_DOFA).








        // Ola 7 (endurecimiento): bandeja de notificaciones in-app por usuario (entrega real).
        modelBuilder.Entity<Notification>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Body).HasColumnType(longTextColumnType).IsRequired();
            b.Property(x => x.LinkRoute).HasMaxLength(300);
            b.Property(x => x.ActorName).HasMaxLength(200);
            b.HasOne(x => x.RecipientTenantUser).WithMany()
                .HasForeignKey(x => x.RecipientTenantUserId).OnDelete(DeleteBehavior.Cascade);
            // Consulta caliente: no leidas del usuario, mas recientes primero.
            b.HasIndex(x => new { x.RecipientTenantUserId, x.IsRead, x.CreatedAt });
        });


        modelBuilder.Entity<TenantSequence>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(10).IsRequired();
            // Un consecutivo por (tenant, codigo). SequenceService lo incrementa con
            // UPDATE condicional atomico (CAS con retry), sin SQL crudo (ADR-0013).
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        // ---- Motor de flujos BPMN (FASE 4, ADR-0014) ----






        // ---- Formularios dinamicos (FASE 4, ADR-0015) ----






        // Maestro-detalle entre formularios (ola F5, doc 01 D7).

        // Motor de programaciones (000889): cabecera + reglas 1:N + canales N + bitacora.






        // ---- Motor de reglas (FASE 4 ola 3, ADR-0016) ----






        // ---- Modulos de sistema (FASE 5, ADR-0017) ----

        modelBuilder.Entity<OrgUnit>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(600);
            // Self-FK del arbol con NO ACTION: una unidad con hijos no se borra en cascada
            // (y las unidades nunca se borran fisicamente: se archivan).
            b.HasOne(x => x.Parent).WithMany()
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            // Clasificador de asignacion por nodo (ADR-0035): default Dependencia para las
            // filas heredadas; TenantUserId solo se usa cuando Classifier=Funcionario.
            b.Property(x => x.Classifier).HasDefaultValue(OrgUnitClassifier.Dependencia);
            b.HasIndex(x => new { x.TenantId, x.ParentId });
            b.HasIndex(x => new { x.TenantId, x.IsArchived });
            b.HasIndex(x => x.ResponsibleTenantUserId);
            b.HasIndex(x => x.TenantUserId);
        });

        modelBuilder.Entity<OrgUnitMember>(b =>
        {
            b.Property(x => x.Role).HasMaxLength(100);
            // El miembro vive y muere con su unidad.
            b.HasOne(x => x.OrgUnit).WithMany()
                .HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OrgUnitId, x.TenantUserId }).IsUnique();
            b.HasIndex(x => x.TenantUserId);
        });

        // Asignacion por nodo (ADR-0035, ola F1): que Dependencia/Cargo atiende un paso Task.

        modelBuilder.Entity<ModuleDefinition>(b =>
        {
            // Catalogo GLOBAL de plataforma (ADR-0017): sin TenantId y sin query filter.
            b.Property(x => x.LegacyCode).HasMaxLength(6).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(600);
            b.Property(x => x.Route).HasMaxLength(200);
            b.HasIndex(x => x.LegacyCode).IsUnique();
        });

        modelBuilder.Entity<TenantModule>(b =>
        {
            // Settings del tenant como documento JSON: jsonb en PG, nvarchar(max) en SQL Server.
            b.Property(x => x.SettingsJson).HasColumnType(jsonColumnType);
            // Restrict: el catalogo global no arrastra estados de tenants al borrarse
            // (una definicion con estados por tenant no se elimina).
            b.HasOne(x => x.ModuleDefinition).WithMany()
                .HasForeignKey(x => x.ModuleDefinitionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.ModuleDefinitionId }).IsUnique();
        });

        // ---- Directorio General (modulo 000232) ----
        // Terceros (empresas / personas) con perfiles de negocio ([Flags] como int), contactos
        // embebidos y fichas dinamicas por perfil (jsonb en PG / nvarchar(max) en SQL Server).



        // Campos configurables por ficha (fiscal/comercial/cliente/proveedor/empleado): datos que
        // vuelven las fichas del tercero personalizables por tenant. FieldType como texto.

        // Formularios ofrecidos en el modal de tercero (config por tenant desde "Configurar campos").
        // Solo la CONFIG: las respuestas son FormResponse ancladas por Reference = "TERCERO:{id}".
        // Restrict: quitar un formulario del modal es una accion explicita, no un efecto de borrar
        // la definicion del formulario.

        // Notas / gestiones "Contacto cliente" del tercero. Viven y mueren con el tercero (cascade).

        // ---- Conceptos de actividades (modulo 000270) ----
        // Jerarquia de dos niveles Categoria -> Subcategoria (concepto). Los vinculos a otros
        // modulos (flujo/formulario/tablero/columna) y a cargos/terceros son NO ACTION (Restrict)
        // para evitar rutas multiples de cascada en SQL Server; las subcategorias y sus relaciones
        // hijas mueren con la categoria (Cascade). Codigos unicos por (TenantId, Codigo).







        // ---- Gestor de Clientes (000740): bolsa, oportunidades, citas, filtros, prospectos ----


        // El tercero apunta a su columna de bolsa (NO ACTION: borrar la columna no borra terceros).






        // ---- Inventarios (grupo Sistema - Inventarios) ----
        // Catalogos normalizados: nombre unico por tenant; FKs de catalogo NO ACTION (Restrict)
        // para evitar rutas multiples de cascada en SQL Server. Los items no se borran
        // fisicamente (IsActive); las imagenes y el stock viven y mueren con el item (cascade).








        // Campos configurables del item POR tipo (000066). Calcado de TerceroFieldDefinition,
        // agrupando por ItemType en vez de por ficha. FK al tipo NO ACTION (Restrict): borrar el
        // catalogo de tipo no arrastra sus definiciones por cascada. FieldKey unico por (tenant, tipo).

        // Configuracion de la entidad (000615). Entidad = agencia/area/sucursal del tenant; varias
        // por tenant, con identidad legal + ubicacion + config + logo + campos dinamicos. Codigo
        // unico por tenant; los valores de campos dinamicos van en FieldValuesJson (jsonb/nvarchar).

        // Campos dinamicos de la entidad, a nivel de tenant (aplican a todas las entidades).


        // ---- Contenedor de datos (un DataModel agrupa varias tablas EAV + config de importacion) ----
        // Cascadas recursivas PG-friendly: borrar un contenedor arrastra su arbol completo (tablas,
        // sub-tablas, columnas, filas, celdas, conectores, destino y procesos). Nota DAL-dual: las
        // cascadas auto-referenciales y multi-ruta son validas en PostgreSQL; el proveedor SQL
        // Server (backlog) requerira revisarlas (se documenta como deuda, igual que el resto del DAL dual).

        // Relacion inter-tabla (arista del ER): entidad propia, ortogonal al tipo de columna.

        // Vinculo dato-a-dato de una relacion (FASE 2 del rediseno de relaciones).












        // Menu configurable por perfil (Ola 1): vistas del menu por tenant + arbol de nodos.
        modelBuilder.Entity<MenuView>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(600);
            // Nombre unico por tenant.
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.IsDefault });
        });

        modelBuilder.Entity<MenuNode>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.IconKey).HasMaxLength(60);
            b.Property(x => x.LegacyCode).HasMaxLength(20);
            b.Property(x => x.Route).HasMaxLength(300);
            b.Property(x => x.Description).HasMaxLength(600);
            b.Property(x => x.HelpText).HasColumnType(longTextColumnType);
            // La vista y sus nodos viven y mueren juntos.
            b.HasOne(x => x.MenuView).WithMany()
                .HasForeignKey(x => x.MenuViewId).OnDelete(DeleteBehavior.Cascade);
            // Self-ref NO ACTION: evita rutas de cascada multiples en SQL Server (borrado de la
            // rama se maneja por la app, no por cascada del padre).
            b.HasOne(x => x.Parent).WithMany()
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.MenuViewId });
            b.HasIndex(x => new { x.MenuViewId, x.ParentId, x.SortOrder });
        });

        // Roles de permisos dinamicos (Ola B1, ADR-0032): rol por tenant + filas de permiso por modulo.
        modelBuilder.Entity<Rol>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Description).HasMaxLength(600);
            // Nombre unico por tenant.
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.IsActive });
        });

        modelBuilder.Entity<RolPermiso>(b =>
        {
            b.Property(x => x.ModuleKey).HasMaxLength(300).IsRequired();
            // El permiso vive y muere con su rol.
            b.HasOne(x => x.Rol).WithMany()
                .HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
            // Una fila por (rol, modulo).
            b.HasIndex(x => new { x.RolId, x.ModuleKey }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.RolId });
        });
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var applyMethod = typeof(EcorexDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                applyMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        // Fail-closed: si no hay tenant activo, TenantId del contexto es null y no devuelve filas.
    }
}
