using SkiaSharp;
using Tronox.Application.Common;
using Tronox.Infrastructure.Pdf;

namespace Tronox.Integration.Tests;

/// <summary>
/// Verifica el estampado de la cajita de firma sobre documentos IMAGEN (RQ05 - RF03-B, "estampa en
/// imagenes"). No toca base de datos: es una prueba pura del SkiaImageSignatureStamper.
/// </summary>
public sealed class SkiaImageSignatureStamperTests
{
    private static byte[] GenerarPng(int ancho, int alto)
    {
        var info = new SKImageInfo(ancho, alto);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(230, 230, 230));
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static CajitaFirma Cajita() => new(
        "Carlos Perez", "Profesional", "Gestion Documental",
        "2026-09-21 10:30", "verificar.tronox.co/v/123", 0,
        TextoConfig: null, MostrarNombre: true, GrafoBase64: null);

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("tif")]
    [InlineData("bmp")]
    [InlineData("gif")]
    public void Soporta_formatos_de_imagen(string formato)
    {
        var stamper = new SkiaImageSignatureStamper();
        Assert.True(stamper.Soporta(formato));
        Assert.True(stamper.Soporta(formato.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    [InlineData("")]
    [InlineData(null)]
    public void No_soporta_no_imagenes(string? formato)
    {
        var stamper = new SkiaImageSignatureStamper();
        Assert.False(stamper.Soporta(formato));
    }

    [Fact]
    public void Estampar_agrega_banda_al_pie_y_devuelve_imagen_valida()
    {
        var stamper = new SkiaImageSignatureStamper();
        var original = GenerarPng(600, 400);

        var sellado = stamper.EstamparCajita(original, "png", Cajita());

        Assert.NotNull(sellado);
        Assert.NotEmpty(sellado);
        // El resultado debe decodificar como imagen y ser mas alto (la banda de firma se suma al pie).
        using var bmp = SKBitmap.Decode(sellado);
        Assert.NotNull(bmp);
        Assert.Equal(600, bmp!.Width);
        Assert.True(bmp.Height > 400, "La imagen sellada debe crecer en alto por la banda de firma.");
    }

    [Fact]
    public void Estampar_con_grafo_no_falla()
    {
        var stamper = new SkiaImageSignatureStamper();
        var original = GenerarPng(800, 500);
        var grafo = "data:image/png;base64," + Convert.ToBase64String(GenerarPng(120, 60));
        var cajita = Cajita() with { GrafoBase64 = grafo };

        var sellado = stamper.EstamparCajita(original, "jpg", cajita);

        Assert.NotEmpty(sellado);
        using var bmp = SKBitmap.Decode(sellado);
        Assert.NotNull(bmp);
        Assert.True(bmp!.Height > 500);
    }

    [Fact]
    public void Estampar_imagen_invalida_devuelve_original()
    {
        var stamper = new SkiaImageSignatureStamper();
        var basura = new byte[] { 1, 2, 3, 4, 5 };

        var resultado = stamper.EstamparCajita(basura, "png", Cajita());

        // Best-effort: si no puede decodificar, devuelve los bytes originales sin lanzar.
        Assert.Equal(basura, resultado);
    }
}
