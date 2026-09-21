using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Estampado de la leyenda de impresion (RF05) con PdfSharpCore (fork cross-platform de PdfSharp; el
/// legacy usa PdfSharp/System.Drawing, que no corre en Linux - ver ADR-016). Calca EstamparImpresionPdf:
/// Arial 7.5 bold, rojo semitransparente, al pie centrado de cada pagina (y = alto - 14). Best-effort.
/// </summary>
public sealed class PdfSharpPrintStamper : IPdfPrintStamper
{
    static PdfSharpPrintStamper()
    {
        // PdfSharpCore exige un resolver de fuentes global (en Linux no hay Arial "de sistema" via GDI+).
        // El resolver que trae usa SixLabors.Fonts + fuentes embebidas; resuelve Arial o cae a una fallback.
        try { GlobalFontSettings.FontResolver ??= new PdfSharpCore.Utils.FontResolver(); }
        catch { /* si ya estaba puesto o falla, el estampado cae al try/catch de abajo */ }
    }

    public byte[] EstamparLeyendaPie(byte[] pdf, string leyenda)
    {
        try
        {
            using var msIn = new MemoryStream(pdf);
            var doc = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
            foreach (var page in doc.Pages)
            {
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var font = new XFont("Arial", 7.5, XFontStyle.Bold);
                var brush = new XSolidBrush(XColor.FromArgb(200, 220, 38, 38));
                var y = page.Height.Point - 14;
                gfx.DrawString(leyenda, font, brush,
                    new XRect(0, y, page.Width.Point, 12), XStringFormats.Center);
            }
            using var msOut = new MemoryStream();
            doc.Save(msOut);
            return msOut.ToArray();
        }
        catch
        {
            // Como el legacy: mejor entregar el PDF sin sello que romper la impresion.
            return pdf;
        }
    }
}
