using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tronox.Application.Common;
using Tronox.Application.Firmas;

namespace Tronox.Infrastructure.Pdf;

/// <summary>
/// Acta / certificado de firma en PDF (RQ05 - RF04) con QuestPDF, calcando fir_certificado.aspx del
/// legacy (informe de auditoria estilo Adobe Acrobat Sign). El legacy lo imprimia desde el navegador;
/// aca se genera server-side. Muestra el resumen probatorio (ID de transaccion, hash, estado) y el
/// historial de eventos del ledger append-only, con IP y origen de hora por evento.
/// </summary>
public sealed class QuestPdfActaRenderer : IActaFirmaRenderer
{
    private const string Brand = "#1D4ED8";
    private const string Ink = "#111827";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string OkGreen = "#15803D";

    public byte[] Render(ActaFirmaDto d)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink).FontFamily("Helvetica"));

                page.Header().Element(c => Header(c, d));
                page.Content().Element(c => Content(c, d));
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, ActaFirmaDto d)
    {
        container.PaddingBottom(14).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("TRONOX SGDEA").FontSize(15).Bold().FontColor(Brand);
                    c.Item().Text("Informe de auditoria de firma electronica").FontSize(11).FontColor(Muted);
                });
                row.ConstantItem(200).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("Generado").FontSize(8).FontColor(Muted);
                    c.Item().AlignRight().Text($"{d.FechaGeneracion.ToLocalTime():dd MMM yyyy HH:mm} GMT").FontSize(9);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Brand);
        });
    }

    private static void Content(IContainer container, ActaFirmaDto d)
    {
        container.PaddingTop(6).Column(col =>
        {
            col.Item().Text(d.DocumentoNombre).FontSize(14).Bold();
            col.Item().PaddingTop(2).Text(d.Completado ? "Documento firmado electronicamente." : "Proceso de firma en curso.")
                .FontSize(10).FontColor(d.Completado ? OkGreen : Muted);

            // Resumen probatorio.
            col.Item().PaddingTop(16).Background("#F8FAFC").Border(1).BorderColor(Line).Padding(14).Column(box =>
            {
                Kv(box, "Estado de firma", d.EstadoFirma);
                Kv(box, "ID de transaccion", d.IdTransaccion);
                Kv(box, "Hash SHA-256", string.IsNullOrWhiteSpace(d.HashSha256) ? "-" : d.HashSha256!);
                Kv(box, "Creado por", d.CreadoPor);
                Kv(box, "Fecha de creacion", $"{d.FechaCreacion.ToLocalTime():dd MMM yyyy HH:mm} GMT");
                Kv(box, "Verificacion", d.VerificarUrl);
            });

            // Historial de eventos.
            col.Item().PaddingTop(20).Text("Historial de eventos").FontSize(11).Bold();
            col.Item().PaddingTop(6).Column(evs =>
            {
                if (d.Eventos.Count == 0)
                {
                    evs.Item().PaddingVertical(6).Text("Sin eventos de firma registrados para este documento.")
                        .FontSize(9).FontColor(Muted).Italic();
                    return;
                }
                foreach (var e in d.Eventos)
                {
                    evs.Item().BorderBottom(1).BorderColor(Line).PaddingVertical(7).Row(r =>
                    {
                        r.ConstantItem(120).Column(c =>
                        {
                            c.Item().Text($"{e.Fecha.ToLocalTime():dd MMM yyyy}").FontSize(9).Bold();
                            c.Item().Text($"{e.Fecha.ToLocalTime():HH:mm:ss} GMT").FontSize(8).FontColor(Muted);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(e.Evento).FontSize(9.5f).Bold().FontColor(Brand);
                            c.Item().Text($"Por: {e.Actor}").FontSize(9);
                            if (!string.IsNullOrWhiteSpace(e.Ip))
                            {
                                c.Item().Text($"Direccion IP: {e.Ip}").FontSize(8).FontColor(Muted);
                            }
                            if (!string.IsNullOrWhiteSpace(e.Detalle))
                            {
                                c.Item().Text(e.Detalle!).FontSize(8).FontColor(Muted);
                            }
                        });
                    });
                }
            });

            if (d.Completado)
            {
                col.Item().PaddingTop(14).Text("Documento completado.").FontSize(10).Bold().FontColor(OkGreen);
            }

            col.Item().PaddingTop(18).Text(
                "Este informe se genera a partir de la pista de auditoria inalterable del sistema (registro append-only). " +
                "La integridad del documento se verifica recalculando su hash SHA-256 y comparandolo con el valor registrado.")
                .FontSize(8).FontColor(Muted).Italic();
        });
    }

    private static void Kv(ColumnDescriptor col, string k, string v)
    {
        col.Item().PaddingVertical(2.5f).Row(r =>
        {
            r.ConstantItem(150).Text(k).FontColor(Muted).FontSize(9);
            r.RelativeItem().Text(v).FontSize(9);
        });
    }

    private static void Footer(IContainer container)
    {
        container.BorderTop(1).BorderColor(Line).PaddingTop(6).Row(r =>
        {
            r.RelativeItem().Text("TRONOX SGDEA - Sistema de Gestion Documental Electronica de Archivo").FontSize(7.5f).FontColor(Muted);
            r.ConstantItem(120).AlignRight().Text(t =>
            {
                t.Span("Pagina ").FontSize(7.5f).FontColor(Muted);
                t.CurrentPageNumber().FontSize(7.5f).FontColor(Muted);
                t.Span(" de ").FontSize(7.5f).FontColor(Muted);
                t.TotalPages().FontSize(7.5f).FontColor(Muted);
            });
        });
    }
}
