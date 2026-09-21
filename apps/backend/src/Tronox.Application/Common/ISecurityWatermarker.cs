namespace Tronox.Application.Common;

/// <summary>
/// Hornea una marca de agua de seguridad (RQ04 RF04) sobre el binario que se muestra en el visor cuando el
/// documento es Reservado o Clasificado. Calca MarcarPdfSeguridad / EstamparImpresionImagen del legacy
/// (doc_visor.ashx): texto diagonal a -45 grados repetido en mosaico, gris azulado semitransparente, con el
/// usuario/fecha/IP que consulta. Aplica a PDF (PdfSharpCore) y a imagenes (SkiaSharp); otros formatos se
/// devuelven sin tocar. Best-effort: si el estampado falla, devuelve el binario original.
/// </summary>
public interface ISecurityWatermarker
{
    byte[] Aplicar(byte[] contenido, string contentType, string? nombreArchivo, string texto);
}
