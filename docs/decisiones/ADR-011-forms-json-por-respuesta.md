# ADR-011: RQ08 Formularios — respuesta como documento JSON (no EAV por fila)

- Estado: Aceptada
- Fecha: 2026-08-04
- Decisores: usuario (product owner) + agente de desarrollo
- Relacionada: RQ08 (Motor de Formularios Dinamicos), port de ECOREX (ADR-0015 de ECOREX)

## Contexto

El Motor de Formularios Dinamicos (RQ08) se porto del proyecto hermano **ECOREX.tareas**. La
spec del vault sugiere un modelo de metadatos EAV (una fila por par campo-valor), coherente con
el motor de metadatos de RQ02 (DAT-04). ECOREX, en cambio, persiste **cada respuesta completa
como un unico documento JSON** (un `jsonb` con el diccionario `fieldCode -> valor`), decision
tomada en su ADR-0015.

## Decision

La respuesta de un formulario (`FormResponse.Data`) se guarda como **documento JSON** (`jsonb`
en PostgreSQL), NO como filas EAV. La definicion del formulario si es relacional
(`FormDefinition` -> `FormContainer` -> `FormQuestion`), pero los VALORES capturados viven en un
solo campo JSON por respuesta.

## Consecuencias

### Positivas
- Se reutiliza el nucleo de ECOREX tal cual (menos superficie de port, menos riesgo).
- Lectura/escritura de una respuesta completa en una sola fila; sin JOIN por campo.
- El esquema de la definicion puede evolucionar sin migrar filas de valores.

### Negativas / deuda
- **Divergencia con el EAV del vault (DAT-04):** las respuestas de formulario NO son
  consultables campo-a-campo con SQL relacional como los metadatos de expediente. Si un
  requerimiento futuro exige filtrar/reportar por valor de campo a escala, habra que indexar el
  `jsonb` (GIN) o proyectar a una tabla de lectura.
- El motor de metadatos de RQ02 (EAV) y el de formularios (JSON) coexisten con modelos
  distintos: son sistemas separados a proposito (metadatos de archivo vs captura de formularios),
  pero conviene tenerlo presente para no mezclarlos.
- **Pendiente:** reflejar esta divergencia en el vault (RQ08) para que la especificacion y el
  codigo no queden en contradiccion.

## Alternativas consideradas

1. **EAV por fila (como el vault / DAT-04).** Consultable relacionalmente, pero implicaba
   reescribir el nucleo de captura de ECOREX y mayor complejidad de lectura. Descartada para
   este slice.
2. **JSON documento (elegida).** Fidelidad al port, simplicidad; se acepta la deuda de
   consultabilidad.
