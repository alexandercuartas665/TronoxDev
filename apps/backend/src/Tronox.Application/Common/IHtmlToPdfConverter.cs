namespace Tronox.Application.Common;

/// <summary>
/// Conversor de HTML a PDF para el editor de texto interno (RF08). Reemplaza al SelectPdf del legacy
/// (comercial y solo Windows) por un motor cross-platform; la implementacion vive en Infrastructure
/// (PuppeteerSharp / Chromium headless). Recibe el fragmento HTML del editor y devuelve los bytes del PDF
/// en tamano Carta. No sanitiza: la sanitizacion del HTML es responsabilidad de la capa que llama.
/// </summary>
public interface IHtmlToPdfConverter
{
    /// <summary>Convierte un fragmento HTML (cuerpo del editor) a un PDF tamano Carta y devuelve sus bytes.</summary>
    Task<byte[]> ConvertirAsync(string html, CancellationToken cancellationToken = default);
}
