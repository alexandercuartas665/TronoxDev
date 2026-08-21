# ADR-016 - Estampado de la copia de impresion (RF05): PdfSharpCore

Estado: aceptado
Fecha: 2026-08-21

## Contexto

El legacy (`doc_visor.ashx.vb`, `EstamparImpresionPdf`) estampa la leyenda "COPIA NO CONTROLADA -
Impreso por: <nombre> - Fecha: dd/MM/yyyy HH:mm - TRONOX" al pie centrado de cada pagina de un PDF
existente cuando se imprime (`print=1`), usando **PdfSharp** (`PdfReader.Open` + `XGraphics.FromPdfPage`
+ `gfx.DrawString`), Arial 7.5 bold, rojo semitransparente ARGB(200,220,38,38), y audita "Impresion".

PdfSharp clasico depende de **System.Drawing.Common (GDI+)**, que **no esta soportado en Linux** en
.NET 6+. TRONOX corre en contenedor Linux. Ademas, los otros motores PDF que ya tiene TRONOX no sirven
para esto: PuppeteerSharp/Chromium renderiza HTML->PDF (no superpone sobre un PDF existente) y QuestPDF
genera PDFs desde cero (no edita uno existente).

## Decision

Usar **PdfSharpCore** (fork cross-platform de PdfSharp, sobre SixLabors.Fonts/ImageSharp, sin
System.Drawing) para abrir el PDF existente y estampar la leyenda al pie de cada pagina. La API es la
misma que el legacy (`PdfReader.Open`, `XGraphics.FromPdfPage(Append)`, `XFont`, `DrawString`), lo que
permite un port casi 1:1 de `EstamparImpresionPdf`.

- Abstraccion `IPdfPrintStamper.EstamparLeyendaPie(pdf, leyenda)` en Application; implementacion
  `PdfSharpPrintStamper` en Infrastructure.
- PdfSharpCore exige un `GlobalFontSettings.FontResolver`; se registra el resolver por defecto que
  trae (SixLabors + fuentes embebidas) una sola vez.
- Best-effort (como el legacy): si el PDF no se puede estampar, se devuelve el original sin sello
  antes que romper la impresion.
- El endpoint `/visor/print?doc=N` descarga el binario (respeta acceso/binario), estampa (solo PDF),
  audita "documento.imprimir" y entrega el PDF inline en pestana nueva (el usuario imprime con Ctrl+P).

## Consecuencias

- Se agrega una libreria PDF mas (PdfSharpCore), junto a QuestPDF (comprobantes) y PuppeteerSharp
  (HTML->PDF). Cada una cubre un caso distinto: generar-desde-cero, HTML->PDF, y editar/estampar un
  PDF existente. Es la unica de las tres capaz de superponer sobre un PDF ya hecho.
- Pendiente/diferido: estampado de la leyenda sobre imagenes (jpg/png) — el legacy tambien lo hace
  (`EstamparImpresionImagen`), pero requiere dibujo de imagen (ImageSharp); hoy la impresion de
  imagenes entrega el binario sin sello. Marca de agua "COPIA" diagonal (RF17) y de seguridad
  (Reservado/Clasificado) tambien quedan para una fase posterior del visor.
