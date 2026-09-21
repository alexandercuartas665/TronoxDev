# ADR-010: RQ11 Workflow — motor BPMN (port de ECOREX) en vez de cadena de pasos del vault

- Estado: Aceptada
- Fecha: 2026-08-04
- Decisores: usuario (product owner) + agente de desarrollo
- Relacionada: RQ11 (Workflow Documental), ADR-009 (Azure Blob), port de RQ08 (Forms)

## Contexto

La spec del vault `RQ11_Workflow_Documental_v2.md` (v2.0, marzo 2026, resolucion de 29
hallazgos) define el modulo de Workflow como un **motor de automatizacion por cadena de
pasos** al estilo HubSpot Workflows / Airtable Automations: disparador -> condicion ->
accion, con cola de eventos asincrona, catalogo de disparadores y acciones, estados
personalizados por tenant, panel de monitoreo y tarea de tercero por el Portal Ciudadano.
La spec **rechaza BPMN explicitamente**:

> "No es un diagrama de procesos: es un motor de automatizacion orientado a resultados."
> "No cubre: Diagramacion BPMN ni canvas de flujos tipo Bizagi."

El proyecto hermano **ECOREX.tareas** trae un motor de **workflow BPMN 2.0** ya construido y
probado: editor canvas con `bpmn-js`, parser/writer de XML BPMN, motor de ejecucion de
instancias con token (historial append-only), compuertas exclusivas auto-resueltas, ciclos
por reinicio y versionamiento inmutable. ~5.657 lineas de Application + UI.

## Decision

Para RQ11 se **porta el motor BPMN de ECOREX** (canvas `bpmn-js` + motor de ejecucion), NO
se construye el motor de cadena de pasos que describe el vault v2.0.

Esta decision la tomo el product owner de forma explicita tras presentarsele la contradiccion
con el vault y una recomendacion en contra (se recomendaba seguir el vault). Se documenta aqui
por el mandato de CLAUDE.md §2: "Si el codigo debe contradecir la spec, se pregunta al usuario,
se escribe un ADR y se avisa para actualizar el vault."

### Alcance del port (slice 1)

- Disenador BPMN con `bpmn-js` (importar/dibujar/publicar/versionar), paleta acotada a 4
  tipos de nodo ejecutables: startEvent, task, exclusiveGateway, endEvent.
- Motor de ejecucion: arrancar instancia, avance en cascada (tope anti-loop de 50),
  compuertas que autorresuelven por `approval == 'X'`, reinicios (RestartNodeId), rechazo con
  reactivacion append-only, cierre implicito al quedarse sin pasos vigentes.
- Asignacion de pasos Task por organigrama (WorkflowNodePolicy -> OrgUnit + resolver de
  candidatos), reusando el organigrama heredado de RQ01.
- Pagina minima de ejecucion para arrancar y atender pasos (prueba e2e).

### Conversiones y podas respecto de ECOREX

- IDs `Guid` -> `long` (convencion de TRONOX; el dominio no usa Guid). La topologia BPMN usa
  `BpmnElementId` (string), independiente del PK numerico: la conversion es limpia.
- Se **elimina** el acople 1:1 al `TaskItem` del modulo Tareas/Kanban (podado en TRONOX):
  ni `ITaskBroadcaster`, ni `TaskItemActivity`, ni maquina de estados de tarea. La instancia
  corre autonoma. La integracion con "Mis Tareas" (RQ10) queda para una ola posterior.
- Se **omiten** los vinculos por nodo de: motor de Reglas (`IWorkflowRuleHook` queda como
  `NoOpWorkflowRuleHook`), formularios y agentes de IA (submodulo ola 2 de ECOREX).

## Consecuencias

### Positivas
- Se reutiliza un motor de ejecucion probado en vez de construir uno desde cero.
- El resultado es un editor visual de procesos potente (BPMN estandar, portable con bpmn.io).

### Negativas / deuda
- **Divergencia con el vault:** hay que actualizar `RQ11_Workflow_Documental_v2.md` para
  reflejar el paradigma BPMN, o marcar la v2.0 como vision futura. Pendiente de avisar al
  equipo funcional.
- El motor BPMN cubre ~40% de lo que pide el vault: quedan FUERA (para olas futuras) los
  disparadores/cola de eventos, el catalogo de acciones de automatizacion, los estados
  personalizados por tenant, el panel de monitoreo con alertas y la tarea de tercero por el
  Portal + constancia DEA. Estos podrian construirse por ENCIMA del motor BPMN mas adelante.
- El bundle `bpmn-modeler.js` (~484 KB) se vendoriza en `wwwroot/lib/bpmnio/` (self-hosted,
  CSP-friendly). bpmn-js no honra el modo oscuro: el canvas se ve siempre en claro.

## Alternativas consideradas

1. **Construir la cadena de pasos del vault (recomendada por el agente).** Alineada con la
   spec y con los modulos ya construidos (Forms/Plantillas/Documentos/Expedientes/Terceros),
   pero implica construir el motor desde cero. Descartada por decision del product owner.
2. **Hibrido (motor BPMN por dentro, cadena por fuera).** Dos modelos de topologia a
   conciliar; mayor complejidad. Descartada.
