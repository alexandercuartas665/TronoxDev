using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using Tronox.Application.Common;
using Tronox.Application.Expedientes;
using ZXing;
using ZXing.OneD;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Genera el PDF de rotulos de expediente (RQ03 - RF17) con QuestPDF, calcando RotuloExportador del
/// legacy: encabezado azul "ARCHIVO DE GESTION", cinco secciones (fondo/seccion/serie/subserie,
/// expediente + nombre, fechas/folios/ubicacion) y un codigo de barras Code 128 del codigo del
/// expediente. La pagina es A4 con N rotulos por hoja (1/2/4) y una posicion de inicio para aprovechar
/// hojas de etiquetas parciales. El tamano (Caja/Carpeta/Sticker) se rotula como referencia de corte.
/// </summary>
public sealed class QuestPdfRotuloExportador : IRotuloExportador
{
    private const string Azul = "#00467F";   // barra de encabezado (RGB 0,70,127 del legacy)
    private const string Ink = "#111827";
    private const string Muted = "#6B7280";
    private const string Line = "#D1D5DB";

    public byte[] Generar(IReadOnlyList<RotuloDatoDto> rotulos, RotuloTamano tamano, int porHoja, int posicionInicio)
    {
        var perPage = porHoja is 1 or 2 or 4 ? porHoja : 4;
        var cols = perPage == 4 ? 2 : 1;
        var rows = perPage / cols;
        var inicio = Math.Clamp(posicionInicio, 1, perPage);

        // Slots = huecos iniciales vacios (hoja parcial) + los rotulos.
        var slots = new List<RotuloDatoDto?>();
        for (var i = 1; i < inicio; i++) { slots.Add(null); }
        slots.AddRange(rotulos ?? []);
        if (slots.Count == 0) { slots.Add(null); }

        // Reparto en paginas de perPage slots.
        var pages = new List<List<RotuloDatoDto?>>();
        for (var i = 0; i < slots.Count; i += perPage)
        {
            pages.Add(slots.GetRange(i, Math.Min(perPage, slots.Count - i)));
        }

        var tamLabel = tamano switch
        {
            RotuloTamano.Carpeta => "Carpeta (18 x 6 cm)",
            RotuloTamano.Sticker => "Sticker (10 x 5 cm)",
            _ => "Caja (21 x 10 cm)"
        };

        return Document.Create(doc =>
        {
            foreach (var pageSlots in pages)
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(8).FontColor(Ink).FontFamily("Helvetica"));

                    page.Content().Grid(grid =>
                    {
                        grid.Columns(cols);
                        grid.Spacing(10);
                        for (var i = 0; i < perPage; i++)
                        {
                            var slot = i < pageSlots.Count ? pageSlots[i] : null;
                            grid.Item().Height((float)(UsableCmHeight() / rows), Unit.Centimetre)
                                .Element(c => Celda(c, slot));
                        }
                    });

                    page.Footer().AlignRight().Text(tamLabel).FontSize(7).FontColor(Muted);
                });
            }
        }).GeneratePdf();
    }

    // Alto util de A4 (29.7 cm) menos margenes (2 x 1 cm) y un respiro para el footer.
    private static double UsableCmHeight() => 26.8;

    private static void Celda(IContainer container, RotuloDatoDto? d)
    {
        if (d is null)
        {
            // Hueco de una hoja de etiquetas parcial: recuadro tenue vacio.
            container.Border(1).BorderColor("#E5E7EB").Background("#FAFAFA");
            return;
        }

        container.Border(1).BorderColor(Ink).Column(col =>
        {
            // 1) Encabezado azul.
            col.Item().Background(Azul).PaddingVertical(3).AlignCenter()
                .Text("ARCHIVO DE GESTION").FontColor("#FFFFFF").FontSize(9).Bold();

            // 2) Fondo / Seccion / Serie / Subserie.
            col.Item().Padding(6).Column(s =>
            {
                Linea(s, "FONDO:", d.Fondo);
                Linea(s, "SECCION:", d.Seccion);
                Linea(s, "SERIE:", d.Serie);
                Linea(s, "SUBSERIE:", d.Subserie);
            });
            col.Item().LineHorizontal(0.5f).LineColor(Line);

            // 3) Expediente (codigo) + nombre.
            col.Item().PaddingHorizontal(6).PaddingVertical(4).Column(s =>
            {
                s.Item().Row(r =>
                {
                    r.ConstantItem(70).Text("EXPEDIENTE:").FontSize(8).Bold();
                    r.RelativeItem().Text(d.CodigoExp).FontSize(9).Bold().FontColor(Azul);
                });
                s.Item().PaddingTop(2).Row(r =>
                {
                    r.ConstantItem(70).Text("NOMBRE:").FontSize(8).Bold();
                    r.RelativeItem().Text(Trunc(d.NombreExp, 90)).FontSize(8);
                });
            });
            col.Item().LineHorizontal(0.5f).LineColor(Line);

            // 4) Fechas / folios / ubicacion.
            col.Item().PaddingHorizontal(6).PaddingVertical(4).Column(s =>
            {
                s.Item().Row(r =>
                {
                    r.RelativeItem().Text(txt =>
                    {
                        txt.Span("FECHAS: ").Bold();
                        txt.Span($"{Guion(d.FechaInicial)} / {Guion(d.FechaFinal)}");
                    });
                    r.ConstantItem(90).Text(txt =>
                    {
                        txt.Span("FOLIOS: ").Bold();
                        txt.Span(Guion(d.Folios));
                    });
                });
                if (!string.IsNullOrWhiteSpace(d.Ubicacion))
                {
                    s.Item().PaddingTop(2).Text(txt =>
                    {
                        txt.Span("UBICACION: ").Bold();
                        txt.Span(Trunc(d.Ubicacion, 80));
                    });
                }
            });

            // 5) Codigo de barras Code 128 del codigo del expediente.
            col.Item().PaddingHorizontal(6).PaddingBottom(6).PaddingTop(2).AlignCenter().Column(s =>
            {
                var png = Code128Png(d.CodigoExp);
                if (png is not null)
                {
                    s.Item().Height(34).AlignCenter().Image(png).FitHeight();
                }
                s.Item().AlignCenter().Text(d.CodigoExp).FontSize(7).FontColor(Muted);
            });
        });
    }

    private static void Linea(ColumnDescriptor col, string etiqueta, string valor)
    {
        col.Item().PaddingVertical(1).Row(r =>
        {
            r.ConstantItem(58).Text(etiqueta).FontSize(7.5f).Bold();
            r.RelativeItem().Text(Trunc(string.IsNullOrWhiteSpace(valor) ? "-" : valor, 60)).FontSize(7.5f);
        });
    }

    private static string Guion(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s;

    private static string Trunc(string? s, int max)
    {
        s ??= string.Empty;
        return s.Length <= max ? s : s[..(max - 1)] + "...";
    }

    /// <summary>
    /// Genera el PNG de un codigo de barras Code 128 (bars puros, sin texto: el codigo va aparte).
    /// ZXing produce el patron de modulos y SkiaSharp lo rasteriza. Best-effort: null si algo falla.
    /// </summary>
    private static byte[]? Code128Png(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) { return null; }
        try
        {
            var matrix = new Code128Writer().encode(texto, BarcodeFormat.CODE_128, 0, 1);
            var modules = matrix.Width;
            if (modules <= 0) { return null; }
            const int scale = 2, height = 90, quiet = 12;
            var w = modules * scale + quiet * 2;
            using var bmp = new SKBitmap(w, height);
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
                for (var x = 0; x < modules; x++)
                {
                    if (matrix[x, 0])
                    {
                        canvas.DrawRect(quiet + x * scale, 0, scale, height, paint);
                    }
                }
            }
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
