using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Cajita visual de firma (RQ05 - RF03-B) con PdfSharpCore. Slice 1: recuadro de texto al pie derecho de
/// la ultima pagina con nombre/cargo/dependencia/fecha + leyenda "Firmado electronicamente - TRONOX" y la
/// URL de verificacion. QR + PDF/A + sellado XMP quedan para el modulo RQ05 completo (ver ADR-017).
/// Best-effort: ante cualquier error devuelve el PDF original (la firma no se frena por la cajita).
/// </summary>
public sealed class PdfSharpSignatureStamper : IPdfSignatureStamper
{
    static PdfSharpSignatureStamper()
    {
        try { GlobalFontSettings.FontResolver ??= new PdfSharpCore.Utils.FontResolver(); }
        catch { /* ya estaba puesto o falla; el estampado cae al try/catch de abajo */ }
    }

    public byte[] EstamparCajita(byte[] pdf, CajitaFirma d)
    {
        try
        {
            using var msIn = new MemoryStream(pdf);
            var doc = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
            var page = doc.Pages[doc.Pages.Count - 1];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            const double w = 220, h = 74, margin = 24;
            var x = page.Width.Point - w - margin;
            var y = page.Height.Point - h - margin;
            var rect = new XRect(x, y, w, h);

            var fondo = new XSolidBrush(XColor.FromArgb(245, 248, 250, 252));
            var borde = new XPen(XColor.FromArgb(255, 37, 99, 235), 1.1);
            gfx.DrawRoundedRectangle(borde, fondo, rect, new XSize(10, 10));

            var azul = new XSolidBrush(XColor.FromArgb(255, 29, 78, 216));
            var gris = new XSolidBrush(XColor.FromArgb(255, 71, 85, 105));
            var grisClaro = new XSolidBrush(XColor.FromArgb(255, 148, 163, 184));
            var fTitulo = new XFont("Arial", 7.5, XFontStyle.Bold);
            var fNombre = new XFont("Arial", 9, XFontStyle.Bold);
            var fLinea = new XFont("Arial", 7, XFontStyle.Regular);

            double px = x + 12, py = y + 8;
            gfx.DrawString("FIRMADO ELECTRONICAMENTE - TRONOX", fTitulo, azul, new XPoint(px, py + 6));
            gfx.DrawString(Trunc(d.Nombre, 42), fNombre, gris, new XPoint(px, py + 22));
            var cargoDep = string.Join(" - ", new[] { d.Cargo, d.Dependencia }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(cargoDep))
            {
                gfx.DrawString(Trunc(cargoDep, 48), fLinea, gris, new XPoint(px, py + 34));
            }
            gfx.DrawString(d.Fecha, fLinea, gris, new XPoint(px, py + 46));
            gfx.DrawString(Trunc(d.VerificarUrl, 48), fLinea, grisClaro, new XPoint(px, py + 57));

            using var msOut = new MemoryStream();
            doc.Save(msOut);
            return msOut.ToArray();
        }
        catch
        {
            return pdf;
        }
    }

    private static string Trunc(string? s, int max)
    {
        s ??= string.Empty;
        return s.Length <= max ? s : s[..(max - 3)] + "...";
    }
}
