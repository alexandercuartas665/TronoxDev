using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SkiaSharp;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Marca de agua de seguridad (RQ04 RF04) horneada en el binario para el visor, calca MarcarPdfSeguridad /
/// EstamparImpresionImagen del legacy (doc_visor.ashx): texto diagonal a -45 grados repetido en mosaico,
/// gris azulado semitransparente. PDF con PdfSharpCore (XGraphics), imagenes con SkiaSharp. Best-effort.
/// </summary>
public sealed class SecurityWatermarker : ISecurityWatermarker
{
    // Gris azulado semitransparente (calca XColor.FromArgb(38, 100, 116, 139) del legacy).
    private static readonly SKColor ColorImagen = new(100, 116, 139, 46);

    private static readonly string[] ExtImagen = ["jpg", "jpeg", "png", "tif", "tiff", "bmp", "gif"];

    static SecurityWatermarker()
    {
        try { GlobalFontSettings.FontResolver ??= new PdfSharpCore.Utils.FontResolver(); }
        catch { /* si ya estaba puesto o falla, el estampado cae al try/catch de abajo */ }
    }

    public byte[] Aplicar(byte[] contenido, string contentType, string? nombreArchivo, string texto)
    {
        if (EsPdf(contentType, nombreArchivo)) { return AplicarPdf(contenido, texto); }
        if (EsImagen(contentType, nombreArchivo)) { return AplicarImagen(contenido, nombreArchivo, texto); }
        return contenido;
    }

    private static byte[] AplicarPdf(byte[] pdf, string texto)
    {
        try
        {
            using var msIn = new MemoryStream(pdf);
            var doc = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
            var font = new XFont("Arial", 11, XFontStyle.Bold);
            var brush = new XSolidBrush(XColor.FromArgb(46, 100, 116, 139));
            foreach (var page in doc.Pages)
            {
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var w = page.Width.Point;
                var h = page.Height.Point;
                var diag = Math.Sqrt(w * w + h * h);
                var anchoTexto = Math.Max(120, gfx.MeasureString(texto, font).Width);
                gfx.TranslateTransform(w / 2, h / 2);
                gfx.RotateTransform(-45);
                // Mosaico: cubre toda la pagina desde el centro rotado (paso 120pt en vertical, legacy).
                for (var y = -diag; y < diag; y += 120)
                {
                    for (var x = -diag; x < diag; x += anchoTexto + 60)
                    {
                        gfx.DrawString(texto, font, brush, x, y);
                    }
                }
            }
            using var msOut = new MemoryStream();
            doc.Save(msOut);
            return msOut.ToArray();
        }
        catch
        {
            return pdf;
        }
    }

    private static byte[] AplicarImagen(byte[] imagen, string? nombreArchivo, string texto)
    {
        try
        {
            using var bmp = SKBitmap.Decode(imagen);
            if (bmp is null) { return imagen; }
            var w = bmp.Width;
            var h = bmp.Height;

            using var surface = SKSurface.Create(new SKImageInfo(w, h));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(bmp, 0, 0);

            var size = Math.Clamp(w / 32f, 12f, 40f);
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), size);
            using var paint = new SKPaint { Color = ColorImagen, IsAntialias = true };
            var anchoTexto = Math.Max(120f, font.MeasureText(texto));
            var diag = (float)Math.Sqrt(w * w + h * h);

            canvas.Save();
            canvas.Translate(w / 2f, h / 2f);
            canvas.RotateDegrees(-45);
            for (var y = -diag; y < diag; y += size * 5)
            {
                for (var x = -diag; x < diag; x += anchoTexto + 40)
                {
                    canvas.DrawText(texto, x, y, SKTextAlign.Left, font, paint);
                }
            }
            canvas.Restore();

            using var img = surface.Snapshot();
            var (fmt, quality) = Codificacion(nombreArchivo);
            using var data = img.Encode(fmt, quality);
            return data.ToArray();
        }
        catch
        {
            return imagen;
        }
    }

    private static bool EsPdf(string contentType, string? nombreArchivo)
        => string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
           || (nombreArchivo?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool EsImagen(string contentType, string? nombreArchivo)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) { return true; }
        var ext = System.IO.Path.GetExtension(nombreArchivo ?? "").TrimStart('.').ToLowerInvariant();
        return ExtImagen.Contains(ext);
    }

    private static (SKEncodedImageFormat, int) Codificacion(string? nombreArchivo)
    {
        var ext = System.IO.Path.GetExtension(nombreArchivo ?? "").TrimStart('.').ToLowerInvariant();
        return ext is "jpg" or "jpeg" ? (SKEncodedImageFormat.Jpeg, 92) : (SKEncodedImageFormat.Png, 100);
    }
}
