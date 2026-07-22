namespace Tronox.Application.Archivistica;

/// <summary>
/// Definicion CANONICA de los 4 niveles de clasificacion documental de TRONOX (RQ01 - RF01-P.3).
///
/// Vive en Application y es LOGICA PURA (sin EF) para que la use tanto el aprovisionamiento del
/// alta de tenant como los tests, sin que las dos definiciones puedan derivar. El orden crece
/// con la restriccion: 1 = Publico (menor) ... 4 = Clasificado (mayor).
/// </summary>
public static class NivelClasificacionCatalogo
{
    public sealed record NivelSemilla(string Nombre, string Codigo, string ColorEtiqueta, int NivelOrden, string Descripcion);

    public static readonly IReadOnlyList<NivelSemilla> Niveles =
    [
        new("Publico",     "01", "#27AE60", 1, "Informacion de libre acceso para cualquier ciudadano."),
        new("Interno",     "02", "#2980B9", 2, "Informacion de uso interno de la entidad."),
        new("Reservado",   "03", "#E67E22", 3, "Informacion reservada segun la Ley 1712 de 2014."),
        new("Clasificado", "04", "#C0392B", 4, "Informacion clasificada: acceso restringido y auditado.")
    ];
}
