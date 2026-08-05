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

    // Roles y matriz de permisos Modulo x Accion (base de RQ01 - RF05). RolPermisos lleva UNA
    // FILA POR (modulo, accion); UsuariosRoles es la puente multi-rol con vigencia temporal.
    DbSet<Rol> Roles { get; }
    DbSet<RolPermiso> RolPermisos { get; }
    DbSet<UsuarioRol> UsuariosRoles { get; }

    // Configuracion archivistica (base de RQ01 - RF01-P.3 y RF02): niveles de clasificacion
    // documental, sedes, fondos y subfondos. Todas tenant-scoped.
    DbSet<NivelClasificacion> NivelesClasificacion { get; }
    DbSet<Sede> Sedes { get; }

    // Datos de la Entidad (base de RQ01 - RF01 4.1.1). UNA fila por tenant.
    DbSet<Entidad> Entidades { get; }
    DbSet<Fondo> Fondos { get; }
    DbSet<Subfondo> Subfondos { get; }

    // Catalogos territoriales DIVIPOLA (pendiente P-02 de RQ01). Son GLOBALES de plataforma:
    // no llevan tenant_id y no reciben query filter, igual que ModuleDefinition.
    DbSet<Pais> Paises { get; }
    DbSet<Departamento> Departamentos { get; }
    DbSet<Municipio> Municipios { get; }

    // Estructura organizacional (base de RQ01 - RF03/RF04).
    DbSet<OrgUnit> OrgUnits { get; }
    DbSet<OrgUnitMember> OrgUnitMembers { get; }
    DbSet<BusinessUnit> BusinessUnits { get; }

    // Configuracion documental (base de RQ02). Versiones de la TRD (RF01): marco legal, una sola
    // Vigente por tenant. Catalogo de series y subseries (RF02): arbol tenant-scoped. Administrador
    // de listas (RF03): maestro-detalle Lista -> Opciones que alimenta metadatos de tipo Lista.
    DbSet<TrdVersion> TrdVersiones { get; }
    DbSet<SerieDocumental> SeriesDocumentales { get; }
    DbSet<ListaMaestra> ListasMaestras { get; }
    DbSet<ListaOpcion> ListaOpciones { get; }
    // Construccion de la TRD (RF04): cruce Dependencia x Serie con reglas y metadatos de expediente.
    DbSet<TrdAsignacion> TrdAsignaciones { get; }
    DbSet<TrdMetadato> TrdMetadatos { get; }
    // Tipologias documentales (RF05): tipos de documento por asignacion + metadatos de documento.
    DbSet<TrdTipologia> TrdTipologias { get; }
    // Topografia fisica (RF06): niveles configurables + arbol de elementos fisicos.
    DbSet<TopografiaNivel> TopografiaNiveles { get; }
    DbSet<TopografiaElemento> TopografiaElementos { get; }

    // Gestion integral de expedientes (base de RQ03): el contenedor archivistico y sus metadatos
    // dinamicos (EAV sobre el motor de RQ02, DAT-04). El consecutivo del codigo usa TenantSequences.
    DbSet<Expediente> Expedientes { get; }
    DbSet<ExpedienteMetadato> ExpedienteMetadatos { get; }

    // Gestion integral de documentos (base de RQ04): el contenido. Binario en object storage (ADR-009,
    // nunca BLOB); metadatos EAV sobre el motor de RQ02 (DAT-04, contexto Documento).
    DbSet<Documento> Documentos { get; }
    DbSet<DocumentoMetadato> DocumentoMetadatos { get; }
    // Tareas de validacion (RQ04 - RF11/RF12): revision/aprobacion. Flujo de metadatos paralelo que NO
    // cambia el estado del documento.
    DbSet<DocumentoValidacion> DocumentoValidaciones { get; }
    // Plantillas documentales (RQ04 - RF09): documento parametrizado con variables, asociado N:N a
    // tipologias. Configuracion que se consume al crear documentos (RF10).
    DbSet<Plantilla> Plantillas { get; }
    DbSet<PlantillaTipo> PlantillaTipos { get; }

    // Motor de formularios dinamicos (RQ08, port ECOREX): definicion con arbol contenedores ->
    // preguntas; respuestas como documento JSON (no EAV por fila).
    DbSet<FormDefinition> FormDefinitions { get; }
    DbSet<FormContainer> FormContainers { get; }
    DbSet<FormQuestion> FormQuestions { get; }
    DbSet<FormResponse> FormResponses { get; }

    // Motor de flujos BPMN (RQ11, port del motor BPMN de ECOREX): definicion (XML BPMN) con
    // nodos/aristas materializados; instancias con historial append-only; asignacion por nodo.
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowNode> WorkflowNodes { get; }
    DbSet<WorkflowEdge> WorkflowEdges { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowStepHistory> WorkflowStepHistories { get; }
    DbSet<WorkflowNodePolicy> WorkflowNodePolicies { get; }

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
