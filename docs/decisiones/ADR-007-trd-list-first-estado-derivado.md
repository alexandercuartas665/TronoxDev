# ADR-007 - TRD list-first con Estado TRD derivado (sin entidad por dependencia) + RF05 embebido

Fecha: 2026-08-01
Estado: Aceptado
Contexto: RQ02 - RF04 (Construccion de la TRD) y RF05 (Tipologias Documentales)

## Contexto

La pantalla `/trd` (RF04) es el destino del boton "Abrir/Ver TRD" del listado de Versiones de TRD
(RF01). En el sistema legacy VB.NET esa navegacion va a `doc_tablaRetencionDocumental.aspx`, que:

1. Aterriza en una LISTA "Asignacion por Dependencia": un grid con TODAS las dependencias, cada una
   con un chip "Tabla N - Version XXXX", un ESTADO TRD por dependencia (Sin TRD / En Construccion /
   Activa), fecha y usuario creador, y acciones "ver" / "+ crear" / "inactivar".
2. Al pulsar "ver"/"+crear" abre un WORKSPACE embebido (misma pagina) de esa dependencia con el arbol
   de series y modales para tiempos, disposicion, clasificacion, METADATOS DE EXPEDIENTE, TIPOLOGIAS
   documentales y METADATOS DE DOCUMENTO (el legacy cubre RF04 **y** RF05 en la misma pantalla).

En la BD legacy hay una entidad cabecera por dependencia+version: `GEN_TRD` (su `REG` es el "Tabla N"),
con `ESTADO` propio, y sus detalles `GEN_TRD_DETALLE` (series), `GEN_TRD_DETALLE_TIPOLOGIA` y
`GEN_TRD_DETALLE_METADATA`.

La primera migracion de RF04 (commit `7e801f1`) siguio la spec, que modela RF04 en **2 niveles**
(version -> asignaciones Dependencia+Serie), colapsando la cabecera+detalle del legacy en una sola
tabla `trd_asignaciones`, con el estado viviendo en la VERSION (RF01), no por dependencia. La UI
resultante aterrizaba en un arbol plano seleccionable, sin el estado TRD por dependencia ni el flujo
ver/crear, y sin tipologias. El usuario senalo que esa pantalla NO era fiel al `doc_tablaRetencionDocumental`
que espera.

## Decision

**No** se introduce una entidad "TRD por dependencia" (cabecera tipo `GEN_TRD`). Se conserva el
modelo de datos de la spec (estado en la version; `trd_asignaciones` como cruce). En su lugar:

1. **UI list-first.** `/trd` aterriza en la lista "Gestion de Tablas de Retencion Documental ·
   {version}" con una fila por dependencia y acciones Ver / Crear, igual que el legacy. Desde ahi se
   abre el WORKSPACE de la dependencia (cambio de vista en la misma pagina).

2. **Estado TRD por dependencia DERIVADO** (no persistido), calculado en logica pura
   (`TrdConstruccionRules.EstadoDependencia`, enum `EstadoTrdDependencia`):
   - `Sin TRD` si la dependencia no tiene series activas en la version;
   - en otro caso refleja el estado de la version: En Construccion -> "En Construccion",
     Vigente -> "Activa", Historico -> "Historica", Inactivo -> "Inactiva".
   Esto reproduce el badge del legacy sin duplicar el ciclo de vida ni permitir activar/inactivar la
   TRD de una sola dependencia de forma independiente (comportamiento legacy que la spec no exige).

3. **RF05 embebido en la misma pantalla.** El workspace incluye tipologias documentales
   (`trd_tipologias`) y metadatos de documento (contexto Documento, colgados de la tipologia via
   `trd_metadatos.trd_tipologia_id`), como el legacy. El usuario confirmo que este modulo cubre RF05.

## Consecuencias

- La pantalla y el flujo (Abrir TRD -> lista por dependencia -> Ver/Crear -> workspace) quedan fieles
  al legacy sin contradecir la spec ni los 10 invariantes.
- **No hay "Tabla N" literal** (era el id de la cabecera `GEN_TRD`, que no existe aqui). Se muestra un
  chip "TRD {codigo-dependencia}" cuando la dependencia tiene series.
- El estado TRD por dependencia no puede divergir del de la version (es una funcion de esta). Si en el
  futuro se requiere activar/inactivar la TRD de una sola dependencia de forma independiente, habra que
  reabrir esta decision e introducir la entidad cabecera.
- RF05 queda cubierto por este modulo (no es un modulo aparte): entidad `TrdTipologia`, enum
  `SoporteTipologia`, y `trd_metadatos` con `contexto` (Expediente/Documento) y FK opcional a tipologia.

## Alternativas descartadas

- **Entidad "TRD por dependencia" (fidelidad total al legacy):** anade una tabla con estado propio,
  fecha/usuario creador y activar/inactivar por dependencia. Contradice el modelo simple de la spec y
  duplica el ciclo de vida de la version. Se descarto por acuerdo con el usuario (fidelidad de pantalla,
  no de esquema).
- **Dejar `/trd` como estaba (arbol plano):** valido contra la spec pero infiel a la pantalla legacy
  que el usuario espera. Descartado.
