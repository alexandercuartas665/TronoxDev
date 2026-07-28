---
type: ADR
status: accepted
date: 2026-07-28
---
# ADR-006: El "modo de codigo de serie" del legacy no se incorpora a RQ02

## Estatus

**Aceptado.** Decision tomada al construir RF04 (Construccion de la TRD), tras analizar el legacy
`doc_versionesTRD` / `doc_tablaRetencionDocumental`.

## Contexto

El sistema legacy guardaba en la version de TRD (`GEN_TRD_VERSION`) una columna
`MODO_CODIGO_SERIE` con dos valores: `CALCULAR CODIGO` (default) y `EDITAR CODIGO`. Al migrar RF01
(Versiones de TRD) surgio la pregunta de si esa columna debia vivir en la entidad `TrdVersion`.

Una lectura superficial sugeria que gobernaba el **codigo CCD** del cruce Dependencia+Serie (RF04).
El analisis detallado del code-behind legacy mostro que NO: `MODO_CODIGO_SERIE` gobierna como se fija
el **codigo de la propia version** al publicarla:

- `CALCULAR CODIGO`: el sistema autogenera `CODIGO_VERSION` con formato `TRD-<anio>-v<consecutivo>`
  (campo de solo lectura).
- `EDITAR CODIGO`: el usuario escribe manualmente el `CODIGO_VERSION`.

El codigo **CCD** del detalle, en cambio, SIEMPRE se compone automaticamente
(`dependenciaCodigo + "." + serieCodigo`) y no depende de esta columna.

## Decision

**No se incorpora `modo_codigo_serie` a TRONOX.** Razones:

1. **La spec (fuente de verdad) ya lo resuelve de otra forma.** RF01 3.1.1 define `codigo_version`
   como un campo de texto (50) **obligatorio, manual y unico por tenant**. No contempla el toggle
   calcular/editar: el codigo lo pone siempre el administrador. El toggle del legacy es una
   comodidad de UX que la spec deliberadamente simplifico.
2. **No afecta al CCD.** El CCD es siempre automatico y no editable (RF04 3.4.4-2), asi que no hay
   nada que "el modo" tenga que gobernar en la construccion de la TRD.
3. **Menos superficie, menos ambiguedad.** Anadir una columna que solo cambia si el codigo de
   version se autogenera o se escribe a mano, cuando la spec ya obliga a escribirlo, complica RF01
   sin aportar valor.

Consecuencia: la entidad `TrdVersion` NO lleva `modo_codigo_serie`; `codigo_version` se captura
siempre manualmente (RF01). Si en el futuro se quiere autogenerar el codigo de version, se abrira
un ADR nuevo y se hara como un helper opcional de UI, no como estado persistido de la version.

## Alternativas consideradas

- **Incorporarlo por paridad con el legacy.** Rechazada: contradice la spec y agrega estado que no
  gobierna nada en el modelo nuevo (el CCD ya es automatico).
- **Autogenerar el codigo de version siempre.** Rechazada: la spec pide codigo manual y unico; la
  autogeneracion seria una mejora futura, no el comportamiento base.

## Impacto sobre el vault

Ninguna correccion de spec: RF01 ya define el codigo manual. Esta decision documenta por que una
columna del legacy no viajo a TRONOX, para que nadie la eche en falta al comparar contra el sistema
anterior.
