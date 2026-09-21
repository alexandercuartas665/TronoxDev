# ADR-021: Firma masiva por lote (RQ05 - RF09) - slice 5

- Estado: Aceptada
- Fecha: 2026-09-07
- Contexto: RQ05 (Firma). Continua slices 1-4 (ADR-017..020).

## Contexto

`FirmaMasivaProcesador.vb` (RF09) firma SECUENCIALMENTE un lote de documentos pendientes del usuario,
reutilizando el mismo motor de sellado que la firma directa/solicitada. Criterios: un solo OTP cubre
todo el lote (por LOTE_ID), consentimiento agregado, resumen N firmados / N con error, los que fallan
quedan Pendientes para reintento individual, y auditoria del lote.

## Decision

Portar la firma masiva reutilizando `CumplirFirmaAsync` (el motor compartido de slices 1-4).

### Datos
- `FirmaOtp.LoteId` (nullable): un OTP con LoteId cubre todo el lote (RF09 criterio 3). El OTP
  individual sigue con LoteId null. Migracion `FirmaOtpLote`.

### Backend (`IFirmaService`)
- `LoteRequiereOtpAsync(firmaIds)`: true si alguna firma seleccionada exige OTP.
- `GenerarOtpLoteAsync(firmaIds)`: valida que sean Pendientes del actor, genera UN codigo, lo guarda
  (LoteId + hash) y lo envia por correo (best-effort; revela el codigo si no hay SMTP). Devuelve LoteId.
- `FirmarLoteAsync(firmaIds, loteId, codigo)`: si el lote requiere OTP valida el codigo del lote UNA
  vez; luego firma cada solicitud secuencialmente via `CumplirFirmaAsync` (mismo sellado; avanza
  circuitos si aplica). Acumula resultado por documento; los que fallan quedan Pendientes. Devuelve
  el resumen (Total/Exitosos/Fallidos + detalle).

### UI (bandeja Mis Firmas, pestana Pendientes)
- Checkboxes por fila + "seleccionar todo" en la barra de lote (`mf-lotebar`).
- Modal "Firmar en lote": consentimiento agregado + (si aplica) el OTP unico del lote; al confirmar
  muestra el resumen por documento (ok / error con motivo).

## Alcance diferido
- Auditoria unica del lote con LOTE_ID (por ahora cada documento audita su firma individualmente en
  `CumplirFirmaAsync`; el detalle por-doc del lote no se agrega en un solo evento).
- Procesamiento en segundo plano (RNF-09): aqui es sincrono en la peticion.

## Consecuencias
- El usuario firma varios pendientes en un acto, con un solo OTP y consentimiento, viendo el resultado
  por documento; los que fallan (p.ej. binario no disponible) quedan Pendientes para reintento.
- Verificado e2e: lote de 4 seleccionados -> 2 firmados (blob OK) + 2 con error "binario no
  disponible" que quedaron Pendientes; resumen y estados correctos.
- Contrato `IFirmaService` ampliado sin romper firmas existentes (invariante 5).
- Prod: requiere aplicar la migracion `FirmaOtpLote` en el proximo deploy (aditiva, la app la
  auto-aplica al arrancar).
