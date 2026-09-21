using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SkiaSharp;
using Tronox.Infrastructure.Pdf;

namespace Tronox.Integration.Tests;

/// <summary>
/// Verifica la marca de agua de seguridad del visor (RQ04 RF04): se hornea sobre PDF e imagen para
/// documentos Reservado/Clasificado, y los formatos no soportados se devuelven sin tocar. No toca BD.
/// </summary>
public sealed class SecurityWatermarkerTests
{
    private const string Texto = "Usuario: Carlos Perez - Fecha: 2026-09-21 10:30:00 - IP: 10.0.0.9";

    private static byte[] PdfDeUnaPagina()
    {
        var doc = new PdfDocument();
        doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    private static byte[] Png(int w, int h)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(new SKColor(240, 240, 240));
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Fact]
    public void Aplica_marca_en_pdf_y_sigue_siendo_pdf_valido()
    {
        var wm = new SecurityWatermarker();
        var pdf = PdfDeUnaPagina();

        var sellado = wm.Aplicar(pdf, "application/pdf", "reservado.pdf", Texto);

        Assert.NotEmpty(sellado);
        // El resultado debe reabrir como PDF (no se corrompio) y no ser el mismo (se agrego contenido).
        using var ms = new MemoryStream(sellado);
        var reabierto = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.True(reabierto.PageCount >= 1);
        Assert.True(sellado.Length > pdf.Length);
    }

    [Fact]
    public void Aplica_marca_en_imagen_y_conserva_dimensiones()
    {
        var wm = new SecurityWatermarker();
        var png = Png(600, 400);

        var sellado = wm.Aplicar(png, "image/png", "clasificado.png", Texto);

        Assert.NotEmpty(sellado);
        using var bmp = SKBitmap.Decode(sellado);
        Assert.NotNull(bmp);
        Assert.Equal(600, bmp!.Width);
        Assert.Equal(400, bmp.Height);
    }

    [Fact]
    public void Formato_no_soportado_devuelve_original()
    {
        var wm = new SecurityWatermarker();
        var bytes = new byte[] { 10, 20, 30, 40 };

        var resultado = wm.Aplicar(bytes, "application/octet-stream", "archivo.docx", Texto);

        Assert.Equal(bytes, resultado);
    }

    [Fact]
    public void Pdf_invalido_devuelve_original()
    {
        var wm = new SecurityWatermarker();
        var basura = new byte[] { 1, 2, 3 };

        var resultado = wm.Aplicar(basura, "application/pdf", "roto.pdf", Texto);

        Assert.Equal(basura, resultado);
    }
}
