# PROGRESO - TRONOX SGDEA

Bitacora de entregables y decisiones. Fuente de verdad funcional: vault Obsidian
`OBSIDIAN.TRONOX` (empezar por `00 - INDICE.md`).

> En este archivo, **ECOREX** siempre se refiere al sistema hermano del que se clono el
> backbone (`C:\desarrolloia\ecorex.tareas`). No confundir con TRONOX.

**Estado a 2026-07-24:** 36 commits · **389 tests en verde** · 6 migraciones aplicadas ·
desplegado en produccion (demo) · RQ01 con 6 de 9 RF construidos.

---

## 1. Fase 0 - Fundacion (CERRADA)

| # | Entregable | Estado |
|---|---|---|
| 0.1 | Clonar backbone + recablear remotos | HECHO (historia huerfana, `.git` 97 -> 10.9 MB) |
| 0.2 | Renombrar `Ecorex.*` -> `Tronox.*` | HECHO (343 archivos, 10 proyectos) |
| 0.3 | Podar dominio ajeno | HECHO (106 entidades, 15 carpetas, 129 migraciones) |
| 0.4 | Ids `Guid` -> `BIGINT` (DAT-01) | HECHO |
| 0.5 | Docker: bloque de puertos propio + preflight | HECHO (5 servicios healthy, 30 vecinos intactos) |
| 0.6 | Migracion inicial limpia PostgreSQL | HECHO (29 tablas, `tenant_id bigint NOT NULL`) |
| 0.7 | Test de aislamiento cross-tenant | HECHO (+ guarda estructural) |
| 0.8 | Plantilla Velzon integrada | HECHO |
| 0.9 | `CLAUDE.md` propio de TRONOX | HECHO |

Puertos de desarrollo: 5443 postgres · 6390 redis · 5683/15683 rabbitmq · 9004/9005 minio ·
8093 adminer · 8095 web. Proyecto compose `tronox`, red `tronox-net`.

---

## 2. Fase 1 - RQ01 Configuracion General y Organizacional

| RF | Modulo | Estado |
|---|---|---|
| RF01 | **Datos de la Entidad** | HECHO - DV del NIT (algoritmo DIAN), `codigo_fondo_agn` autogenerado y de solo lectura (M01), sigla max 10, obligatoriedad condicional si Publica, selectores encadenados, subformulario de Sedes |
| RF01-P.3 | **Niveles de Clasificacion** | HECHO - los 4 niveles sembrados al alta del tenant |
| RF02 | **Fondos Documentales** | HECHO (backend) - codigo unico por tenant, fondo Cerrado de solo lectura, `sede_id` NULL = transversal. **Sin pantalla propia todavia** |
| RF03 | **Dependencias** | HECHO - arbol unico con clasificador (ADR-003), ciclos fail-closed, archivado, vigencias, sucesora |
| RF04 | **Catalogo de Cargos** | HECHO - vista de catalogo de los nodos `Cargo`; `codigo_dafp` solo si la entidad es Publica; no se inactiva un cargo con funcionarios activos; **codigo de cargo unico en el tenant** (reconciliado con el legacy `CatalogoCargos.aspx`, 2026-07-27) |
| RF05 | **Roles y Permisos** | HECHO - 6 acciones, multi-rol con vigencia, union por OR, nivel maximo, **fail-closed** (ADR-004) |
| RF06 | **Usuarios / Funcionarios** | HECHO - documento y correo unicos por tenant, activacion exige dependencia+cargo+rol, dependencia **derivada** del cargo |
| RF07 | Mi Perfil | PENDIENTE |
| RF08 | Carga Masiva Asistida | PENDIENTE |
| RF09 | **Administrador de Menu** | HECHO - menu en BD (136 nodos), arbol canonico del prototipo, filtrado por permisos |

**Lo que NO existe todavia (de RQ01):** RF07 (Mi Perfil) y RF08 (Carga Masiva). Ver deuda tecnica.

---

## 2.b Fase 2 - RQ02 Configuracion Documental (en curso)

| RF | Modulo | Estado |
|---|---|---|
| RF01 | **Versiones de TRD** | HECHO - marco legal; una sola Vigente por tenant (indice unico parcial + flip a Historico al activar), maquina de estados (En Construccion/Vigente/Historico/Inactivo), sin borrado fisico, convalidacion solo si Publica. Migrado del legacy `doc_versionesTRD.aspx` (2026-07-27) |
| RF02 | **Catalogo de Series y Subseries** | HECHO - arbol autorreferencial (jerarquia ilimitada), codigo unico por nivel + nombre unico entre hermanos, inactivacion sin borrado fisico, ciclos fail-closed. Migrado del legacy `doc_catalogoTRD.aspx` (2026-07-27) |
| RF03 | **Administrador de Listas** | HECHO - maestro-detalle Lista/Opciones; nombre unico por tenant, clave (interna, nueva) unica en la lista, orden reordenable, usabilidad >= 2 activas, sin borrado fisico. Migrado del legacy `doc_adminlistas.aspx` (2026-07-27) |
| RF04 | **Construccion de la TRD (cruce Dependencia + Serie)** | HECHO - CCD automatico, personalizacion por dependencia, tiempos/disposicion/clasificacion, metadatos de expediente; solo-lectura por estado de version (RF01 3.1.3). Puente Abrir/Ver TRD desde RF01. **UI rehecha list-first** (lista "Gestion de TRD por dependencia" con Estado TRD DERIVADO + Ver/Crear + workspace), fiel al legacy `doc_tablaRetencionDocumental.aspx` (2026-08-01, ADR-007). `modo_codigo_serie` descartado (ADR-006) |
| RF05 | **Tipologias Documentales** | HECHO - tipos documentales por asignacion (nombre, soporte Fisico/Electronico/Hibrido, formato, obligatorio en expediente) + metadatos de documento (contexto Documento, colgados de la tipologia). Embebido en la pantalla de RF04 igual que el legacy (2026-08-01, ADR-007) |

**Wizards fieles al legacy (2026-08-04):** el modal "Crear" simple se reemplazo por los dos
asistentes que trae el fuente VB.NET `doc_tablaRetencionDocumental.aspx`:
`pnlModalSubserie` (wizard **Nueva Serie/Subserie de 4 pasos**: Datos basicos -> Caracterizacion
-> Metadatos de expediente -> Tipologias, cada una con sus metadatos de documento) y
`pnlModalTipologia` (wizard **Tipologia de 2 pasos**: Configuracion -> Confirmar, en modo alta y
edicion). El boton `+` contextual decide subserie vs tipologia segun la estructura de la serie,
igual que el legacy. `pnlModalProcedimiento` NO se construye (huerfano en el legacy: el
procedimiento se edita inline en el paso 2). Import CSV/JSON del paso 4 diferido a RF07.
Verificado end-to-end en local: el wizard crea asignacion + metadato + tipologia, genera el CCD
(`100.150`) y respeta el aislamiento por tenant. Al verificar se detecto que la BD dev estaba
atrasada (faltaba la migracion RF05 `trd_tipologia_id`); se aplico. Build verde, 513 tests.
| RF06 | **Topografia Fisica** | HECHO - jerarquia de niveles configurable + arbol de elementos con codigo topografico automatico (siglas raiz->hoja), ocupacion y estados. Migrado del legacy `NEWFRONT_doc_bodegas.aspx` (2026-07-28). Menu en GENERAL, bajo Datos de la Entidad (decision del usuario) |
| RF07..RF10 | (resto de RQ02) | PENDIENTE |

**Del resto (RQ05 a RQ17): nada construido** (salvo lo de RQ03/RQ04 abajo). El menu muestra las
opciones del arbol canonico, pero la mayoria llevan a una ficha de "modulo pendiente".
**Menu completo != sistema construido.**

---

## 2.c Fase 3 - Oleada Expedientes/Documentos (RQ03 + RQ04) - primeros slices

Migracion fiel de 4 modulos del legacy VB.NET (`C:\Desarrollo\core\...\Modulos\`), en este orden
(2026-08-04). Todos verificados end-to-end en local y con tests de reglas puras.

| Modulo | Legacy | Ruta / menu | Estado |
|---|---|---|---|
| **Mis Expedientes** (RQ03 - RF01/RF03/RF04/RF10) | `exp_bandeja` | `modulo/expedientes-mios` (Gestion Integral de Expedientes) | HECHO (slice bandeja+crear) - codigo estructurado `[dep]-[serie]-[anio]-[consec6]` con `TenantSequence`, herencia de clasificacion SOLO ELEVAR (RF10), metadatos EAV (DAT-04), inmutabilidad de la asignacion de TRD (DAT-03), **fail-closed por clasificacion resuelto en el servicio**. Eliminacion logica. Verificado: `100-150-2026-000001`. Diferido: detalle completo, cierre/reapertura+indice/firma, transferencias/fases, compartir, ubicacion, alertas, FUID/rotulos/exportadores, vistas y columnas personalizadas |
| **Mis Documentos** (RQ04 - RF15/RF16) | `doc_bandeja` | `modulo/documentos` (Gestion Integral de Documentos) | HECHO (slice borradores+archivar) - 3 bandejas; crear borrador (binario en **object storage Azure Blob** o Fisico), ver, editar, descargar, eliminar (unico borrado fisico), y **Archivar** en expediente (hereda asignacion TRD DAT-03, foliacion inmutable). Binario NUNCA en BD (invariante 9). Verificado con Azurite. Diferido: compartir (RF07), versionado (RF03), busqueda avanzada (RF14), OCR, editor/plantillas WYSIWYG (RF08/RF10), restricciones (RF13), referencias (RF17) |
| **Mis Tareas** (RQ04 - RF11/RF12) | `mis_tareas` | `modulo/tramite-mis-tareas` (Gestion y Tramite) | HECHO - solicitar Revision/Aprobacion desde un documento (RF11) + bandeja Pendientes/Historial con chips + Tramitar (Aprobar/Devolver/Rechazar, comentario obligatorio al devolver/rechazar). **La validacion NO cambia el estado del documento** (RF11 CA-1): flujo de metadatos paralelo. Solo el asignado responde. Diferido: circuitos secuencial/paralelo, notificacion por correo, badge en tiempo real |
| **Plantillas Documentales** (RQ04 - RF09) | `doc_plantillasDocumentales` | `modulo/formularios-plantillas` (Motor de Formularios, **eleccion del usuario**) | HECHO (slice CRUD) - plantilla con contenido y variables `{{...}}`, asociacion N:N a tipologias, estado Activa/Inactiva (sin borrado fisico), conteo de variables automatico. Catalogo de variables Sistema/Expediente/Firma + **Terceros deshabilitado hasta RQ07 (DAT-02)**. Diferido: editor WYSIWYG (CKEditor/TipTap) y consumo RF10 (crear documento desde plantilla) |

**Object storage = Azure Blob Storage** (ADR-009, eleccion del usuario, paridad con el legacy; NO
S3/MinIO como decia CLAUDE.md). Abstraccion `IObjectStorage` + `AzureBlobObjectStorage`; local con
**Azurite** (`tronox-azurite`). **Pendiente: reflejar ADR-009 en el vault + CLAUDE.md.**

**Menu:** se habilitaron los modulos padre `req008` (Motor de Formularios) y `req010` (Gestion y
Tramite) de Disabled -> InDevelopment para que Plantillas y Mis Tareas salgan en el sidebar (sus
items hermanos aun sin construir van al placeholder, como en Expedientes/Documentos).

**Ubicacion de Plantillas:** el vault la pone en Configuracion Documental (RQ02); el usuario eligio
Motor de Formularios (`modulo/formularios-plantillas`). Decision registrada aqui.

4 migraciones EF nuevas (`ExpedientesRq03Bandeja`, `DocumentosRq04`, `ValidacionesRq04Rf12`,
`PlantillasRq04Rf09`). Build verde, 540 tests.

---

## 2.d Fase 4 - Ports desde ECOREX.tareas (RQ08 Forms + RQ11 Workflow BPMN)

Ports del proyecto hermano `C:\DesarrolloIA\ECOREX.tareas` (Guid->long, sin las dependencias
podadas). Ambos verificados end-to-end en local. **Commit `5ba938b` + DESPLEGADOS a prod
(2026-08-05):** migraciones `FormulariosRq08` + `WorkflowBpmnRq11` auto-aplicadas, 10 tablas
nuevas, 30 contenedores (sin caer vecinos), HTTP 200, 0 reinicios. Permisos concedidos a los
roles admin de los 2 tenants de prod (formularios + workflows + workflows-ejecucion).
Backup previo: `backup_pre_rq08_rq11_20260805_092016.sql.gz`.

### RQ08 - Motor de Formularios Dinamicos (`modulo/formularios`) - PORT FIEL
Primer slice recortado (commit 5ba938b) fue calificado "mediocre" por el usuario: la idea era un
port MILIMETRICO. Se REHIZO como port fiel de ECOREX (JSON-por-respuesta, ADR-011; sobre tx-*, no
copiando el CSS de ECOREX; decision del usuario).

- **Dominio a fidelidad:** los 28 tipos de control (incl. CascadeConfigurator), FormQuestion con
  todos los campos (origen de datos/lookup, calculados, cascada, visibilidad por rol, formato,
  subform), FormDefinition transaccional + modulo + CardLayout, FormResponse con campos de registro;
  5 entidades nuevas: FormToken, FormRecordLink, WorkflowNodeForm, FormFlowLink y FormFieldCondition
  (regla condicional AUTOCONTENIDA que reemplaza el motor de Reglas podado).
- **Application:** motores puros (FormExpressionEvaluator calculados, FormGridCalculator + xlsx,
  Cascade config+runtime), validador completo (28 tipos + grilla por columna), evaluador de
  condiciones puro, servicios `FormDefinitionService` (913) + `FormResponseService` (571) completos
  (CRUD + move/duplicate, transaccional + consecutivo via ISequenceService, bandeja + export,
  maestro-detalle), tokens (SHA-256), framework de lookups (sin fuentes: diferidas).
- **Web (tx-*):** catalogo TARJETAS + TABLA (toggle, 4 KPIs, tabs Activos/Archivados, buscador,
  archivar/restaurar); disenador 3-COLUMNAS (paleta + arbol, barra de dispositivo, drag-drop,
  pestanas Diseno/Datos/Reglas, todos los contenedores/tipos, duplicar, publicar por URL); renderer
  de los 28 tipos (grilla dinamica, subform, cascada, firma/GPS/archivo, condiciones en vivo,
  autosave, anular); bandeja de modulo `/m/{code}`; impresion `/formularios/imprimir/{id}`.
- Migracion `FormsFidelidadRq08`. Build verde, 334 tests Application (incl. condiciones). Verificado
  e2e: catalogo tarjetas+tabla, disenador 3-col, llenar -> validar -> respuesta persistida (id 5).

**Diferido (con nota, no por recorte unilateral):** lookups de Tercero (hasta RQ07); Item/
DataContainer (modulos de ECOREX que no aplican a TRONOX); visor publico anonimo `/f/{token}`
(necesita plumbing de tenant-ambiente sin sesion); binding runtime formulario<->paso de flujo BPMN
(entidades listas); VLOOKUP de grilla. Ruido menor: 404 de choices.js/flatpickr (plugins Velzon).
**Pendientes de gobernanza: ADR del CSS tx-* (se aparta de Velzon-only para el diseñador) + vault.**

### RQ11 - Workflow Documental = motor BPMN (`modulo/workflows`) - **ADR-010**
El usuario eligio **portar el motor BPMN de ECOREX** (canvas bpmn-js), que **contradice la spec
RQ11 v2.0 del vault** (cadena de pasos estilo HubSpot, que rechaza BPMN explicitamente). Decision
documentada en **ADR-010**; el vault queda por actualizar.

- **Dominio (6):** `WorkflowDefinition` (XML BPMN + version inmutable), `WorkflowNode`/`Edge`
  (grafo materializado), `WorkflowInstance` (IVersioned), `WorkflowStepHistory` (token = filas
  append-only IsCurrent), `WorkflowNodePolicy` (asignacion por OrgUnit). Enums ya venian del clon.
- **Application:** motor `WorkflowEngine` (avance en cascada tope 50, compuertas auto-resueltas por
  `approval == 'X'`, reinicios por RestartNode, rechazo con reactivacion, cierre implicito),
  logica pura (`BpmnProcessParser`/`BpmnXmlWriter`/`WorkflowConditionEvaluator`/`WorkflowAutoLayout`),
  `WorkflowDesignService` (backend del editor bpmn-js), resolver de asignacion por organigrama
  (`OrgAssigneeTree` puro + `NodeAssigneeResolver` + `WorkflowNodePolicyService`). Hook de reglas
  = `NoOpWorkflowRuleHook`.
- **Podas respecto de ECOREX:** acople a TaskItem/Tareas-Kanban (eliminado), motor de Reglas
  (NoOp), formularios/agentes por nodo (diferidos), `BpmnXmlMerger` (no portado -> las mutaciones
  regeneran el XML en vez de mergear).
- **Web:** `Workflows.razor` (indice KPIs+tarjetas + editor embebido), `FlowEditor.razor` (canvas
  bpmn-js + paneles config/asignacion/condiciones), `WorkflowRuntime.razor` (arrancar+atender).
  bpmn-js v8.8.2 vendorizado en `wwwroot/lib/bpmnio/` (~484 KB) + `wwwroot/js/tronox-bpmn.js`.
  Menu `req011` Disabled -> Ready (2 hojas: Mis Workflows + Ejecucion).
- Migracion `WorkflowBpmnRq11`. Build verde, **14 tests puros nuevos**. Verificado e2e: crear ->
  disenar (canvas monta, paleta, shapes) -> importar BPMN -> publicar -> arrancar instancia ->
  Task -> compuerta enruta por condicion -> Task -> End -> instancia Completed.
- **Defectos corregidos en verificacion:** (a) `AddStep` usaba navegacion en vez de FK escalar
  para el nodo AsNoTracking (habria insertado nodos duplicados con IDs long); (b) `v@x.Version`
  renderizaba literal; (c) el editor no cambiaba de definicion tras importar por acceso concurrente
  al DbContext del circuito (se quito el `Reload` del switch y se anadio `@key`).
- **Limitacion conocida (chip):** importar un `.bpmn` SIN seccion de diagrama (DI) deja el lienzo
  en blanco (bpmn-js necesita coordenadas); el motor si computa el layout. Los flujos dibujados en
  bpmn-js siempre traen DI y renderizan bien.

---

## 2.e Fase 5 - Configuracion de Radicacion (RQ09 RF01)

Construido 2026-08-05, **config-first** (RQ09 la operacion no opera aun). El usuario pidio migrar
`.../Radicacion/rad_config.aspx`; **ese archivo NO existe** en el legacy (la carpeta `Radicacion/`
es el tablero del bot de facturas DIAN, dominio ERP; la radicacion documental legacy es
`doc_ventanillaunica.aspx`, la operacion). Se construyo **fiel al vault RQ09 RF01** (fuente de
verdad, que ademas ubica la config en Config General). Ubicacion: Config General y Organizacional
-> General -> **Configuracion Radicacion** (`modulo/config-radicacion`, el item ya existia como
placeholder; ahora desarrollado). Estilo tx-*, NO el legacy.

- **Dominio:** 8 enums + 5 entidades: `RadicacionConfig` (singleton: consecutivos RF01-1 + alertas
  SLA RF01-3), `TipoComunicacion` (RF01-2), `BuzonCorreo` (RF01-4, clave AES-256), `NotificacionRadicacionConfig`
  (RF01-5), `MigracionRadicadosLog` (RF01-6).
- **Application:** `RadicacionConfigService` (GetConfig crea+siembra 13 tipos base + 10 eventos),
  `TipoComunicacionService` (CRUD, base no eliminable), `BuzonCorreoService` (CRUD, ISecretProtector).
- **Infra:** 5 tablas, migracion `ConfigRadicacionRf01`, FKs a niveles/dependencias/entidad.
- **Web:** `ConfigRadicacion.razor` (1228 lineas) con 6 pestañas tx-* (Consecutivos con preview,
  Tipos, Alertas SLA, Buzones, Notificaciones, Migracion). Verificado e2e: 13 tipos base + config +
  10 eventos sembrados al primer acceso.
- **Diferido (integracion posterior, no recorte de UI):** worker de captura de correos IMAP/Graph;
  proceso asincrono de importacion de migracion historica; lectura del ultimo consecutivo del emisor
  de secuencias (hoy 0). Documentado en el vault: `REQ009/RQ09_RF01_Implementacion.md`.

---

## 2.f Administrador de Menu - mas interaccion en el editor (2026-08-06)

Peticion del usuario: mas control en la herramienta de configuracion del menu. El backend
(`MenuConfigService`) ya tenia `CreateNodeAsync`/`MoveNodeAsync`/`ToggleNodeVisibilityAsync`;
el trabajo fue exponerlo mejor en `ConfiguracionMenu.razor` (editor de la vista):

- **Crear subniveles:** el boton "+" de un contenedor antes SOLO creaba Items. Ahora una Section o
  Subgroup ofrece **dos** acciones: "Agregar subnivel" (crea Subgroup anidado) y "Agregar elemento"
  (Item). Las reglas de `MenuNodeKindRules` ya permitian Subgroup dentro de Section/Subgroup.
- **Mover a otro grupo (explicito):** boton "Mover a otro grupo" en Items y Subgroups que abre un
  modal con selector de contenedores validos (ruta "Seccion / Subgrupo" para desambiguar), excluyendo
  el propio nodo, su padre actual y sus descendientes. Complementa el drag-drop (fragil en Blazor).
- **Sin eliminacion, solo ocultar (peticion "de momento"):** se quito el boton papelera de las filas
  del arbol. Queda el toggle de visibilidad (ocultar/mostrar), que aplica a cualquier nodo: grupo,
  subnivel o elemento. `DeleteNodeAsync` queda en el codigo, inactivo, para reactivar luego.

Build verde. Pendiente: e2e visual (requiere login, que no automatizo por no teclear claves).

---

## 2.g Port Radicacion legacy VB.NET - Modulo 1: Panel de Control (2026-08-06)

Arranca el port MILIMETRICO del modulo de Radicacion desde el legacy VB.NET
(`C:\Desarrollo\core\Bootstrap\Formularios\Modulos\Radicacion\`), modulo por modulo, al sistema
nuevo. Decisiones del usuario: dominio REAL milimetrico; legacy manda y solo se adaptan invariantes;
config PQR (rad_config) se reconcilia al llegar. El legacy es SOLO LECTURA (nunca se escribe en el).

**Modulo 1 - Panel de Control** (legacy `rad_panel.aspx` + `rad_panel_op.ashx` -> `modulo/radicacion-panel`,
nodo de menu ya existente en MenuCatalogo bajo Gestion y Tramite / Ventanilla Unica). Dashboard de
SOLO LECTURA. Spec milimetrica del legacy en `scratchpad/spec_rad_panel.md`.

- **Dominio:** `Radicado` (espejo RAD_RADICADOS), `RadicadoTrazabilidad` (RAD_TRAZABILIDAD, append-only),
  `CorreoRecibido` (RAD_CORREOS) + 5 enums. Adaptaciones de invariante: tenant_id + filtro global;
  dependencias/funcionarios como FK (OrgUnit/TenantUser) en vez de codigos; remitente inline
  (RemitenteNombre/Anonimo) hasta que exista RQ07 Terceros (entonces FK RemitenteTerceroId).
- **Infra:** DbSets + config EF (indices por tenant, FK NO ACTION, trazas cascade) + migracion
  `RadicacionOperativaPanel` (3 tablas).
- **Application:** `RadicacionPanelService.GetDashboardAsync(desde,hasta)` = replica de AccDashboard
  (6 KPIs, 7 series, actividad) en LINQ parametrizado. QUIRKS del legacy conservados a proposito: los
  KPIs son "actuales/hoy" e ignoran el rango; actividad = ultimos 6.
- **Web:** `PanelRadicacion.razor` (`/modulo/radicacion-panel`): selector de periodo (5 chips + rango),
  6 tarjetas KPI, 7 graficos SVG/CSS a mano (hbars, donut, gauge SLA, lineas, vbars) fieles al legacy,
  tabla de actividad. Estilo tx-/tema (no el CSS legacy). Pop-up de detalle = marcador hasta portar
  rad_detalle.
- **Seed demo LOCAL** (solo tenant 1, `scratchpad/seed_panel_demo.sql`): 4 tipos, 3 dependencias,
  36 radicados, 5 trazas, 3 correos. Para probar el panel con datos (arranca vacio en prod).

Build verde. Diferido honesto: el panel se llena con datos reales al portar rad_radicar/rad_bandeja;
el filtro de visibilidad RF11-8 se integra con la bandeja.

---

## 2.h Port Radicacion - Modulo 2: Bandeja + Distribucion + Detalle (2026-08-06)

Segundo lote del port de Radicacion (rad_bandeja + dependencias). Decision del usuario: bandeja de
listado completa + **Distribuir funcional** + **rad_detalle portado milimetrico** (no marcador).
Responder / Registrar envio / + Nuevo Radicado quedan como marcadores hasta portar rad_radicar /
rad_salida. Specs milimetricas en scratchpad (spec_rad_bandeja / spec_rad_detalle / spec_distribucion).

- **Dominio (delta):** `RadicadoTarea` (RAD_TAREAS), `RadicadoArchivo` (RAD_RADICADO_ARCHIVOS ->
  object storage, invariante 9, sin BLOB), `RadicadoComunicacion` (RAD_COMUNICACIONES),
  `RadicadoVisibilidadPermiso` (RAD_PERMISOS_VISIBILIDAD) + enums `RadicadoTareaEstado`/`VisibilidadNivel`.
  Columnas nuevas en `Radicado`: descripcion, remitente (tipo_doc/documento/email/telefono), nivel_reserva,
  folios, num_anexos, soporte, radicado_relacionado (self-FK padre/salidas), es_respuesta_definitiva,
  estado/canal_envio. Migracion `RadicacionBandejaDistribucion` (4 tablas + columnas).
- **Application:** `RadicacionVisibilidadService` (resolver fail-closed, ADR-013),
  `RadicacionBandejaService` (listado con 6 tabs + filtros + contadores + catalogos), `RadicacionDistribucionService`
  (distribuir/reasignar en UNA transaccion, FK a OrgUnit/TenantUser, traza DISTRIBUIR/REASIGNAR),
  `RadicadoDetalleService` (DTO info+docs+traza+tareas+comunicaciones+padre+salidas). LINQ parametrizado.
- **Web:** `/modulo/radicacion` (Radicacion.razor): tabs con contadores, filtros, grilla E/S/I, export CSV,
  acciones Ver/Distribuir; modal Distribuir funcional. Detalle como drawer `RadDetalle.razor` (5 pestanas).
  Marcadores: Responder/Nuevo (rad_radicar), Registrar envio (rad_salida), visor de documentos (upload).
- **ADR-013:** visibilidad fail-closed - el permiso del modulo es el gate; la visibilidad es tightening
  aditivo; error -> Propios, nunca Todos (invierte el fail-open del legacy).

Build verde. Quirks del legacy NO replicados: fail-open, SQL concatenado, sin transaccion, SELECT MAX(REG),
funcionario por nombre, BLOB en BD, callbacks window.parent.

---

## 2.i Port Radicacion - Modulo 3: Correos por Revisar + fundaciones (2026-08-06)

Tercer lote del port de Radicacion (rad_correos). Decisiones del usuario: portar el calendario habil
ahora (festivos reales) y el boton "Simular correo" para datos de prueba. El flujo "radicar desde
correo" es el PRIMER camino que CREA radicados, asi que arrastra fundaciones compartidas con
rad_radicar. Spec en scratchpad/spec_rad_correos.md.

**Fundaciones (Fase 1):**
- **Calendario habil (RQ01):** `FestivosColombia` (calculo puro Ley Emiliani + Pascua/Computus),
  entidad `DiaFestivo`, `CalendarioHabilService` (EsHabil/ProximoHabil/SumarDiasHabiles + siembra por
  anio), pagina `/modulo/calendario-habil`.
- **Consecutivo:** se REUTILIZA `ISequenceService` (SELECT FOR UPDATE, scope tenant/tipo/anio -> reinicio
  anual por codigo). No se duplico logica.
- **`RadicadorService`:** orquestador que crea el Radicado (consecutivo + vencimiento SLA via calendario +
  numero Sigla+Cod+Anio+consec). Reutilizable por correos y rad_radicar.

**Correos por Revisar (Fase 2):**
- **Dominio:** `CorreoRecibido` extendida (message_id, cuerpo tratado, tipo_detectado, confianza,
  duplicado/ref, modo, radica_en, num_adjuntos, remitente_email...), `CorreoRecibidoAdjunto`
  (object storage), `CorreoDescartado` (log append-only). Migracion `CalendarioHabilYCorreos` (3 tablas).
- **Application:** `RadicacionCorreosService` (listar 2 tabs + contadores, detalle, radicar/vincular
  [usa RadicadorService, RF04-5 cierra termino], editar, descartar con causal, recuperar, simular).
- **Web:** `/modulo/radicacion-correos` (bandeja dos paneles + lectura + modales editar/descartar).

Diferido honesto: captura IMAP/Graph (worker), descarga/visor de adjuntos (necesita upload), tercero
RQ07 (remitente inline). Quirks legacy NO replicados: BLOB, SQL concatenado, fail-open, sin transaccion,
MAX(REG), consecutivo sin bloqueo. Build verde.

---

## 2.j Port Radicacion - Configuracion PQR (2026-08-06)

Port de las dos secciones que faltaban de rad_config (Prioridades + Portal Web). Decision del usuario:
PANTALLA APARTE (no extender "Configuracion Radicacion"). El resto de rad_config ya estaba cubierto por
la Config Radicacion RF01 (buzones ya tenian puerto+clave AES; el numero se compone con Entidad.Sigla en
RadicadorService, no requiere campos extra). Spec en scratchpad/spec_rad_config.md.

- **Dominio:** `RadPrioridad` (RAD_PRIORIDADES, base no eliminable) + `RadPortalConfig` (RAD_PORTAL_CONFIG,
  singleton, con Slug para resolver el tenant server-side en el portal publico). Migracion
  `ConfigPqrPrioridadesPortal` (2 tablas).
- **Application:** `ConfiguracionPqrService` (prioridades CRUD + siembra base Normal/Alta/Urgente; portal
  config get/save; toggles de tipos publicados via TipoComunicacion.HabilitadoWeb).
- **Web:** `/modulo/config-pqr` (ConfiguracionPqr.razor): pestanas Prioridades y Portal Web + tipos publicados.

Build verde.

---

## 2.k Port Radicacion - Portal Ciudadano (2026-08-06)

Port de rad_portal (superficie PUBLICA: radicar PQRSD + consultar estado, sin login). Alcance elegido:
"funcional + refuerzos base". Spec en scratchpad/spec_portal_ciudadano.md. ADR-014 (tenant por slug).

- **Dominio:** `Radicado` += PortalToken, RespuestaPublica, EsRespuestaPublica. Migracion
  `PortalCiudadanoRadicadoTokens`.
- **Application:** `PortalCiudadanoService` - ResolverTenant por SLUG (IgnoreQueryFilters), GetPortal
  (branding + tipos publicados via HabilitadoWeb), Radicar (reutiliza RadicadorService, canal Web, token),
  Consultar (numero+documento, solo datos publicos: estado, dependencia, semaforo, timeline publico,
  respuesta publica). Anonimos sin seguimiento.
- **Web:** `/portal/{slug}` (PortalCiudadano.razor, AllowAnonymous + EmptyLayout): radicar + consultar.
  Cada llamada corre bajo `AmbientTenantContext.Begin(tenant)` (aislamiento sin usuario, ADR-014).

Refuerzos DIFERIDOS: reCAPTCHA v3 real (toggle listo, llaves despues), rate-limit Redis, upload de
adjuntos a object storage. Quirks legacy NO replicados: tenant por query manipulable, captcha casero
System.Drawing, SQL concatenado, BLOB, rate-limit en memoria. Build verde.

---

## 3. Interfaz

- Login reconstruido sobre `auth-signin-cover` de Velzon con la marca de RQ01 (PLAN 3.1).
- Shell con el patron del prototipo: sidebar plano de 280px, sin el rail heredado, logo TRONOX +
  SGDEA, chip del tenant al pie.
- **5 paletas conmutables** portadas del prototipo (classic-light por defecto), persistidas en
  cookie y **renderizadas por el servidor** para que la navegacion no pierda el tema ni parpadee.
- Iconos Bootstrap Icons (MIT) vendorizados: los 117 nodos del menu con icono real.
- Panel de Control en `/inicio` con Chart.js vendorizado y **datos de demostracion marcados**.

---

## 4. Despliegue

Desplegado el 2026-07-23 en el host compartido `10.0.0.3` (con Visal, ECOREX, DokTrino y 11
stacks mas). Puerto **5680**, Postgres interno, proyecto compose `tronox`.
**27 contenedores vecinos antes y despues del alta: ninguno se cayo.**
Runbook completo y credenciales en el vault (`06. Deploy`).

Pendiente para URL publica: **dominio + bloque en el Caddy externo** (decision del usuario).

---

## 5. Decisiones tomadas (ADR en `docs/decisiones/`)

| ID | Decision | ADR |
|---|---|---|
| D-01 | Historia git huerfana (repo publico) | - |
| D-02 | `Ecorex.SuperAdmin` -> `Tronox.Web`; Console sera host nuevo | **ADR-002** |
| D-03 | TRONOX no procesa pagos: fuera la pasarela | - |
| D-04 | Estructura organizacional: **arbol unico con clasificador** | **ADR-003** |
| D-05 | Ids de entidad `BIGINT` | ADR-001 (vault) |
| D-06 | Enforcement **fail-closed** + anclaje del Super Administrador | **ADR-004** |
| D-07 | **No autenticado != no autorizado** | **ADR-005** |
| D-08 | Diseno **hibrido**: estructura Velzon + tokens de marca del prototipo | pendiente de ADR |

---

## 6. Defectos encontrados y corregidos (los que importan)

1. **El filtro global de tenant quedo desactivado** por la poda: el metodo generico
   `ApplyTenantFilter<TEntity>` se quedo vacio. Compilaba, arrancaba y **habria servido datos de
   todos los tenants**. Lo detecto el test de aislamiento al ejecutarlo por primera vez.
   Se restauro y se anadio `TenantFilterGuardTests` (guarda estructural), **verificando que la
   guarda detecta el fallo** al reintroducirlo a proposito.
2. **La auditoria de altas guardaba `EntityId = 0`**: `AuditWriter` copiaba el id antes de que la
   base lo generara. Afectaba a las 10 entradas de creacion. Se resolvio con resolucion diferida
   en la unidad de trabajo, no con 10 parches.
3. **Invitar usuario estaba roto**: se leia el id del `PlatformUser` antes de guardarlo (FK 23503).
4. **Poda excesiva**: se elimino `ConnectEndpoints.cs`, que **era la autenticacion de la API**
   (`/connect/token`). Se restauro. Leccion: al podar por lotes, juzgar cada archivo por su
   contenido, no por su vecindad.
5. **La matriz de permisos nacia vacia**: el catalogo de modulos se deriva del menu y el menu solo
   sembraba 8 nodos, asi que el Super Administrador nacia **sin un solo permiso** y con
   fail-closed el sistema era inusable. Se sembro el arbol canonico completo.
6. **Clic en el menu expulsaba al login** (reportado por el usuario): el handler de permisos leia
   del `AuthenticationState`, que **no tiene estado en la pasada HTTP** del middleware -> excepcion
   -> fail-closed -> `AccessDeniedPath` (= `/login`). Se resolvio usando
   `AuthorizationHandlerContext.User`, sin volver a tocar `IHttpContextAccessor` (ADR-004 intacto).
7. **La imagen de produccion casi se lleva la cadena de conexion de DESARROLLO**: `Program.cs`
   carga `appsettings.Development.local.json` incondicionalmente y el archivo entraba a la imagen.
   Se atrapo verificando la imagen en local ANTES de subirla.
8. **Un token de Mapbox** venia dentro de la plantilla Velzon y bloqueo el primer push. Se purgo
   de la historia. Mi escaneo previo no lo detecto: buscaba `password=`, no formatos de token.

> Patron: **ninguno de estos lo encontro el compilador.** Los encontraron los tests de
> invariantes, arrancar la aplicacion de verdad, y el usuario usandola.

---

## 7. Deuda tecnica registrada

1. **Modulos funcionales:** RQ02 a RQ17 sin construir. 91 pantallas son fichas de "pendiente".
2. **DIVIPOLA incompleto:** 33 departamentos reales, pero solo **37 municipios** (32 capitales +
   5 de Cundinamarca) de los ~1.100 del DANE. Declarado en `DivipolaSeed.cs`.
3. **`/auth/register` (auto-registro publico) debe retirarse:** en TRONOX los tenants se
   aprovisionan desde la Console (RQ14), no por un formulario abierto en internet.
4. **Sin backup** del Postgres de produccion.
5. **Licencia de Velzon:** es una plantilla comercial y el repo de codigo es PUBLICO. Sus assets
   (22 MB) estan versionados. **Sin resolver.**
6. **Delegacion temporal (RF06)**: es el pendiente P-01 del vault, a la espera del Product Owner.
7. **Solo el Super Administrador cambia el estado de la entidad** (RF01): la UI lo dice, el backend
   aun no lo impide.
8. **`Sede` con ubicacion nullable**: volverla obligatoria exige decidir que pasa con las sedes ya
   creadas sin ubicacion.
9. **Sin disparador para re-aprovisionar** un tenant existente (menu/matriz) desde la UI.
10. **Vulnerabilidad transitiva** `System.Security.Cryptography.Xml` (3 avisos de severidad alta),
    heredada de `DataProtection`; no hay version superior que aplicar hoy.
11. Pantallas de plataforma dentro de `Tronox.Web` (`Tenants`, `Plans`, `EquipoPlataforma`,
    `Anuncios`): se mueven a `Tronox.Console` cuando exista (ADR-002).
12. **Fondos (RF02) sin pantalla**: hoy crear una dependencia exige que exista un fondo cargado
    por otra via.

---

## 8. Divergencias con el vault (ver `ESTADO DE IMPLEMENTACION` en el vault)

El codigo se aparta de la especificacion en 8 puntos, todos con decision explicita del usuario y
ADR. Estan consolidados en el vault para que el equipo que lea Obsidian no lea ficcion.
