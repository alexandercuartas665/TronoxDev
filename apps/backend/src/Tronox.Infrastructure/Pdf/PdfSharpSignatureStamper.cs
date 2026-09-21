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

            // El grafo (firma manuscrita) se dibuja en una franja superior de la cajita; sin grafo la caja
            // conserva la altura de texto (RF03 3.3.3, calca FirmaCajitaHelper).
            var grafo = LimpiarBase64(d.GrafoBase64);
            var hayGrafo = !string.IsNullOrWhiteSpace(grafo);
            const double w = 220, margin = 24, gap = 8, grafoH = 42;
            double h = hayGrafo ? 120 : 74;
            var x = page.Width.Point - w - margin;
            // Indice apila las cajitas hacia arriba (circuito multi-firmante) para que no se solapen.
            var y = page.Height.Point - h - margin - (d.Indice * (h + gap));
            if (y < margin) { y = margin; }
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

            // Grafo escalado y centrado en la franja superior + separador (RF03 3.3.3).
            double py = y + 8;
            if (hayGrafo)
            {
                DibujarGrafo(gfx, grafo!, new XRect(x + 12, y + 8, w - 24, grafoH));
                var sep = new XPen(XColor.FromArgb(255, 210, 216, 226), 0.8);
                gfx.DrawLine(sep, x + 12, y + 8 + grafoH + 4, x + w - 12, y + 8 + grafoH + 4);
                py = y + 8 + grafoH + 8;
            }

            // Titulo configurable (FirmaTextoDefault) o el default.
            var titulo = string.IsNullOrWhiteSpace(d.TextoConfig) ? "FIRMADO ELECTRONICAMENTE - TRONOX" : d.TextoConfig!;
            double px = x + 12;
            gfx.DrawString(Trunc(titulo, 40), fTitulo, azul, new XPoint(px, py + 6));
            // El nombre puede ocultarse por configuracion (FirmaMostrarNombre).
            var nombre = d.MostrarNombre ? d.Nombre : "Firma electronica";
            gfx.DrawString(Trunc(nombre, 42), fNombre, gris, new XPoint(px, py + 22));
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

    /// <summary>
    /// Dibuja el grafo (firma manuscrita base64) dentro del recuadro, preservando proporcion y centrado.
    /// Calca FirmaCajitaHelper.DibujarGrafo: el PNG se materializa en un archivo temporal y se coloca con
    /// PdfSharp (XImage.FromFile decodifica via ImageSharp, cross-platform). Best-effort: si el base64 es
    /// invalido no dibuja nada y la cajita sigue con el texto.
    /// </summary>
    private static void DibujarGrafo(XGraphics gfx, string base64, XRect box)
    {
        string? tmp = null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            tmp = Path.Combine(Path.GetTempPath(), "grafo_" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(tmp, bytes);
            using var img = XImage.FromFile(tmp);
            var escala = Math.Min(box.Width / img.PixelWidth, box.Height / img.PixelHeight);
            if (escala <= 0) { escala = 1; }
            var wDst = img.PixelWidth * escala;
            var hDst = img.PixelHeight * escala;
            var xDst = box.X + (box.Width - wDst) / 2;
            var yDst = box.Y + (box.Height - hDst) / 2;
            gfx.DrawImage(img, xDst, yDst, wDst, hDst);
        }
        catch { /* grafo invalido: se omite sin romper la cajita (best-effort) */ }
        finally
        {
            try { if (tmp is not null && File.Exists(tmp)) { File.Delete(tmp); } } catch { }
        }
    }

    /// <summary>Quita el prefijo data-URI ("data:image/png;base64,XXXX") si viene incluido. Calca el legacy.</summary>
    private static string? LimpiarBase64(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) { return null; }
        var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? s[(idx + 7)..].Trim() : s.Trim();
    }
}
