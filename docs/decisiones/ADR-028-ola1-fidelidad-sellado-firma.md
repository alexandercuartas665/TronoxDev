# ADR-028: Ola 1 - Fidelidad de sellado de firma (NTP, QR, XMP byte-range, verificador) (RQ05)

- Estado: Aceptada
- Fecha: 2026-09-21
- Contexto: RQ05 (Firma), "Ola 1" del plan por olas. Completa el cumplimiento PAdES/Decreto 2364 sobre el
  PDF/A ya desplegado (ADR-027). Cuatro piezas.

## Decisiones

### 1. Hora legal NTP (RF02) - port de NtpHelper
- `INtpTimeProvider` / `NtpTimeProvider`: SNTP UDP/123 (RFC 2030, epoca 1900 big-endian), cache del offset
  10 min por servidor, timeout 3 s. Se usa en `SellarPdfEnSitioAsync` para el timestamp del sellado cuando
  `FirmaConfig.NtpActivo` (por defecto **false** -> sin cambio de comportamiento). Best-effort: si el
  servidor NTP no responde/no resuelve, se sella con la hora del servidor (UtcNow). Verificado el parseo
  contra pool.ntp.org (hora correcta).

### 2. QR de verificacion (RF03)
- QRCoder (1.6.0, `PngByteQRCode`, ECC nivel M como el legacy). Se dibuja en el **certificado/acta**
  (QuestPdfActaRenderer), codificando `https://verificar.tronox.co/v/{docId}`. Se ubica en el acta (que se
  regenera on-demand) y NO en la cajita del documento, para no estampar un QR a un portal publico en PDFs
  permanentes antes de que exista; una vez estable, puede agregarse tambien a la cajita.

### 3. Sellado XMP length-neutral con hash byte-range (RF02/RF03) - port de FirmaSelladoHelper
- `IPdfXmpSealer` / `PdfXmpSealer`: sobre el PDF/A (que trae xpacket + padding), inserta un bloque
  `tronox:` dentro del xpacket **consumiendo el padding whitespace** (tamano y offsets del PDF intactos) y
  calcula un SHA-256 que **excluye todo el paquete XMP** (`<?xpacket begin ... ?>`), guardandolo en un
  placeholder de 64 ceros dentro del propio XMP. Asi el hash no se auto-invalida (patron PAdES).
  `RecalcularHash` reproduce el calculo para el verificador. Enganche en `SellarPdfEnSitioAsync` tras la
  conversion PDF/A. Best-effort: sin xpacket/padding suficiente, se usa el SHA-256 del PDF/A completo.
- Todo el trabajo es en espacio de BYTES (marcadores por ISO-8859-1 = 1 char/byte; bloque insertado como
  bytes UTF-8) para preservar la neutralidad de tamano. Verificado sobre un PDF/A real de LibreOffice:
  neutral, pdfaid intacto, hash sellado == recalculado, reabre en soffice.

### 4. Portal verificador publico (RF04)
- Pagina publica `/v/{docId}` (`@attribute [AllowAnonymous]`, `@layout EmptyLayout`), la URL que estampan
  el QR y el XMP. `IVerificacionFirmaService` resuelve el documento por id **cross-tenant**
  (IgnoreQueryFilters), sin sesion, y muestra solo datos probatorios: entidad, estado, hash SHA-256
  registrado y firmantes. **Nunca** expone el binario ni el contenido. Alcanzable por el Caddy externo que
  ya proxya el dominio al app (igual que el portal ciudadano); NO se toco Caddy.

## Consecuencias

- Sin migracion. Paquete nuevo: QRCoder.
- El dominio vanidoso `verificar.tronox.co` requiere DNS + una entrada de Caddy que apunte al app (tarea de
  infra aparte); mientras tanto el verificador funciona en el dominio publico actual del app en `/v/{id}`.
- Diferido: veraPDF estricto (validacion de conformidad; el legacy tambien lo difirio, hoy chequeo debil
  pdfaid:part); agregar el QR tambien a la cajita del documento; mostrar/ocultar nombre por clasificacion
  en el verificador si se requiere mayor privacidad.
