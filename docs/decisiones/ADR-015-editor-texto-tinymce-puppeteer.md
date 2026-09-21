# ADR-015 - Editor de texto interno (RF08): TinyMCE + PuppeteerSharp

Estado: aceptado
Fecha: 2026-08-20

## Contexto

El modulo de documentos (Mis Documentos / RQ04) del legacy VB.NET (`ctrlEditorTexto.ascx`) ofrece un
editor de texto interno (RF08) para redactar el cuerpo de un borrador y luego "Generar PDF". El legacy usa:

- **CKEditor 4.22.1** (por CDN) como editor WYSIWYG, con hoja tamano Carta.
- **SelectPdf** (`Funciones.cHtmltoPDF`) para convertir el HTML a PDF.
- Guarda el HTML crudo en la columna `EXP_DOCUMENTOS.CONTENIDO_HTML`.
- Al "Generar PDF" marca el documento como `Archivado` (Original Electronico) aunque no tenga expediente.

Al portarlo a TRONOX (.NET 10, Blazor Server, contenedor **Linux**, repositorio **publico**) hay tres
incompatibilidades: CKEditor 4 esta EOL; SelectPdf es comercial y solo Windows; y "Archivado sin
expediente" choca con el invariante TRONOX de que Archivado = incorporado a un expediente.

## Decision

1. **Editor: TinyMCE 7 self-hosted** (GPLv2), servido desde `wwwroot/tinymce` (sin CDN, cumple CSP).
   Toolbar equivalente al legacy (Formato/Fuente/Tamano, estilos basicos, colores, listas, sangrias,
   alineaciones, link, imagen, tabla, pantalla completa) y hoja tamano Carta (816x1056px, margenes 1").
   Se carga bajo demanda (solo al abrir el editor) via `wwwroot/js/editor-texto.js`.

2. **Motor HTML->PDF: PuppeteerSharp (Chromium headless)** (Apache-2.0), cross-platform.
   Implementa `IHtmlToPdfConverter` en Infrastructure. El binario de Chromium se resuelve por
   configuracion (`Puppeteer:ExecutablePath` / env `PUPPETEER_EXECUTABLE_PATH`): en el contenedor de
   prod se instala `chromium` via apt y se apunta a `/usr/bin/chromium` (ver Dockerfile); en dev se
   apunta a un Chrome local o se descarga una revision cacheada la primera vez.

3. **"Generar PDF" deja el documento como Borrador CON binario**, no como Archivado. El editor
   convierte el HTML a PDF, lo sube al object storage y marca `tiene_binario=true, formato=pdf`, pero
   conserva `Estado=Borrador`. La incorporacion a un expediente sigue haciendose con el flujo Archivar
   (RF16). Asi se respeta el invariante "Archivado = en expediente". El HTML queda en la nueva columna
   `documentos.contenido_html` para poder reabrir y regenerar.

## Consecuencias

- La imagen Docker crece (~300 MB por Chromium + fuentes). Aceptable: es el estandar FOSS para
  HTML->PDF fiel en Linux y evita dependencias comerciales en un repo publico.
- El PDF no es WYSIWYG exacto respecto al editor (igual que el legacy con SelectPdf); se acota con una
  hoja/CSS comun tamano Carta.
- Divergencia intencional vs legacy en el estado tras "Generar PDF" (Borrador vs Archivado), a favor
  del invariante de aislamiento por expediente. El usuario archiva en un segundo paso.
- Pendiente/diferido: sanitizacion del HTML (el legacy tampoco sanitiza), plantillas (RF10) y nueva
  version desde el editor (RF03).
