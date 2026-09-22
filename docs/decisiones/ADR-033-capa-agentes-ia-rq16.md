# ADR-033: Capa de Agentes de IA (RQ16) portada de ECOREX

Fecha: 2026-09-22
Estado: Aceptada

## Contexto

TRONOX heredo del backbone (ECOREX.tareas) el **gateway multi-proveedor de IA**
(`IAiProviderClient` con `CompleteAsync`/`CompleteWithToolsAsync`/`CompleteVisionAsync`,
`AiProviderConfig`, `AiProviderCatalog`, `IAiUsageService` con cuota por plan, pagina
`/servidores-ia`). En su momento se decidio NO heredar el "motor conversacional" (agentes,
tool-cache, sesiones); la nota quedo escrita en `AiGatewayContracts.cs`:

> el motor conversacional de TRONOX (agentes, tool-cache, sesiones) NO se hereda.

El primer consumidor del gateway fue el clasificador de **Correos -> PQR** (RQ16), con un
**prompt PQRS embebido en codigo** (`CorreoClasificadorIa.PromptClasificador`).

El usuario pidio traer la **capa de Agentes** completa de ECOREX para tener agentes
configurables por tenant (proveedor, modelo, **prompt editable**, herramientas) y **migrar el
clasificador de Correos -> PQR a un agente editable**.

## Decision

1. **Se revierte** la nota "los agentes NO se heredan": se porta la capa de agentes de ECOREX.
2. El port es **fiel al nucleo** pero **omite** las piezas atadas a modulos podados en TRONOX
   (WhatsApp/lineas, Retell voz, Colmena RPA, agentes de nodo BPMN, reacciones, cierre y
   reactivacion). La invocacion se **cablea al canal que TRONOX si tiene** (Correos/Radicacion)
   en vez de WhatsApp.
3. Conversion de convencion: `Guid` -> `long` en todas las entidades/FK; todo `ITenantScoped`.
4. Se agrega un **toolset propio del SGDEA** (`RadicacionToolset`) con `buscar_tercero` (DAT-02)
   y `radicar_entrada` (crea radicado via `IRadicadorService`), para que un agente pueda OPERAR
   sobre el dominio documental.
5. **Correos -> PQR**: el `BuzonCorreo` gana `AgenteIaId` (FK opcional a `AiAgent`). El
   clasificador usa el proveedor/modelo/**comportamiento** del agente cuando el buzon lo apunta;
   si no, cae al primer proveedor habilitado con el comportamiento por defecto. El **contrato de
   salida JSON PQRS** se anexa SIEMPRE (fijo del sistema) para que la respuesta sea parseable sin
   importar como el tenant edite el prompt del agente.

## Piezas nuevas

- Domain: `AiAgent`, `AiAgentResource`, `AiAgentPrompt`, `AiAgentCacheField`,
  `AiAgentCacheValue`, `AiAgentRunLog` + enum `AiAgentRunLogKind`. Migracion `AddAiAgents`.
- Application: `AiAgentService`, `AiAgentCacheService`, `AiInferenceService` (prompt builder +
  tool loop + extraccion de cache), `IAgentToolset`/`AgentToolResult`, `RadicacionToolset`.
- Web: pagina `/modulo/agentes` (CRUD + chat de prueba con panel de prompts) e item de menu.
- Correos -> PQR: `BuzonCorreo.AgenteIaId` (migracion `AddBuzonAgenteIa`), selector de agente en
  el editor del buzon, clasificador refactorizado (comportamiento editable + contrato fijo).

## Consecuencias

- Un tenant puede crear varios agentes y editar su prompt sin tocar codigo.
- El interruptor por-agente es `AiAgent.IsActive`; el de proveedor, `AiProviderConfig.IsEnabled`;
  la cuota por plan sigue vigente (`IAiUsageService.GetQuotaAsync`). Queda pendiente el
  interruptor maestro por tenant `tenants.ia_habilitada` (DAT-07), que ECOREX tampoco tenia.
- Deuda diferida (no aplica a un SGDEA hoy): entrega multimedia de recursos ([[enviar]]) end to
  end, vision inline en el tool loop, y las piezas de WhatsApp/voz/BPMN.
- Debe reflejarse en el vault (Observaciones Tecnicas / MAPA_MENU): nuevo modulo `/modulo/agentes`.
