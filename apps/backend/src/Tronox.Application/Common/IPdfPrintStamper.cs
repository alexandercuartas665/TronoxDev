namespace Tronox.Application.Common;

/// <summary>
/// Estampa la leyenda de impresion al pie de cada pagina de un PDF (RQ04 RF05 "Imprimir - copia con
/// estampa"), calca EstamparImpresionPdf del legacy. La implementacion vive en Infrastructure
/// (PdfSharpCore, cross-platform). Best-effort: si el PDF no se puede estampar, devuelve el original.
/// </summary>
public interface IPdfPrintStamper
{
    byte[] EstamparLeyendaPie(byte[] pdf, string leyenda);
}
