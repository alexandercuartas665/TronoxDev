namespace Tronox.Application.MenuConfig;

/// <summary>
/// Definicion CANONICA del arbol de navegacion de TRONOX (RQ01 - RF09 5.9.4, punto 1).
///
/// Vive en Application y es LOGICA PURA (sin EF), igual que RolCatalogo y por la misma razon: la
/// consumen el aprovisionamiento del alta del tenant (MenuProvisioningService) y los tests, de modo
/// que las dos definiciones no puedan derivar. RF09 5.9.4 punto 4 lo exige explicitamente: "la
/// semilla del entorno demo y la del alta real deben ser la misma definicion".
///
/// FUENTE: MAPA_MENU_SISTEMA_TRONOX del vault (secciones, 17 modulos y, por modulo, que es item
/// real del menu lateral y que es pantalla interna o accion contextual). Se respeta esa distincion:
/// las acciones contextuales (Firmar, Distribuir, Crear expediente, FUID...) y las pantallas de
/// detalle NO son nodos del menu.
///
/// POR QUE IMPORTA TANTO QUE ESTE COMPLETO: el catalogo de modulos del tenant se DERIVA de este
/// arbol (nodos Item con Route). Si el arbol solo trae las secciones, no hay modulos, la matriz de
/// permisos del Super Administrador nace VACIA y, con el enforcement fail-closed de ADR-004, el
/// tenant nace inusable. Es la misma trampa de ADR-001 entrando por otra puerta.
///
/// EXCLUSIONES DELIBERADAS (documentadas en el MAPA):
/// - RQ16 (Capa IA) NO tiene menu propio: se inserta en otros modulos. Su unica presencia aqui es
///   el item de configuracion dentro de RQ01, tal como dice el MAPA.
/// - RQ14 (TRONOX Console) es una aplicacion de plataforma aparte, no un modulo del tenant.
/// - Los portales externos (Ciudadano RQ06, Verificador RQ05, Sede del Contratista RQ12, Portal
///   Laboral RQ17) viven fuera del SGDEA interno. Del RQ06 solo entran sus bandejas de GESTION
///   INTERNA.
/// - Los subniveles DINAMICOS (Contratos/Convenios de RQ12 y Personal/Procesos de RQ17 se
///   construyen desde las series y estados que active el tenant; Mis Vistas de RQ03 son bandejas
///   del usuario) se siembran como nodo PADRE unicamente: inventar hijos fijos los desincronizaria.
/// </summary>
public static class MenuCatalogo
{
    /// <summary>
    /// Un item hoja del menu. <paramref name="Ruta"/> es a la vez destino de navegacion y LLAVE DE
    /// PERMISOS (RF09 5.9.3): renombrar el item no altera sus permisos, cambiar la ruta si.
    /// Las rutas con prefijo "modulo/" resuelven en la pagina generica de modulo pendiente.
    /// </summary>
    public sealed record ItemSemilla(string Nombre, string Ruta, string? CodigoRf = null);

    /// <summary>Modulo agrupador dentro de una seccion (nodo Subgroup).</summary>
    public sealed record GrupoSemilla(
        string Nombre, string Slug, string Icono, string? CodigoRf, IReadOnlyList<ItemSemilla> Items);

    /// <summary>Seccion acordeon de primer nivel. Puede llevar grupos y/o items sueltos.</summary>
    public sealed record SeccionSemilla(
        string Nombre,
        string Slug,
        string Icono,
        IReadOnlyList<GrupoSemilla> Grupos,
        IReadOnlyList<ItemSemilla> Items);

    /// <summary>Nombre de la vista predeterminada que se siembra en el alta del tenant.</summary>
    public const string NombreVistaPredeterminada = "Completa";

    /// <summary>Enlace suelto de primer nivel (nodo QuickLink), antes de las secciones.</summary>
    public static readonly ItemSemilla Inicio = new("Inicio", "inicio");

    public const string IconoInicio = "bi-house";

    public static readonly IReadOnlyList<SeccionSemilla> Secciones =
    [
        // ------------------------------------------------------------------ CONFIGURACION
        new("CONFIGURACION", "configuracion", "bi-sliders",
        [
            // RQ01 Seccion A - General.
            new("Configuracion General", "configuracion-general", "bi-gear", "RQ01",
            [
                new("Datos de la Entidad", "modulo/datos-entidad", "RF01"),
                new("Sedes / Sucursales", "modulo/sedes", "RF01"),
                new("Parametros de Seguridad", "modulo/parametros-seguridad", "RF01-P"),
                new("Configuracion SMTP", "modulo/configuracion-smtp", "RF01-P"),
                new("Niveles de Clasificacion", "modulo/niveles-clasificacion", "RF01-P"),
                new("Calendario Habil", "modulo/calendario-habil", "RF01-P"),
                new("Esquema de Radicacion", "modulo/esquema-radicacion", "RF01-P"),
                // Sede unica de la configuracion de RQ05, RQ09, RQ11 y RQ06 (patron transversal 1
                // del MAPA: "[config en RQ01]"). No se dispersa un item por modulo.
                new("Modulos del Sistema", "modulo/configuracion-modulos", "RF01-P"),
                // Unica presencia de RQ16 en el menu: RQ01 > Configuracion General > IA.
                new("Inteligencia Artificial", "modulo/configuracion-ia", "RQ16")
            ]),

            // RQ01 Seccion B - Organizacional.
            new("Estructura Organizacional", "organizacional", "bi-diagram-3", "RQ01",
            [
                new("Fondos Documentales", "modulo/fondos-documentales", "RF02"),
                new("Estructura Organica", "dependencias", "RF03"),
                new("Catalogo de Cargos", "modulo/cargos", "RF04"),
                new("Roles y Permisos", "roles-permisos", "RF05"),
                new("Usuarios / Funcionarios", "admin-usuarios", "RF06"),
                new("Mi Perfil", "modulo/mi-perfil", "RF07"),
                new("Carga Masiva Asistida", "modulo/carga-masiva-organizacional", "RF08")
            ]),

            // RQ02: el MAPA anota que el menu mapea 1:1 los 7 RF de la spec.
            new("Configuracion Documental", "configuracion-documental", "bi-journals", "RQ02",
            [
                new("Versiones de TRD", "modulo/trd-versiones", "RF01"),
                new("Catalogo de Series y Subseries", "modulo/series-subseries", "RF02"),
                new("Listas Maestras", "modulo/listas-maestras", "RF03"),
                new("Construccion de la TRD", "modulo/trd-construccion", "RF04"),
                new("Tipologias Documentales", "modulo/tipologias-documentales", "RF05"),
                new("Topografia Fisica", "modulo/topografia-fisica", "RF06"),
                new("Carga Masiva de TRD", "modulo/trd-carga-masiva", "RF07")
            ])
        ], []),

        // ------------------------------------------------------------ GESTION DOCUMENTAL
        new("GESTION DOCUMENTAL", "gestion-documental", "bi-folder",
        [
            new("Expedientes", "expedientes", "bi-folder2-open", "RQ03",
            [
                new("Mis Expedientes", "modulo/expedientes-mios", "RQ03"),
                new("Compartidos Conmigo", "modulo/expedientes-compartidos", "RQ03"),
                new("Publicos", "modulo/expedientes-publicos", "RQ03"),
                // Central e Historico dependen del submodulo Transferencias (activable en RQ01):
                // el nodo se siembra y el filtrado por permiso/submodulo decide si se ve.
                new("Archivo Central", "modulo/expedientes-archivo-central", "RQ03"),
                new("Archivo Historico", "modulo/expedientes-archivo-historico", "RQ03"),
                // Bandejas personalizadas del usuario: nodo padre, hijos dinamicos.
                new("Mis Vistas", "modulo/expedientes-mis-vistas", "RQ03")
            ]),

            new("Documentos", "documentos", "bi-file-earmark-text", "RQ04",
            [
                new("Mis Documentos", "modulo/documentos", "RQ04"),
                new("Mis Borradores", "modulo/documentos-borradores", "RQ04"),
                new("Archivados por mi", "modulo/documentos-archivados", "RQ04"),
                new("Compartidos conmigo", "modulo/documentos-compartidos", "RQ04"),
                new("Tramitar", "modulo/documentos-tramitar", "RQ04"),
                new("Plantillas", "modulo/plantillas-documentales", "RQ04")
            ]),

            // RQ05: en la operacion diaria el modulo tiene UN solo item propio, "Mis Firmas",
            // con sus cuatro bandejas. Lo demas es accion contextual sobre un documento (RQ04),
            // configuracion en RQ01, o el hito futuro de la Seccion B (firma digital certificada).
            new("Mis Firmas", "mis-firmas", "bi-pen", "RQ05",
            [
                new("Pendientes", "modulo/firmas-pendientes", "RQ05"),
                new("Enviadas", "modulo/firmas-enviadas", "RQ05"),
                new("Completadas", "modulo/firmas-completadas", "RQ05"),
                new("Rechazadas", "modulo/firmas-rechazadas", "RQ05")
            ]),

            new("Formularios", "formularios", "bi-ui-checks-grid", "RQ08",
            [
                new("Listado de Formularios", "modulo/formularios", "RQ08"),
                new("Plantillas de Formularios", "modulo/formularios-plantillas", "RQ08"),
                new("Bandeja de Respuestas", "modulo/formularios-respuestas", "RQ08"),
                new("Respuestas Huerfanas", "modulo/formularios-respuestas-huerfanas", "RQ08")
            ]),

            new("Workflow", "workflow", "bi-diagram-2", "RQ11",
            [
                new("Mis Workflows", "modulo/workflows", "RQ11"),
                new("Estados Personalizados", "modulo/workflow-estados", "RQ11"),
                new("Biblioteca de Plantillas", "modulo/workflow-plantillas", "RQ11"),
                new("Panel de Monitoreo", "modulo/workflow-monitoreo", "RQ11")
            ])
        ], []),

        // ------------------------------------------------------- CORRESPONDENCIA Y TRAMITE
        new("CORRESPONDENCIA Y TRAMITE", "correspondencia", "bi-envelope",
        [
            new("Radicacion", "radicacion", "bi-inbox", "RQ09",
            [
                new("Radicados de Entrada", "modulo/radicados-entrada", "RQ09"),
                new("Radicados de Salida", "modulo/radicados-salida", "RQ09"),
                new("Radicados Internos", "modulo/radicados-internos", "RQ09"),
                new("Correos por Revisar", "modulo/radicacion-correos", "RQ09"),
                new("Panel de Control", "modulo/radicacion-panel", "RQ09"),
                new("Reportes de Radicacion", "modulo/radicacion-reportes", "RQ09")
            ]),

            // RQ10: "Mis Tareas" es deliberadamente UNA bandeja con filtros, no varias bandejas.
            new("Gestion y Tramite", "tramite", "bi-list-check", "RQ10",
            [
                new("Mis Tareas", "modulo/mis-tareas", "RQ10"),
                new("Correos Capturados", "modulo/tramite-correos-capturados", "RQ10"),
                new("Configuracion de Tramite", "modulo/tramite-configuracion", "RQ10")
            ]),

            new("PQRSD", "pqrsd", "bi-chat-left-text", "RQ15",
            [
                new("Dashboard PQRSD", "modulo/pqrsd-dashboard", "RQ15"),
                new("Bandeja de PQRSD", "modulo/pqrsd", "RQ15"),
                new("Expedientes PQRSD", "modulo/pqrsd-expedientes", "RQ15"),
                new("Panel de Vencimientos", "modulo/pqrsd-vencimientos", "RQ15"),
                new("Reportes y FURAG", "modulo/pqrsd-reportes", "RQ15"),
                new("Configuracion de PQRSD", "modulo/pqrsd-configuracion", "RQ15")
            ])
        ], []),

        // ------------------------------------------------------------ CIUDADANO Y TERCEROS
        new("CIUDADANO Y TERCEROS", "ciudadano-terceros", "bi-people",
        [
            // Solo la cara de GESTION INTERNA del RQ06. La cara externa (radicar PQRSD, subir
            // documentos, firmar, consultar estado) vive en ciudadano.{sigla}.tronox.co.
            new("Portal Ciudadano", "portal-ciudadano", "bi-globe", "RQ06",
            [
                new("Documentos Recibidos", "modulo/portal-documentos-recibidos", "RQ06"),
                new("Firmas Enviadas", "modulo/portal-firmas-enviadas", "RQ06")
            ]),

            new("Terceros", "terceros", "bi-person-vcard", "RQ07",
            [
                new("Catalogo de Terceros", "modulo/terceros", "RQ07"),
                new("Importacion y Exportacion Masiva", "modulo/terceros-carga-masiva", "RQ07")
            ])
        ], []),

        // ---------------------------------------------------------- PROCESOS ESPECIALIZADOS
        new("PROCESOS ESPECIALIZADOS", "procesos", "bi-briefcase",
        [
            new("Contratacion", "contratacion", "bi-file-earmark-ruled", "RQ12",
            [
                new("Dashboard de Contratacion", "modulo/contratacion-dashboard", "RQ12"),
                // Contratos y Convenios expanden sus subniveles desde las series que el tenant
                // vincula en la TRD: se siembra el nodo padre, nunca hijos fijos.
                new("Contratos", "modulo/contratos", "RQ12"),
                new("Convenios", "modulo/convenios", "RQ12"),
                new("Expedientes Contractuales", "modulo/contratacion-expedientes", "RQ12"),
                new("Bandeja del Supervisor", "modulo/contratacion-supervisor", "RQ12"),
                new("CDP / RP", "modulo/contratacion-respaldos", "RQ12"),
                new("Panel de Vencimientos", "modulo/contratacion-vencimientos", "RQ12"),
                new("Calendario Contractual", "modulo/contratacion-calendario", "RQ12"),
                new("Reportes de Contratacion", "modulo/contratacion-reportes", "RQ12"),
                new("Configuracion de Contratacion", "modulo/contratacion-configuracion", "RQ12")
            ]),

            new("Gestion Laboral", "laboral", "bi-person-badge", "RQ17",
            [
                new("Dashboard Laboral", "modulo/laboral-dashboard", "RQ17"),
                // Personal y Procesos expanden sus subniveles desde los estados y convocatorias
                // que configure el tenant: nodo padre unicamente.
                new("Personal", "modulo/laboral-personal", "RQ17"),
                new("Procesos de Seleccion", "modulo/laboral-procesos", "RQ17"),
                new("Historias Laborales", "modulo/laboral-historias", "RQ17"),
                new("Mis Tareas Laborales", "modulo/laboral-tareas", "RQ17"),
                new("Panel de Vencimientos", "modulo/laboral-vencimientos", "RQ17"),
                new("Reportes Laborales", "modulo/laboral-reportes", "RQ17"),
                new("Configuracion Laboral", "modulo/laboral-configuracion", "RQ17")
            ])
        ], []),

        // --------------------------------------------------------- INTELIGENCIA Y ANALITICA
        new("INTELIGENCIA Y ANALITICA", "analitica", "bi-graph-up",
        [
            new("Analitica y Reportes", "analitica-reportes", "bi-bar-chart", "RQ13",
            [
                new("Dashboard Ejecutivo", "modulo/analitica-dashboard-ejecutivo", "RQ13"),
                new("Dashboard Operativo", "modulo/analitica-dashboard-operativo", "RQ13"),
                new("Reportes Predefinidos", "modulo/analitica-reportes", "RQ13"),
                new("Mis Reportes", "modulo/analitica-mis-reportes", "RQ13"),
                new("Constructor de Reportes", "modulo/analitica-constructor", "RQ13"),
                new("Centro de Alertas", "modulo/analitica-alertas", "RQ13"),
                new("Configuracion de Analitica", "modulo/analitica-configuracion", "RQ13")
            ])
        ], []),

        // ------------------------------------------------------------------------ SISTEMA
        // Items sueltos: son dos pantallas de gobierno del propio sistema, no un modulo.
        new("SISTEMA", "sistema", "bi-shield-lock", [],
        [
            // Inalterable por RNF-04: ningun rol puede modificarla ni borrarla.
            new("Pistas de Auditoria", "modulo/pistas-auditoria", "RNF-04"),
            new("Vistas del Menu", "configuracion-menu", "RF09")
        ])
    ];

    /// <summary>Todas las rutas de item del arbol canonico: son las LLAVES DE PERMISO del tenant.</summary>
    public static IReadOnlyList<string> RutasDeItem { get; } = Secciones
        .SelectMany(s => s.Items.Concat(s.Grupos.SelectMany(g => g.Items)))
        .Select(i => i.Ruta)
        .ToList();

    /// <summary>Numero total de nodos del arbol (QuickLink + secciones + grupos + items).</summary>
    public static int TotalNodos { get; } =
        1
        + Secciones.Count
        + Secciones.Sum(s => s.Grupos.Count)
        + Secciones.Sum(s => s.Items.Count + s.Grupos.Sum(g => g.Items.Count));
}
