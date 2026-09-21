# ADR-008 - Tipo de dato "Moneda" anadido al catalogo de metadatos

Fecha: 2026-08-04
Estado: Aceptado
Contexto: RQ02 - RF04 (metadatos de expediente) y RF05 (metadatos de documento)

## Contexto

La spec RQ02 - RF04 3.4.1 (paso 6) define seis tipos de dato para un metadato: Texto Corto,
Texto Largo, Numerico, Fecha, Lista, Booleano. El enum `TipoDatoMetadato` se construyo con esos seis.

Al hacer la prueba de compatibilidad importando la TRD real del sistema legacy (VB.NET, entidad
`00132`) se encontro un metadato con tipo de dato **"Moneda"** (`GEN_TRD_DETALLE_METADATA.TIPO_DATO`),
que el legacy distingue explicitamente de "Numerico". El nuevo enum no lo tenia, asi que en la
primera importacion se mapeo `Moneda -> Numerico`, perdiendo el matiz (formato de moneda, separador
de miles, simbolo).

## Decision

Se anade el valor **`Moneda`** al enum `TipoDatoMetadato` (valor 6), como septimo tipo de dato de
metadato. Se persiste como texto (igual que el resto) y se etiqueta "Moneda" en la UI.

En la migracion desde el legacy, `TIPO_DATO='Moneda'` mapea a `Moneda` (ya no a Numerico).

## Consecuencias

- Contradice la lista de seis tipos de la spec RF04 3.4.1. Se registra aqui y se avisa para
  reflejarlo en el vault (la spec pasa a listar siete tipos).
- El comportamiento de captura del valor Moneda (input con formato de moneda) se implementa donde se
  diligencian los metadatos (modulo de Expedientes, RQ03, aun no construido). En RF04/RF05 solo se
  DEFINE el metadato, asi que por ahora basta el valor de enum + la etiqueta.
- La migracion de datos legacy conserva la semantica "Moneda" en vez de degradarla a Numerico.

## Alternativas descartadas

- **Mantener el mapeo `Moneda -> Numerico`** (no tocar el enum): cero cambio de codigo, pero se
  pierde informacion del dato legacy y cualquier validacion/formateo monetario futuro. Descartado por
  ser un tipo real que el cliente usa.
- **Modelar "Moneda" como un Numerico con un flag de formato:** mas complejo que un valor de enum
  para un beneficio marginal. Descartado.
