# ADR-022: Pista de auditoria de firma (RQ05 - RF12) - slice 6 (cierre RQ05)

- Estado: Aceptada
- Fecha: 2026-09-07
- Contexto: RQ05 (Firma). Cierra el modulo (slices 1-5 = ADR-017..021).

## Contexto

`FirmaAuditoriaRepository.vb` (RF12) lee FIR_AUDITORIA, un ledger append-only (SQL Server 2022 ledger
table: el motor rechaza UPDATE/DELETE). Registra los eventos del proceso de firma (Documento_Abierto,
Consentimiento, OTP_*, Firma_Ejecutada, Firma_Rechazada, Circuito_*, Firma_Masiva, ...) y los muestra
en el modal "Pista de auditoria" de Mis Firmas.

TRONOX ya tiene un ledger append-only de plataforma (`super_admin_audit_logs`, RNF-04) donde el
FirmaService ya venia registrando cada accion de firma (documento.firmar, solicitar_firma,
rechazar_firma, firmar_solicitada, circuito_crear, cancelar_firma).

## Decision

**No duplicar el ledger**: servir la pista desde `super_admin_audit_logs`, filtrando las acciones de
firma. Respeta el invariante de auditoria unica append-only (RNF-04).

### Backend (`IFirmaService`)
- Se agrega la auditoria unica del lote `documento.firmar_lote` en FirmarLoteAsync (estaba diferida en
  ADR-021): un evento con el resumen del lote.
- `ListarPistaAuditoriaAsync(actor, tope)`: lee el ledger por tenant, filtra el set de acciones de
  firma, resuelve el nombre del actor (PlatformUsers) y mapea cada accion a una etiqueta amigable
  (Firma directa / Firma ejecutada / Solicitud de firma / Firma rechazada / Solicitud cancelada /
  Circuito creado / Firma masiva). Mas recientes primero. Solo lectura.

### UI
- Boton "Pista de auditoria" en el header de Mis Firmas -> modal `mf-ev-*` (timeline calcado del
  legacy): fecha, evento, actor, detalle (JSON del asiento) e IP.

## Alcance diferido
- Eventos de grano fino que el legacy audita y TRONOX aun no emite: Documento_Abierto/Scroll_Completo
  (lectura en el visor), OTP_Enviado/OTP_Verificado (el OTP se valida pero no se audita por separado),
  Config_Modificada. Se agregan cuando se necesiten; la pista los mostrara automaticamente.
- Filtros de la pista (evento/usuario/fechas) y resolucion del nombre del documento por asiento (el
  EntityId del asiento varia entre Documento/Firma/Circuito). La vista actual muestra actor+evento+detalle.
- Hora legal NTP en el timestamp (se usa CreatedAt del asiento).

## Consecuencias
- La actividad de firma queda trazada en un ledger append-only unico (no hay tabla paralela) y visible
  en Mis Firmas, cumpliendo RF12 y RNF-04.
- Verificado e2e: 12 eventos (firma directa/ejecutada, rechazada, circuito creado, firma en lote) con
  actor, fecha y detalle correctos, mas recientes primero.
- **RQ05 (Firma) cerrado**: firma directa, bandeja, solicitar (RF06), stepper OTP (RF08), circuitos
  (RF07), firma masiva (RF09) y pista de auditoria (RF12).
- Sin migracion (solo lectura del ledger existente): el deploy a prod seria un image swap puro.
