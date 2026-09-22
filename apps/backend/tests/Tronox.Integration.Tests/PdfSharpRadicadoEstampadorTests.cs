using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Tronox.Infrastructure.Pdf;

namespace Tronox.Integration.Tests;

/// <summary>
/// Verifica el estampado del sello de radicado sobre PDF (RF02-5, estampa arrastrable del asistente):
/// el resultado sigue siendo un PDF valido, conserva las paginas y crece (se agrego contenido). No toca BD.
/// </summary>
public sealed class PdfSharpRadicadoEstampadorTests
{
    private static byte[] PdfDePaginas(int n)
    {
        var doc = new PdfDocument();
        for (var i = 0; i < n; i++) { doc.AddPage(); }
        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Estampa_el_radicado_y_sigue_siendo_pdf_valido()
    {
        var est = new PdfSharpRadicadoEstampador();
        var pdf = PdfDePaginas(2);

        var sellado = est.EstamparRadicado(pdf, "ALCPRU-E-2026-000005", "22/09/2026 06:30", 20, 75);

        Assert.NotEmpty(sellado);
        Assert.True(sellado.Length > pdf.Length, "el PDF estampado debe crecer respecto del original");
        using var ms = new MemoryStream(sellado);
        var reabierto = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(2, reabierto.PageCount);
    }

    [Fact]
    public void Posicion_fuera_de_rango_no_rompe_el_estampado()
    {
        var est = new PdfSharpRadicadoEstampador();
        var pdf = PdfDePaginas(1);

        // Porcentajes fuera de [0,100]: el estampador los acota y no debe lanzar.
        var sellado = est.EstamparRadicado(pdf, "RAD-1", "hoy", 999, -50);

        Assert.NotEmpty(sellado);
        using var ms = new MemoryStream(sellado);
        var reabierto = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(1, reabierto.PageCount);
    }

    [Fact]
    public void Pdf_invalido_devuelve_el_original_sin_romper()
    {
        var est = new PdfSharpRadicadoEstampador();
        var basura = System.Text.Encoding.UTF8.GetBytes("esto no es un pdf");

        var resultado = est.EstamparRadicado(basura, "RAD-2", "hoy", 62, 6);

        // Best-effort: si no se puede abrir, devuelve el original tal cual (no rompe el radicado).
        Assert.Equal(basura, resultado);
    }
}
