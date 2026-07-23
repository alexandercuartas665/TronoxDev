using Tronox.Domain.Enums;

namespace Tronox.Application.MenuConfig;

/// <summary>
/// Definicion CANONICA del arbol de navegacion del workspace del tenant.
///
/// FUENTE DE VERDAD: el PROTOTIPO unificado (Prototipo/assets/js/tronox-shell.js, arreglos GROUPS y
/// MODULES). Este catalogo reproduce ESE arbol de forma literal: mismos grupos, mismos modulos,
/// mismas pantallas y mismos rotulos. No es una reinterpretacion del MAPA del vault sino el reflejo
/// del prototipo que el cliente aprobo como referencia visual.
///
/// Vive en Application y es LOGICA PURA (sin EF), igual que RolCatalogo y por la misma razon: la
/// consumen el aprovisionamiento del alta del tenant (MenuProvisioningService) y los tests, de modo
/// que las dos definiciones no puedan derivar.
///
/// MAPEO prototipo -> modelo de nodos (Kind):
///   GROUP    (config, docs, corr, ...)      -> Section  (encabezado de grupo del sidebar).
///   MODULE   (req001..req017)               -> Subgroup (disclosure del modulo, con punto de estado).
///   sub-seccion de un modulo (General/...)  -> Subgroup ANIDADO dentro del modulo (encabezado interno).
///   SCREEN                                  -> Item     (enlace hoja; su Route es la llave de permisos).
///
/// Las pantallas con `children` del prototipo (req008 catalogo, req007 bandeja, req011 mis-workflows,
/// req014 tenants, req017 portal) se APLANAN: el padre y sus hijos quedan como Items hermanos bajo el
/// modulo. El modelo de nodos (RF09) define el Item como hoja sin descendencia, asi que aplanar es la
/// forma de conservar TODAS las pantallas navegables sin inventar un nivel que el editor no soporta.
/// Las unicas sub-secciones que se modelan como Subgroup anidado son las que en el prototipo son
/// encabezados SIN pantalla propia (General/Organizacional/Sistema de req001; Gestion interna /
/// Portal externo de req006).
///
/// ESTADO POR MODULO (prototipo `estado`): listo -> Ready, prototipo -> InDevelopment, spec ->
/// Disabled. El estado se guarda en el nodo del MODULO (Subgroup), NUNCA en las pantallas: los Items
/// nacen Ready porque el catalogo de modulos de la matriz de permisos se deriva de los Items en estado
/// Ready (RolProvisioningService.ModulosDelMenuAsync); marcar una pantalla InDevelopment/Disabled la
/// sacaria de la matriz y volveria el modulo inaccesible. El punto de color del sidebar lee el estado
/// del modulo, que es donde el prototipo lo pinta.
///
/// RUTAS: la Route de cada Item es a la vez destino de navegacion y LLAVE DE PERMISOS (RF09 5.9.3).
/// Donde ya existe una pagina real se apunta a ella (dependencias, admin-usuarios, configuracion-menu);
/// el resto resuelve en la pagina generica /modulo/{slug}. Las pantallas que en el prototipo enlazan a
/// OTRO modulo (Configuracion Radicacion, Configuracion PQR, Plantilla Documentos, Mis Tareas, Firmar)
/// conservan su rotulo literal y reciben una Route propia y coherente: siguen siendo su propia llave de
/// permisos, sin colisionar con la pantalla destino.
///
/// INCLUSIONES respecto a versiones previas del catalogo (por decision del cliente, parquedad literal
/// con el prototipo): la Capa IA Transversal (RQ16) SI es un modulo propio bajo Inteligencia y
/// Analitica, y la TRONOX Console (RQ14) SI aparece bajo el grupo Plataforma. Los portales externos del
/// prototipo (Verificador, Sede del Contratista, Portal Laboral, cara externa del Portal Ciudadano)
/// tambien se reproducen como pantallas del sidebar.
/// </summary>
public static class MenuCatalogo
{
    /// <summary>
    /// Un item hoja del menu. <paramref name="Ruta"/> es a la vez destino de navegacion y LLAVE DE
    /// PERMISOS (RF09 5.9.3): renombrar el item no altera sus permisos, cambiar la ruta si.
    /// <paramref name="Icono"/> es la CLAVE de icono (clase Bootstrap Icons "bi-*", ADR-001).
    /// </summary>
    public sealed record ItemSemilla(string Nombre, string Ruta, string Icono, string? CodigoRf = null);

    /// <summary>
    /// Grupo agrupador (nodo Subgroup). Es un MODULO del prototipo o una sub-seccion anidada dentro de
    /// un modulo. Lleva sus <paramref name="Items"/> directos y/o <paramref name="Subgrupos"/> anidados.
    /// <paramref name="Estado"/> es el estado del modulo (punto de color del sidebar): solo tiene
    /// sentido en el nodo de modulo; las sub-secciones lo dejan en Ready.
    /// </summary>
    public sealed record GrupoSemilla(
        string Nombre,
        string Slug,
        string Icono,
        string? CodigoRf,
        IReadOnlyList<ItemSemilla> Items,
        MenuNodeState Estado = MenuNodeState.Ready,
        IReadOnlyList<GrupoSemilla>? Subgrupos = null);

    /// <summary>Seccion acordeon de primer nivel (nodo Section). Grupo del prototipo.</summary>
    public sealed record SeccionSemilla(
        string Nombre,
        string Slug,
        string Icono,
        IReadOnlyList<GrupoSemilla> Grupos,
        IReadOnlyList<ItemSemilla> Items);

    /// <summary>Nombre de la vista predeterminada que se siembra en el alta del tenant.</summary>
    public const string NombreVistaPredeterminada = "Completa";

    /// <summary>Enlace suelto de primer nivel (nodo QuickLink), antes de las secciones.</summary>
    public static readonly ItemSemilla Inicio = new("Inicio", "inicio", IconoInicio);

    public const string IconoInicio = "bi-house";

    public static readonly IReadOnlyList<SeccionSemilla> Secciones =
    [
        // ============================================================ CONFIGURACION
        new("Configuracion", "configuracion", "bi-sliders",
        [
            // REQ001 - un solo modulo con tres sub-secciones (General / Organizacional / Sistema).
            new("Configuracion General y Organizacional", "req001", "bi-building-gear", "RQ01", [],
                MenuNodeState.Ready,
                [
                    new("General", "req001-general", "bi-sliders", null,
                    [
                        new("Datos de la Entidad", "modulo/datos-entidad", "bi-building", "RF01"),
                        // Rotulos con tilde EXACTOS del prototipo (contenido de UI, no identificador).
                        new("Configuración Radicación", "modulo/config-radicacion", "bi-hash", "RF01"),
                        new("Configuración PQR", "modulo/config-pqr", "bi-megaphone", "RF01")
                    ]),
                    new("Organizacional", "req001-organizacional", "bi-diagram-3", null,
                    [
                        new("Dependencias", "dependencias", "bi-diagram-3", "RF03"),
                        new("Cargos", "modulo/cargos", "bi-person-badge", "RF04"),
                        new("Usuarios / Funcionarios", "admin-usuarios", "bi-people", "RF06")
                    ]),
                    new("Sistema", "req001-sistema", "bi-gear", null,
                    [
                        new("Administrador de Menu", "configuracion-menu", "bi-list-nested", "SIST")
                    ])
                ]),

            new("Configuracion Documental", "req002", "bi-collection", "RQ02",
            [
                new("Versiones de TRD", "modulo/trd-versiones", "bi-clock-history", "RF01"),
                new("Catalogo de Series y Subseries", "modulo/series-subseries", "bi-collection", "RF02"),
                new("Listas Maestras", "modulo/listas-maestras", "bi-list-ul", "RF03"),
                new("Tabla de Retencion Documental", "modulo/trd", "bi-table", "RF04"),
                new("Importar / Exportar", "modulo/trd-importar-exportar", "bi-cloud-arrow-up", "RF07"),
                new("Plantilla Documentos", "modulo/plantilla-documentos", "bi-file-earmark-richtext", "RF10")
            ])
        ], []),

        // ======================================================= GESTION DOCUMENTAL
        new("Gestion Documental", "gestion-documental", "bi-folder2-open",
        [
            new("Gestion Integral de Expedientes", "req003", "bi-folder", "RQ03",
            [
                new("Mis Expedientes", "modulo/expedientes-mios", "bi-folder", "RF01"),
                new("Compartidos Conmigo", "modulo/expedientes-compartidos", "bi-folder-symlink", "RF01"),
                new("Publicos", "modulo/expedientes-publicos", "bi-folder2-open", "RF01"),
                new("Archivo Central", "modulo/expedientes-archivo-central", "bi-archive", "RF14"),
                new("Archivo Historico", "modulo/expedientes-archivo-historico", "bi-bank", "RF14")
            ]),

            new("Gestion Integral de Documentos", "req004", "bi-file-earmark-text", "RQ04",
            [
                new("Mis Documentos", "modulo/documentos", "bi-file-earmark-text", "RF15")
            ]),

            new("Firma Electronica y Digital", "req005", "bi-pen", "RQ05",
            [
                new("Mis Firmas", "modulo/firmas-mis", "bi-inbox", "RF10"),
                new("Configuracion del Modulo de Firma", "modulo/firmas-config", "bi-gear", "RF01"),
                new("Proveedores ECD", "modulo/firmas-proveedores", "bi-shield-lock", "RF14"),
                new("Vigencia de Certificados", "modulo/firmas-vigencia", "bi-patch-check", "RF19"),
                new("Consumo y Metricas", "modulo/firmas-metricas", "bi-graph-up", "RF20"),
                new("Portal Verificador", "modulo/firmas-verificador", "bi-patch-check-fill", "RF04")
            ], MenuNodeState.InDevelopment),

            new("Motor de Formularios Dinamicos", "req008", "bi-ui-checks-grid", "RQ08",
            [
                new("Formularios (catalogo)", "modulo/formularios", "bi-ui-checks-grid", "RF01"),
                new("Nuevo Formulario", "modulo/formularios-nuevo", "bi-plus-square", "RF05"),
                new("Disenador de Formularios", "modulo/formularios-disenador", "bi-pencil-square", "RF02"),
                new("Plantillas", "modulo/formularios-plantillas", "bi-files", "RF05"),
                new("Bandeja de Respuestas", "modulo/formularios-respuestas", "bi-inbox", "RF07"),
                new("Respuestas Huerfanas", "modulo/formularios-huerfanas", "bi-inboxes", "RF07")
            ], MenuNodeState.Disabled),

            new("Workflow Documental", "req011", "bi-diagram-2", "RQ11",
            [
                new("Mis Workflows", "modulo/workflows", "bi-diagram-2", "RF03"),
                new("Constructor de Workflow", "modulo/workflows-constructor", "bi-diagram-3", "RF03"),
                new("Historial de Versiones", "modulo/workflows-versiones", "bi-clock-history", "RF08"),
                new("Estados Personalizados", "modulo/workflows-estados", "bi-flag", "RF02"),
                new("Biblioteca de Plantillas", "modulo/workflows-biblioteca", "bi-collection", "RF09"),
                new("Panel de Monitoreo", "modulo/workflows-monitoreo", "bi-activity", "RF11"),
                new("Configuracion del Modulo", "modulo/workflows-config", "bi-gear", "RF01")
            ], MenuNodeState.Disabled)
        ], []),

        // ========================================================= GESTION Y TRAMITE
        new("Gestion y Tramite", "gestion-tramite", "bi-inboxes",
        [
            new("Ventanilla Unica / Radicacion", "req009", "bi-inboxes", "RQ09",
            [
                new("Panel de Control", "modulo/radicacion-panel", "bi-speedometer2", "RF12"),
                new("Radicacion", "modulo/radicacion", "bi-inboxes", "RF11"),
                new("Correos por Revisar", "modulo/radicacion-correos", "bi-envelope", "RF04")
            ]),

            new("Gestion y Tramite", "req010", "bi-list-task", "RQ10",
            [
                new("Mis Tareas", "modulo/tramite-mis-tareas", "bi-check2-square", "RF12"),
                new("Firmar", "modulo/tramite-firmar", "bi-pen", "RF08")
            ], MenuNodeState.Disabled),

            new("PQRSD", "req015", "bi-megaphone", "RQ15",
            [
                new("Dashboard", "modulo/pqrsd-dashboard", "bi-speedometer2", "RF03"),
                new("Bandeja de PQRSD", "modulo/pqrsd", "bi-megaphone", "RF07"),
                new("Expedientes PQRSD", "modulo/pqrsd-expedientes", "bi-folder", "RF16"),
                new("Panel de Vencimientos", "modulo/pqrsd-vencimientos", "bi-calendar-x", "RF18"),
                new("Reportes y FURAG", "modulo/pqrsd-furag", "bi-graph-up", "RF19")
            ], MenuNodeState.Disabled)
        ], []),

        // ====================================================== CIUDADANO Y TERCEROS
        new("Ciudadano y Terceros", "ciudadano-terceros", "bi-people",
        [
            // REQ006 - dos sub-secciones: gestion interna y cara externa del portal.
            new("Portal Ciudadano", "req006", "bi-globe", "RQ06", [],
                MenuNodeState.Disabled,
                [
                    new("Gestion interna", "req006-interna", "bi-inbox", null,
                    [
                        new("Documentos Recibidos", "modulo/portal-documentos-recibidos", "bi-inbox", "RF08"),
                        new("Firmas Enviadas", "modulo/portal-firmas-enviadas", "bi-send", "RF08"),
                        new("Configuracion del Portal", "modulo/portal-config", "bi-gear", "RF01")
                    ]),
                    new("Portal Ciudadano (externo)", "req006-externo", "bi-globe", null,
                    [
                        new("Radicar PQRSD", "modulo/portal-ext-radicar", "bi-megaphone", "RF02"),
                        new("Subir Documentos", "modulo/portal-ext-subir", "bi-upload", "RF03"),
                        new("Firmar Documento", "modulo/portal-ext-firmar", "bi-pen", "RF04"),
                        new("Consultar Estado", "modulo/portal-ext-consulta", "bi-search", "RF05"),
                        new("FAQ / Contacto", "modulo/portal-ext-faq", "bi-question-circle", "RF01")
                    ])
                ]),

            new("Catalogo de Terceros", "req007", "bi-person-vcard", "RQ07",
            [
                new("Catalogo de Terceros", "modulo/terceros", "bi-people", "RF01"),
                new("Nuevo Tercero", "modulo/terceros-nuevo", "bi-person-plus", "RF01"),
                new("Vista 360 del Tercero", "modulo/terceros-vista360", "bi-person-vcard", "RF03"),
                new("Importacion y Exportacion Masiva", "modulo/terceros-importar", "bi-arrow-down-up", "RF02")
            ], MenuNodeState.Disabled)
        ], []),

        // ==================================================== PROCESOS ESPECIALIZADOS
        new("Procesos Especializados", "procesos-especializados", "bi-briefcase",
        [
            new("Contratacion", "req012", "bi-briefcase", "RQ12",
            [
                new("Dashboard", "modulo/contratacion-dashboard", "bi-speedometer2", "RF03"),
                new("Contratos", "modulo/contratos", "bi-file-earmark-medical", "RF05"),
                new("Convenios", "modulo/convenios", "bi-people", "RF05"),
                new("Expedientes Contractuales", "modulo/contratacion-expedientes", "bi-folder", "RF13"),
                new("Bandeja del Supervisor", "modulo/contratacion-supervisor", "bi-clipboard-check", "RF12"),
                new("CDP / RP (Respaldo)", "modulo/contratacion-respaldos", "bi-cash-coin", "RF04"),
                new("Panel de Vencimientos", "modulo/contratacion-vencimientos", "bi-calendar-x", "RF15"),
                new("Calendario", "modulo/contratacion-calendario", "bi-calendar3", "RF16"),
                new("Reportes", "modulo/contratacion-reportes", "bi-graph-up", "RF17"),
                new("Configuracion", "modulo/contratacion-config", "bi-gear", "RF01"),
                new("Sede del Contratista", "modulo/contratacion-sede", "bi-globe", "RF18")
            ], MenuNodeState.Disabled),

            new("Gestion Laboral", "req017", "bi-person-workspace", "RQ17",
            [
                new("Dashboard", "modulo/laboral-dashboard", "bi-speedometer2", "RF03"),
                new("Personal (empleados)", "modulo/laboral-personal", "bi-people", "RF04"),
                new("Procesos (seleccion)", "modulo/laboral-seleccion", "bi-person-plus", "RF07"),
                new("Historias Laborales", "modulo/laboral-historias", "bi-folder", "RF05"),
                new("Mis Tareas Laborales", "modulo/laboral-tareas", "bi-list-task", "RF06"),
                new("Panel de Vencimientos", "modulo/laboral-vencimientos", "bi-calendar-x", "RF13"),
                new("Organigrama", "modulo/laboral-organigrama", "bi-diagram-3", "RF17"),
                new("Reportes", "modulo/laboral-reportes", "bi-graph-up", "RF18"),
                new("Configuracion", "modulo/laboral-config", "bi-gear", "RF01"),
                new("Portal Laboral", "modulo/laboral-portal", "bi-globe", "RF16"),
                new("Convocatorias / Postulacion", "modulo/laboral-ext-convocatorias", "bi-megaphone", "RF16"),
                new("Mi Estado / Firma / Novedades", "modulo/laboral-ext-privado", "bi-person-check", "RF16")
            ], MenuNodeState.Disabled)
        ], []),

        // =================================================== INTELIGENCIA Y ANALITICA
        new("Inteligencia y Analitica", "inteligencia-analitica", "bi-graph-up",
        [
            new("Analitica y Reportes", "req013", "bi-graph-up-arrow", "RQ13",
            [
                new("Dashboard Ejecutivo", "modulo/analitica-dashboard-ejecutivo", "bi-speedometer2", "RF02"),
                new("Dashboard Operativo", "modulo/analitica-dashboard-operativo", "bi-activity", "RF03"),
                new("Reportes Predefinidos", "modulo/analitica-predefinidos", "bi-file-earmark-bar-graph", "RF05"),
                new("Mis Reportes", "modulo/analitica-mis-reportes", "bi-collection", "RF05"),
                new("Constructor Ad-Hoc", "modulo/analitica-adhoc", "bi-tools", "RF04"),
                new("Centro de Alertas", "modulo/analitica-alertas", "bi-bell", "RF06"),
                new("Configuracion", "modulo/analitica-config", "bi-gear", "RF01")
            ], MenuNodeState.Disabled),

            // RQ16: el prototipo lo trae como modulo propio de esta seccion (Capa IA Transversal).
            new("Capa IA Transversal", "req016", "bi-stars", "RQ16",
            [
                new("Plumita (asistente inline)", "modulo/ia-plumita", "bi-stars", "RF03"),
                new("Chat Documental", "modulo/ia-chat", "bi-chat-dots", "RF04"),
                new("Configuracion IA", "modulo/ia-config", "bi-gear", "RF01"),
                new("Consumo IA", "modulo/ia-consumo", "bi-graph-up", "RF05")
            ], MenuNodeState.Disabled)
        ], []),

        // ================================================================ PLATAFORMA
        // RQ14 (TRONOX Console): el prototipo lo incluye como grupo Plataforma del sidebar.
        new("Plataforma", "plataforma", "bi-hdd-stack",
        [
            new("TRONOX Console", "req014", "bi-hdd-stack", "RQ14",
            [
                new("Login A&D GROUP", "modulo/console-login", "bi-box-arrow-in-right"),
                new("Dashboard Global", "modulo/console-dashboard", "bi-globe2", "RF02"),
                new("Planes Comerciales", "modulo/console-planes", "bi-tags", "RF03"),
                new("Tenants", "modulo/console-tenants", "bi-buildings", "RF04"),
                new("Detalle del Tenant", "modulo/console-tenant-detalle", "bi-building", "RF05"),
                new("Metricas y Consumo", "modulo/console-consumo", "bi-graph-up", "RF06"),
                new("Alertas de Consumo", "modulo/console-alertas", "bi-exclamation-triangle", "RF07"),
                new("Avisos de Plataforma", "modulo/console-avisos", "bi-megaphone", "RF08"),
                new("Impersonacion", "modulo/console-impersonar", "bi-person-bounding-box", "RF09"),
                new("Log de Auditoria", "modulo/console-auditoria", "bi-journal-text", "RF10"),
                new("Reportes y Exportacion", "modulo/console-reportes", "bi-file-earmark-bar-graph", "RF11"),
                new("Usuarios de Console", "modulo/console-usuarios", "bi-person-gear", "RF01")
            ], MenuNodeState.Disabled)
        ], [])
    ];

    /// <summary>Todos los grupos del arbol (modulos y sub-secciones anidadas), en recorrido plano.</summary>
    public static IEnumerable<GrupoSemilla> TodosLosGrupos() =>
        Secciones.SelectMany(s => s.Grupos.SelectMany(AplanarGrupo));

    private static IEnumerable<GrupoSemilla> AplanarGrupo(GrupoSemilla grupo) =>
        new[] { grupo }.Concat((grupo.Subgrupos ?? []).SelectMany(AplanarGrupo));

    /// <summary>
    /// Todos los items del arbol (los de grupo -a cualquier profundidad- y los sueltos de seccion).
    /// </summary>
    public static IEnumerable<ItemSemilla> TodosLosItems() =>
        Secciones.SelectMany(s => s.Items)
            .Concat(TodosLosGrupos().SelectMany(g => g.Items));

    /// <summary>Todas las rutas de item del arbol canonico: son las LLAVES DE PERMISO del tenant.</summary>
    public static IReadOnlyList<string> RutasDeItem { get; } =
        TodosLosItems().Select(i => i.Ruta).ToList();

    /// <summary>Numero total de grupos (modulos + sub-secciones anidadas).</summary>
    public static int TotalSubgrupos { get; } = TodosLosGrupos().Count();

    /// <summary>
    /// Mapa Ruta/Slug -> clave de icono para TODOS los nodos del arbol canonico (quick link,
    /// secciones, grupos e items). Permite RELLENAR el icono de tenants que nacieron sin el.
    /// </summary>
    public static IReadOnlyDictionary<string, string> IconosPorRuta { get; } =
        new[] { (Inicio.Ruta, IconoInicio) }
            .Concat(Secciones.Select(s => (s.Slug, s.Icono)))
            .Concat(TodosLosGrupos().Select(g => (g.Slug, g.Icono)))
            .Concat(TodosLosItems().Select(i => (i.Ruta, i.Icono)))
            .GroupBy(t => t.Item1, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.Ordinal);

    /// <summary>Mapa Ruta/Slug -> NOMBRE canonico para todos los nodos del arbol.</summary>
    public static IReadOnlyDictionary<string, string> NombresPorRuta { get; } =
        new[] { (Inicio.Ruta, Inicio.Nombre) }
            .Concat(Secciones.Select(s => (s.Slug, s.Nombre)))
            .Concat(TodosLosGrupos().Select(g => (g.Slug, g.Nombre)))
            .Concat(TodosLosItems().Select(i => (i.Ruta, i.Nombre)))
            .GroupBy(t => t.Item1, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.Ordinal);

    /// <summary>
    /// Nombres ANTERIORES de nodos renombrados, indexados por ruta/slug. Lo consume el relleno de
    /// tenants existentes NO reconciliables (BackfillCanonicalNamesAsync). Con la alineacion al
    /// prototipo la ESTRUCTURA cambio por completo (rutas nuevas), asi que un tenant intacto se
    /// reconcilia re-sembrando su vista (ReconciliarVistaPredeterminadaAsync); el relleno por ruta
    /// queda como red de seguridad y hoy no rastrea renombrados.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> NombresAnterioresPorRuta =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // Los dos rotulos que ganaron tilde al copiarse literal del prototipo: los tenants ya
            // re-sembrados con la version ASCII se ponen al dia por relleno (no cambia su ruta).
            ["modulo/config-radicacion"] = ["Configuracion Radicacion"],
            ["modulo/config-pqr"] = ["Configuracion PQR"],
        };

    /// <summary>
    /// HUELLA del arbol ANTERIOR a la alineacion con el prototipo (el de 117 nodos): el conjunto
    /// EXACTO de sus claves de nodo (Route de los items, Slug de secciones/subgrupos, mas el quick
    /// link "inicio"). Es version-independiente porque las rutas no cambian con los rellenos de
    /// icono/nombre.
    ///
    /// La consume ReconciliarVistaPredeterminadaAsync para reconocer una vista INTACTA de la version
    /// previa y re-sembrarla con seguridad: se re-siembra solo si TODA clave del tenant pertenece a
    /// esta huella (es decir, el tenant no anadio ni un nodo propio). Un nodo con clave ajena a la
    /// huella (creado en el editor) rompe la condicion y la vista se deja tal cual.
    ///
    /// Se decidio una huella EXPLICITA en vez de fiarse de los campos de auditoria porque en un
    /// circuito Blazor interactivo no hay HttpContext y el editor no siempre sella UpdatedBy, asi que
    /// "modificado por un humano" no es distinguible de "tocado por un relleno del sistema".
    /// </summary>
    public static readonly IReadOnlySet<string> HuellaVistaAnterior = new HashSet<string>(StringComparer.Ordinal)
    {
        // Quick link + secciones + subgrupos del arbol anterior.
        "inicio",
        "configuracion", "gestion-documental", "correspondencia", "ciudadano-terceros",
        "procesos", "analitica", "sistema",
        "configuracion-general", "organizacional", "configuracion-documental", "expedientes",
        "documentos", "mis-firmas", "formularios", "workflow", "radicacion", "tramite", "pqrsd",
        "portal-ciudadano", "terceros", "contratacion", "laboral", "analitica-reportes",
        // Items del arbol anterior (rutas).
        "modulo/datos-entidad", "modulo/sedes", "modulo/parametros-seguridad", "modulo/configuracion-smtp",
        "modulo/niveles-clasificacion", "modulo/calendario-habil", "modulo/esquema-radicacion",
        "modulo/configuracion-modulos", "modulo/configuracion-ia",
        "modulo/fondos-documentales", "dependencias", "modulo/cargos", "roles-permisos", "admin-usuarios",
        "modulo/mi-perfil", "modulo/carga-masiva-organizacional",
        "modulo/trd-versiones", "modulo/series-subseries", "modulo/listas-maestras", "modulo/trd-construccion",
        "modulo/tipologias-documentales", "modulo/topografia-fisica", "modulo/trd-carga-masiva",
        "modulo/expedientes-mios", "modulo/expedientes-compartidos", "modulo/expedientes-publicos",
        "modulo/expedientes-archivo-central", "modulo/expedientes-archivo-historico", "modulo/expedientes-mis-vistas",
        "modulo/documentos", "modulo/documentos-borradores", "modulo/documentos-archivados",
        "modulo/documentos-compartidos", "modulo/documentos-tramitar", "modulo/plantillas-documentales",
        "modulo/firmas-pendientes", "modulo/firmas-enviadas", "modulo/firmas-completadas", "modulo/firmas-rechazadas",
        "modulo/formularios", "modulo/formularios-plantillas", "modulo/formularios-respuestas",
        "modulo/formularios-respuestas-huerfanas",
        "modulo/workflows", "modulo/workflow-estados", "modulo/workflow-plantillas", "modulo/workflow-monitoreo",
        "modulo/radicados-entrada", "modulo/radicados-salida", "modulo/radicados-internos",
        "modulo/radicacion-correos", "modulo/radicacion-panel", "modulo/radicacion-reportes",
        "modulo/mis-tareas", "modulo/tramite-correos-capturados", "modulo/tramite-configuracion",
        "modulo/pqrsd-dashboard", "modulo/pqrsd", "modulo/pqrsd-expedientes", "modulo/pqrsd-vencimientos",
        "modulo/pqrsd-reportes", "modulo/pqrsd-configuracion",
        "modulo/portal-documentos-recibidos", "modulo/portal-firmas-enviadas",
        "modulo/terceros", "modulo/terceros-carga-masiva",
        "modulo/contratacion-dashboard", "modulo/contratos", "modulo/convenios", "modulo/contratacion-expedientes",
        "modulo/contratacion-supervisor", "modulo/contratacion-respaldos", "modulo/contratacion-vencimientos",
        "modulo/contratacion-calendario", "modulo/contratacion-reportes", "modulo/contratacion-configuracion",
        "modulo/laboral-dashboard", "modulo/laboral-personal", "modulo/laboral-procesos", "modulo/laboral-historias",
        "modulo/laboral-tareas", "modulo/laboral-vencimientos", "modulo/laboral-organigrama",
        "modulo/laboral-reportes", "modulo/laboral-configuracion",
        "modulo/analitica-dashboard-ejecutivo", "modulo/analitica-dashboard-operativo", "modulo/analitica-reportes",
        "modulo/analitica-mis-reportes", "modulo/analitica-constructor", "modulo/analitica-alertas",
        "modulo/analitica-configuracion",
        "modulo/pistas-auditoria", "configuracion-menu"
    };

    /// <summary>Numero total de nodos del arbol (QuickLink + secciones + grupos + items).</summary>
    public static int TotalNodos { get; } =
        1
        + Secciones.Count
        + TotalSubgrupos
        + RutasDeItem.Count;
}
