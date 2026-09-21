namespace Tronox.Application.Expedientes;

/// <summary>Tamano del rotulo imprimible (RQ03 - RF17, calca RotuloExportador.TamanoRotulo del legacy).</summary>
public enum RotuloTamano
{
    /// <summary>Lomo de caja de archivo (21 x 10 cm).</summary>
    Caja = 0,
    /// <summary>Portada de carpeta (18 x 6 cm).</summary>
    Carpeta = 1,
    /// <summary>Etiqueta adhesiva (10 x 5 cm).</summary>
    Sticker = 2
}

/// <summary>
/// Datos que se pintan en un rotulo de expediente (RF17). Se arma en runtime del expediente + TRD +
/// folios + ubicacion fisica; no persiste. Calca RotuloExportador.DatosRotulo del legacy.
/// </summary>
public sealed record RotuloDatoDto(
    string Fondo,
    string Seccion,
    string Serie,
    string Subserie,
    string CodigoExp,
    string NombreExp,
    string FechaInicial,
    string FechaFinal,
    string Folios,
    string Ubicacion);
