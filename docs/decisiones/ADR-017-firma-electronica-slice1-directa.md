# ADR-017: Firma electronica (RQ05) - slice 1: firma directa + contrato estable

- Estado: Aceptada
- Fecha: 2026-08-21
- Contexto: RQ04 (Documentos), integra con RQ05 (Firma)

## Contexto

El modulo legacy `Formularios/Modulos/Firma` es RQ05 completo: ~20 clases de repositorio, el
control `ctrlFirmaStepper` (Experiencia del Firmante de 5 pasos: Lectura con gating de paginas ->
Identidad -> Consentimiento -> Verificacion OTP -> Resultado, mas sub-paso Rechazo), OTP,
sellado PDF/A + cajita QR + XMP, hora legal NTP (RF12), firma masiva, plantillas de firmante,
alertas, auditoria, grafo de firma y la bandeja `mis_firmas.aspx`.

Portarlo entero excede el alcance de un slice del modulo de Documentos. Pero el menu "Firmar
electronicamente" de un documento **Terminado** necesitaba una accion real (no un placeholder), y
otros modulos dependen del **contrato estable de firma** (invariante 5).

El propio legacy separa dos caminos:
- **Firma directa** (`FirmaDirectaHelper`): el usuario logueado firma su propio documento recien
  terminado. **SIN el stepper, sin leer/visor, sin consentimiento largo y SIN OTP** (ya esta
  autenticado en su sesion). Sella el PDF y lo deja Terminado + Firmado.
- **Solicitud a otro** (`FirmaRepository.RegistrarSolicitud`): fila `Pendiente`; el firmante la
  cumple con el stepper de 5 pasos (con OTP).

## Decision

Implementar en TRONOX el **slice 1 de RQ05**: la **firma directa** como accion del menu "Firmar
electronicamente" (documento Terminado, propio, PDF), mas el **contrato estable de RQ05**:

- `IFirmaService.SolicitarFirmaAsync` (solicitarFirma) - registra la solicitud `Pendiente`.
- `IFirmaService.ConsultarEstadoFirmaAsync` (consultarEstadoFirma) - dimension + firmas del doc.
- `IFirmaService.CancelarFirmaAsync` (cancelarFirma) - cancela una solicitud pendiente.
- `IFirmaService.FirmarDirectoAsync` - la firma directa (auto-firma) del slice 1.

Estas firmas **no cambian** (invariante 5); si falta algo, se agrega.

### Datos y flujo del slice 1

- Entidad `Firma` (tabla `firmas`), calca `FIR_FIRMAS`: documento, firmante (PlatformUserId),
  snapshot (nombre/cargo/dependencia), tipo (`Electronica`/`DigitalCertificada`), estado
  (`Pendiente`/`Firmado`/`Cancelado`), hash, timestamp, ip, sesion, otp_requerido, solicitado_por.
  Enums como string (sin migracion al agregar valores). TENANT-SCOPED. Cascade con el documento.
- La firma es **dimension independiente** del archivado (`EXP_DOCUMENTOS.ESTADO_FIRMA` ->
  `Documento.EstadoFirma`): un documento se puede archivar sin firmar.
- `FirmarDirectoAsync`: descarga el PDF -> estampa la **cajita visual** (`IPdfSignatureStamper`,
  PdfSharpCore: nombre/cargo/dependencia/fecha + leyenda + URL de verificacion) -> calcula
  **hash SHA-256** del PDF sellado -> lo sube a una key nueva -> sella en sitio (sin versionar):
  el documento apunta al PDF firmado, queda `EstadoFirma=Firmado` -> registra la fila `Firmado` +
  auditoria. Borra el binario anterior best-effort. Requiere **consentimiento explicito** del
  firmante en la UI (checkbox, RF08).
- UI: modal `FirmarModal` (identidad + consentimiento) desde el menu del documento Terminado. Tras
  firmar, el badge de firma pasa a "Firmado".

## Alcance diferido (modulo RQ05 completo)

- **Stepper de 5 pasos** (Lectura con gating / Identidad / Consentimiento / OTP / Resultado) que
  **cumple una solicitud** asignada a otro firmante, y la bandeja `mis_firmas`.
- **OTP** (correo/SMS) y presets de verificacion.
- **PDF/A** (conversion), **QR** en la cajita y **sellado XMP** (metadatos de integridad embebidos).
- **Hora legal NTP** (RF12): el slice 1 usa `DateTimeOffset.UtcNow` del servidor.
- **Firma digital certificada** (certificado), **firma masiva**, **plantillas de firmante**,
  **grafo de firma**, **alertas** y **cargo/dependencia** del snapshot (organigrama).

## Consecuencias

- El menu "Firmar electronicamente" ya ejecuta una firma real y trazable sobre el PDF.
- El contrato `IFirmaService` queda fijado para que RQ11/flujos y el modulo RQ05 completo construyan
  encima sin romper llamadas.
- Un documento firmado directamente queda sellado en sitio (v1 oficial) sin versionar, como el legacy.
- Pendiente de reflejar en el vault Obsidian que RQ05 arranca por la firma directa (slice 1).
