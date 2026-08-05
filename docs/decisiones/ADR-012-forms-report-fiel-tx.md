# ADR-012: RQ08 Formularios — re-port fiel de ECOREX sobre tx-*/Velzon

- Estado: Aceptada
- Fecha: 2026-08-05
- Decisores: usuario (product owner) + agente de desarrollo
- Relacionada: RQ08, ADR-011 (JSON-por-respuesta), ADR-010 (Workflow BPMN)

## Contexto

El primer slice de RQ08 (commit 5ba938b) fue un port RECORTADO: UI propia reducida, ~13 tipos
de campo, sin logica condicional/calculados/cascada/lookups/transaccional/bandeja-de-modulo/
tokens. El product owner lo califico de "mediocre": el estandar para los ports desde ECOREX es
MILIMETRICO y COMPLETO, fiel en aspecto (vista tarjetas + tabla) y en funcionalidad.

## Decision

Se REHACE RQ08 como port fiel de ECOREX, con dos decisiones del usuario:

1. **Aspecto: reconstruir sobre tx-*/Velzon**, NO vendorizar el CSS propio de ECOREX. Se
   reproduce el comportamiento y el layout (catalogo tarjetas+tabla, disenador 3-columnas con
   barra de dispositivo/drag-drop/pestanas, renderer de los 28 tipos) con las clases `tx-*` de
   TRONOX + Velzon y `<style>` scoped por pagina. Esto respeta la regla Velzon-only de CLAUDE.md
   (a diferencia del canvas bpmn-js del ADR-010, que si se vendorizo por ser una lib de terceros).
2. **Alcance: modulo completo**, no un slice. Se porta TODO lo autocontenido; se difiere SOLO lo
   bloqueado por modulos ausentes, con acuerdo explicito (no por recorte unilateral).

## Consecuencias

### Positivas
- Fidelidad de aspecto y funcionalidad; el catalogo tarjetas+tabla y el disenador 3-columnas que
  el usuario pidio.
- Sin dependencia del CSS de ECOREX: mantenible dentro del sistema de diseno de TRONOX.

### Diferido (con nota, acordado)
- Lookups de **Tercero** -> hasta RQ07 (Catalogo de Terceros). Item/DataContainer: modulos de
  ECOREX que NO aplican a TRONOX (descartados).
- **Motor de Reglas** (podado) -> la logica condicional se reimplemento AUTOCONTENIDA en
  Formularios (`FormFieldCondition` + `FormConditionEvaluator` puro), sin el motor externo.
- **Visor publico anonimo** `/f/{token}` -> necesita plumbing de "tenant ambiente" para renderizar
  sin sesion (TRONOX resuelve el tenant del JWT). La emision/listado/revocacion de tokens si queda
  funcional para el admin.
- **Binding runtime formulario<->paso de flujo BPMN** (RQ08 x RQ11) -> entidades listas
  (WorkflowNodeForm, FormFlowLink); el completado del paso al enviar es integracion posterior.
- VLOOKUP de columnas de grilla (depende de las fuentes de lookup).

### Deuda menor
- El disenador dispara 404 de `choices.js`/`flatpickr` (auto-init de plugins de Velzon en rutas
  `/assets/libs/`): inocuo (controles nativos), pendiente de silenciar.

## Alternativas consideradas
- **Vendorizar el CSS de ECOREX** (pixel-identico): descartada por el usuario a favor de tx-*.
- **Otra oleada recortada**: descartada; el usuario exige el modulo completo.
