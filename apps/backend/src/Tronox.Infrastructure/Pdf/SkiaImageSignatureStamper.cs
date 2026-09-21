using SkiaSharp;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Estampa la cajita de firma sobre imagenes (RQ05 - RF03-B) con SkiaSharp. Dibuja una banda al pie con
/// el titulo/nombre/fecha/URL y, si el firmante tiene grafo, la firma manuscrita a la derecha. Devuelve
/// la imagen en su mismo formato cuando es posible (jpg/png), si no en PNG. Best-effort.
/// </summary>
public sealed class SkiaImageSignatureStamper : IImageSignatureStamper
{
    private static readonly string[] Formatos = ["jpg", "jpeg", "png", "tif", "tiff", "bmp", "gif"];

    public bool Soporta(string? formato)
        => !string.IsNullOrWhiteSpace(formato) && Formatos.Contains(formato.Trim().ToLowerInvariant());

    public byte[] EstamparCajita(byte[] imagen, string? formato, CajitaFirma d)
    {
        try
        {
            using var original = SKBitmap.Decode(imagen);
            if (original is null) { return imagen; }

            var w = original.Width;
            var h = original.Height;
            // Banda de firma proporcional al ancho (min 90px), al pie de la imagen.
            var banda = Math.Clamp(w / 5, 90, 260);

            var info = new SKImageInfo(w, h + banda);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(original, 0, 0);

            var top = (float)h;
            using (var fondo = new SKPaint { Color = new SKColor(248, 250, 252), IsAntialias = true })
            {
                canvas.DrawRect(0, top, w, banda, fondo);
            }
            using (var borde = new SKPaint { Color = new SKColor(37, 99, 235), StrokeWidth = 2, Style = SKPaintStyle.Stroke })
            {
                canvas.DrawLine(0, top, w, top, borde);
            }

            var pad = banda * 0.16f;
            var x = pad;
            var y = top + pad + banda * 0.10f;

            var azul = new SKColor(29, 78, 216);
            var gris = new SKColor(71, 85, 105);
            var grisClaro = new SKColor(148, 163, 184);
            var fTit = banda * 0.16f;
            var fNom = banda * 0.20f;
            var fLin = banda * 0.145f;

            var titulo = string.IsNullOrWhiteSpace(d.TextoConfig) ? "FIRMADO ELECTRONICAMENTE - TRONOX" : d.TextoConfig!;
            DibujarTexto(canvas, Trunc(titulo, 46), x, y, fTit, azul, bold: true);
            y += fNom + banda * 0.04f;
            var nombre = d.MostrarNombre ? d.Nombre : "Firma electronica";
            DibujarTexto(canvas, Trunc(nombre, 48), x, y, fNom, gris, bold: true);
            y += fLin + banda * 0.06f;
            DibujarTexto(canvas, d.Fecha, x, y, fLin, gris, bold: false);
            y += fLin + banda * 0.03f;
            DibujarTexto(canvas, Trunc(d.VerificarUrl, 54), x, y, fLin, grisClaro, bold: false);

            // Grafo (firma manuscrita) a la derecha de la banda.
            var grafo = LimpiarBase64(d.GrafoBase64);
            if (!string.IsNullOrWhiteSpace(grafo))
            {
                try
                {
                    var gb = Convert.FromBase64String(grafo!);
                    using var gimg = SKBitmap.Decode(gb);
                    if (gimg is not null)
                    {
                        var gh = banda * 0.62f;
                        var gw = gh * gimg.Width / Math.Max(1, gimg.Height);
                        var gx = w - gw - pad;
                        var gy = top + (banda - gh) / 2;
                        canvas.DrawBitmap(gimg, new SKRect(gx, gy, gx + gw, gy + gh));
                    }
                }
                catch { /* grafo invalido: se omite */ }
            }

            using var img = surface.Snapshot();
            var (fmt, quality) = Codificacion(formato);
            using var data = img.Encode(fmt, quality);
            return data.ToArray();
        }
        catch
        {
            return imagen;
        }
    }

    private static void DibujarTexto(SKCanvas canvas, string texto, float x, float y, float size, SKColor color, bool bold)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial",
            bold ? SKFontStyle.Bold : SKFontStyle.Normal), size);
        canvas.DrawText(texto, x, y + size, SKTextAlign.Left, font, paint);
    }

    private static (SKEncodedImageFormat, int) Codificacion(string? formato)
        => (formato?.Trim().ToLowerInvariant()) switch
        {
            "jpg" or "jpeg" => (SKEncodedImageFormat.Jpeg, 92),
            _ => (SKEncodedImageFormat.Png, 100)
        };

    private static string Trunc(string? s, int max)
    {
        s ??= string.Empty;
        return s.Length <= max ? s : s[..(max - 3)] + "...";
    }

    private static string? LimpiarBase64(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) { return null; }
        var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? s[(idx + 7)..].Trim() : s.Trim();
    }
}
