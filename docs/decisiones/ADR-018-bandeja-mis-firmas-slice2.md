# ADR-018: Bandeja "Mis Firmas" (RQ05 - RF10) - slice 2

- Estado: Aceptada
- Fecha: 2026-09-07
- Contexto: RQ05 (Firma). Continua el slice 1 (ADR-017, firma directa + contrato estable).

## Contexto

El legacy `Formularios/Modulos/Firma/mis_firmas.aspx` (+`.vb`, 947 lineas) con
`FirmaBandejaRepository.vb` es la bandeja unificada de firma (RF10): 4 KPIs (Pendientes, En
progreso, Firmados, Rechazadas) y 4 vistas (Pendientes / Enviadas / Completadas / Rechazadas), mas
tarjetas de progreso de circuito (RF07), firma masiva por lote (RF09), pista de auditoria y el
stepper OTP embebido (RF08).

El slice 1 (ADR-017) dejo la entidad `Firma` (calca FIR_FIRMAS nucleo), el contrato estable
`IFirmaService` (SolicitarFirma/ConsultarEstadoFirma/CancelarFirma) y la firma directa. Faltaba la
bandeja y el ciclo solicitud->cumplimiento entre usuarios. El nodo de menu `modulo/firmas-mis`
("Mis Firmas") ya estaba sembrado.

## Decision

Portar la bandeja "Mis Firmas" sobre las **firmas individuales** (sin circuitos), reutilizando la
entidad `Firma` y el contrato de RQ05.

### Datos
- Se agregan a `firmas` las columnas de contexto de la solicitud (calca FIR_FIRMAS): `Prioridad`
  (reusa `PrioridadTarea`), `FechaLimite`, `Instrucciones`, `Tag`, `ComentarioRechazo`.
- Se agrega el valor `Rechazado` a `EstadoFirma` (distinto de `Cancelado`: rechaza el firmante con
  comentario; cancela el solicitante). Enum como string -> sin migracion por el valor.
- El backfill de `Prioridad` en filas existentes usa `Media` (un enum-string vacio romperia la
  materializacion EF).

### Backend (`IFirmaService`)
- `ListarBandejaAsync(tab)` - las 4 vistas, fail-closed (solo firmas del usuario como firmante o
  como solicitante segun la vista). Individuales (sin circuito).
- `ContarResumenAsync` - los 4 KPIs (EnProgreso=0: reservado a circuitos RF07, diferido).
- `RechazarSolicitudAsync` - el firmante rechaza una pendiente (comentario obligatorio) -> Rechazado;
  si no quedan pendientes del doc, su dimension vuelve a SinFirma.
- `FirmarSolicitadaAsync` - cumple una solicitud pendiente propia: sella el PDF (mismo motor que la
  firma directa: cajita + hash) y pasa esa fila a Firmado (sin duplicar), el documento a Firmado.
  Sin stepper OTP por ahora (consentimiento simple en la UI).
- `GetFirmantesAsignablesAsync` - selector de firmante por PlatformUserId (espacio de id que usa
  `Firma`; consistente con `firmante_user_id`, a diferencia de DocumentoValidacion que usa
  TenantUser.Id). `SolicitarFirmaAsync` se extendio con prioridad/fecha limite/instrucciones/tag.

### UI
- Pagina `MisFirmas.razor` en `/modulo/firmas-mis` (policy `Perm:modulo/firmas-mis:View`), calcando
  `mf-*`: KPIs + tabs con badge + buscador + grid por vista + acciones Firmar/Rechazar/Ver.
- `SolicitarFirmaModal` cableado en "Solicitar Firma" del menu del documento (RF06): alimenta
  Enviadas (solicitante) y Pendientes (firmante).

## Alcance diferido (slices posteriores)
- Stepper OTP de 5 pasos (RF08) para cumplir con verificacion (`ctrlFirmaStepper`).
- Circuitos multi-firmante secuencial/paralelo (RF07): tablas FIR_CIRCUITOS/FIR_CIRCUITO_FIRMANTES,
  tarjetas de progreso, KPI "En progreso".
- Firma masiva por lote (RF09) y pista de auditoria (modal).
- PDF/A, QR, sellado XMP, hora legal NTP, certificado digital (ver ADR-017).

## Consecuencias
- El usuario ve y opera toda su actividad de firma individual en un solo lugar.
- El ciclo Solicitar (RF06) -> Firmar/Rechazar (RF10) queda cerrado entre usuarios del tenant.
- El contrato `IFirmaService` se amplio (no se rompio ninguna firma existente; invariante 5).
- Fail-closed: cada vista solo muestra lo que pertenece al usuario. Sin eliminacion real (rechazo).
- Pendiente reflejar en el vault Obsidian el avance del slice 2 de RQ05.
