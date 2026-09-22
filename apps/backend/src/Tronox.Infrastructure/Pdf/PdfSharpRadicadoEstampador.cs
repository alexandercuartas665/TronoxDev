using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Estampado del sticker de radicado sobre un PDF con PdfSharpCore (RF02-5). Dibuja un recuadro con borde
/// punteado navy en la posicion elegida por el operador (arrastre del chip en el asistente), con la
/// leyenda "TRONOX - RADICADO", el numero (monospace) y la fecha. Solo la primera pagina (como el legacy).
/// Best-effort: cualquier fallo devuelve el PDF original.
/// </summary>
public sealed class PdfSharpRadicadoEstampador : IRadicadoEstampador
{
    static PdfSharpRadicadoEstampador()
    {
        try { GlobalFontSettings.FontResolver ??= new PdfSharpCore.Utils.FontResolver(); }
        catch { /* ya estaba puesto o falla: cae al try/catch de estampar */ }
    }

    public byte[] EstamparRadicado(byte[] pdf, string numero, string fecha, double xPct, double yPct)
    {
        try
        {
            using var msIn = new MemoryStream(pdf);
            var doc = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
            if (doc.PageCount == 0) { return pdf; }

            var page = doc.Pages[0];
            var pw = page.Width.Point;
            var ph = page.Height.Point;

            // Tamano fijo del sello en puntos (calca el chip del legacy, ~150x46).
            const double w = 150, h = 46;
            var x = Math.Clamp(xPct / 100.0, 0, 1) * pw;
            var y = Math.Clamp(yPct / 100.0, 0, 1) * ph;
            // No dejar que el sello se salga de la pagina.
            x = Math.Min(x, Math.Max(0, pw - w));
            y = Math.Min(y, Math.Max(0, ph - h));

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var navy = XColor.FromArgb(255, 64, 81, 137);   // #405189
            var gris = XColor.FromArgb(255, 148, 163, 184);  // #94A3B8

            // Fondo crema semitransparente + borde punteado navy.
            gfx.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(235, 255, 254, 245)),
                new XRect(x, y, w, h), new XSize(6, 6));
            var pen = new XPen(navy, 1.2) { DashStyle = XDashStyle.Dash };
            gfx.DrawRoundedRectangle(pen, new XRect(x, y, w, h), new XSize(6, 6));

            var fTitulo = new XFont("Arial", 6.5, XFontStyle.Bold);
            var fNumero = new XFont("Courier New", 10, XFontStyle.Bold);
            var fFecha = new XFont("Arial", 6.5, XFontStyle.Regular);
            gfx.DrawString("TRONOX - RADICADO", fTitulo, new XSolidBrush(gris),
                new XRect(x + 8, y + 5, w - 16, 9), XStringFormats.TopLeft);
            gfx.DrawString(numero ?? "", fNumero, new XSolidBrush(navy),
                new XRect(x + 8, y + 16, w - 16, 14), XStringFormats.TopLeft);
            gfx.DrawString(fecha ?? "", fFecha, new XSolidBrush(gris),
                new XRect(x + 8, y + 32, w - 16, 9), XStringFormats.TopLeft);

            using var msOut = new MemoryStream();
            doc.Save(msOut);
            return msOut.ToArray();
        }
        catch
        {
            return pdf;
        }
    }
}
