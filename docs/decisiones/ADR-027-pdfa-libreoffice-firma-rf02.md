# ADR-027: Archivado PDF/A-2b de la firma via LibreOffice (RQ05 - RF02) - Fase C.2

- Estado: Aceptada
- Fecha: 2026-09-08
- Contexto: RQ05 (Firma), Fase C (fidelidad/cumplimiento). Segundo item: llevar el PDF sellado a PDF/A-2b
  para el archivado de largo plazo (Decreto 2364/2012, AGN). Requiere infraestructura nueva.

## Contexto

El sellado actual (ADR-017) produce un PDF normal (cajita + SHA-256). Para conformidad archivistica el
legacy convierte a **PDF/A-2b con LibreOffice headless** (`PdfAConverter.vb`, ADR-002 del legacy que
descarto iText/Aspose/Select.HtmlToPdf por licencia/costo). Validacion estricta con veraPDF quedo
diferida en el legacy (usa un chequeo debil de texto `pdfaid:part`).

## Decision

- **Anadir LibreOffice a la imagen de la app** (`libreoffice-writer` + `libreoffice-draw`, ~+300-400 MB;
  draw provee el import de PDF y el export PDF/A). `soffice` queda en `/usr/bin/soffice`; se expone
  `TRONOX_SOFFICE_PATH` como env. Es el unico cambio de infraestructura; NO se despliega Tronox.Workers.
- `IPdfAConverter` / `LibreOfficePdfAConverter` (port de PdfAConverter): invoca
  `soffice --headless --norestore -env:UserInstallation=file://<perfil> --convert-to
  pdf:writer_pdf_Export:{"SelectPdfVersion":{"type":"long","value":"2"}} --outdir <out> <entrada>`,
  perfil temporal por conversion, timeout 120 s, valida `pdfaid:part` (chequeo debil, como el legacy).
- **Enganche en `SellarPdfEnSitioAsync`**: tras estampar la cajita (PdfSharpCore), se convierte el PDF
  sellado a PDF/A-2b y se calcula el SHA-256 sobre el resultado. Orden "estampar -> convertir" (no al
  reves) para GARANTIZAR que la salida sea PDF/A conforme (una segunda pasada de PdfSharp tras el
  PDF/A podria romper la conformidad; eso se resolvera con el sellado XMP byte-range, ver Diferido).
- **BEST-EFFORT**: si `soffice` no esta (p. ej. en local) o la conversion falla/no valida, se conserva
  el PDF sellado sin convertir y la firma NO se frena (la integridad la da el hash igual).

## Alternativas descartadas

- **iText / Aspose / Select.HtmlToPdf**: licencia AGPL / costo / limite de paginas (ya descartadas en el
  ADR-002 del legacy).
- **Convertir original -> PDF/A y luego estampar con PdfSharp**: PdfSharpCore al re-guardar puede quitar
  la conformidad PDF/A (OutputIntent/XMP); se elige estampar-primero-convertir-despues para asegurar la
  salida conforme.
- **Desplegar Tronox.Workers para la conversion**: no se despliega a prod; la conversion corre inline en
  el sellado dentro de la app.

## Consecuencias

- La imagen de prod crece ~300-400 MB y cada firma agrega la latencia de una conversion LibreOffice
  (~pocos segundos, cold start incluido). Aceptable para el volumen de firma.
- LibreOffice re-importa el PDF sellado: para PDFs de texto tipico conserva el texto; PDFs complejos
  pueden rasterizarse (limitacion inherente del enfoque validado con el cliente).
- **Diferido**: (a) sellado XMP `tronox:` + hash length-neutral con byte-range excluido (FirmaSelladoHelper),
  que permite guardar el hash dentro del propio PDF/A sin auto-invalidarlo, y estampar preservando la
  conformidad; (b) validacion estricta veraPDF; (c) hora legal NTP (FirmaConfig.NtpActivo/NtpServidor);
  (d) portal verificador publico. La conversion PDF/A de este ADR es el habilitador de (a) y (b).
