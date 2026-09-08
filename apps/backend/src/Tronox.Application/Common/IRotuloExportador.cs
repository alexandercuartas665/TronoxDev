using Tronox.Application.Expedientes;

namespace Tronox.Application.Common;

/// <summary>
/// Genera el PDF de rotulos de expediente (RQ03 - RF17). Calca RotuloExportador del legacy: A4 con los
/// rotulos apilados, tamano configurable (Caja/Carpeta/Sticker), N por hoja y posicion de inicio (para
/// aprovechar hojas de etiquetas parciales). Cada rotulo lleva un codigo de barras Code 128 del codigo
/// del expediente.
/// </summary>
public interface IRotuloExportador
{
    byte[] Generar(IReadOnlyList<RotuloDatoDto> rotulos, RotuloTamano tamano, int porHoja, int posicionInicio);
}
