namespace Tronox.Application.Common;

/// <summary>
/// Estampa el sticker de radicado (numero + fecha) sobre un PDF en una posicion dada (RF02-5, calca la
/// estampa arrastrable de rad_radicar: el operador elige donde queda con el chip #vsrChip). La posicion
/// llega como porcentaje (0..100) del ancho/alto de la primera pagina, esquina superior izquierda del
/// sello. La implementacion vive en Infrastructure (PdfSharpCore). Best-effort: si falla, devuelve el
/// PDF original sin romper el radicado.
/// </summary>
public interface IRadicadoEstampador
{
    byte[] EstamparRadicado(byte[] pdf, string numero, string fecha, double xPct, double yPct);
}
