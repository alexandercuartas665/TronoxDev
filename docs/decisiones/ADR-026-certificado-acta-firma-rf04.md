# ADR-026: Certificado / acta de firma en PDF (RQ05 - RF04) - Fase C.1

- Estado: Aceptada
- Fecha: 2026-09-08
- Contexto: RQ05 (Firma), Fase C (fidelidad/cumplimiento). Primer item de la fase, elegido por ser el de
  mayor valor SIN cambio de infraestructura.

## Contexto

Legacy `fir_certificado.aspx`: "informe de auditoria" estilo Adobe Acrobat Sign. Lee el documento +
`FirmaAuditoriaRepository.ListarPorDocumento` (ledger FIR_AUDITORIA) y muestra un resumen (fecha de
creacion, creador, estado de firma, **ID de transaccion**, **hash SHA-256**) + el historial de eventos
con timestamp NTP e IP; "Documento completado" si esta Firmado. El PDF real lo producia el **navegador**
via `window.print()` (no habia generacion server-side).

## Decision

Portar el acta como generacion **server-side con QuestPDF** (la herramienta PDF de la casa; no depende
de un navegador headless), leyendo el ledger append-only de TRONOX (`super_admin_audit_logs`, el mismo
que sirve la pista RF12).

- `IFirmaService.GenerarActaAsync(docId, actor)`: carga el documento (fail-closed por no-historica),
  lee del ledger los eventos de firma del documento (mismas acciones que la pista: `EntityId == docId`
  + acciones de firma), resuelve nombres de actores, y arma `ActaFirmaDto` (resumen + eventos).
- **ID de transaccion** determinista estilo Adobe (calca el legacy): `"TRX" + base64-alfanumerico de
  SHA256(tenant|docId|createdAt)` recortado a 30 mayus.
- `IActaFirmaRenderer` / `QuestPdfActaRenderer` (Infrastructure): dibuja el acta (cabecera, caja de
  resumen, historial de eventos con fecha GMT/actor/IP/detalle, "Documento completado", nota legal).
- Endpoint `GET /visor/certificado?doc=N` (RequireAuthorization TenantMember): devuelve el PDF inline.
  Boton "Certificado / acta de firma" (icono fa-certificate) en la barra del visor.

## Alternativas descartadas

- **HTML + window.print() como el legacy:** no genera PDF server-side y depende del navegador; QuestPDF
  ya esta en el repo (comprobantes, rotulos) y produce el PDF sin browser.
- **Leer un ledger propio de firma:** se reutiliza `super_admin_audit_logs` (RNF-04), fuente unica de la
  pista de auditoria de firma (ADR-022).

## Consecuencias

- Sin migracion ni infraestructura nueva. Verificado: harness de render (layout fiel) + e2e del endpoint
  (`/visor/certificado?doc=106` -> 200 PDF con datos reales del ledger, sin errores).
- El acta refleja lo que el ledger tiene; los eventos de grano fino (Documento_Abierto/Scroll/OTP_*) no
  se registran hoy (diferido de RF12), asi que el historial muestra los eventos de alto nivel existentes.
- El campo "Verificacion" imprime `verificar.tronox.co/v/{docId}`; el portal publico que resuelve esa URL
  es un item aparte de Fase C (2.12, requiere exposicion via Caddy), aun pendiente.
