namespace Tronox.Application.SeriesDocumentales;

/// <summary>
/// LOGICA PURA del catalogo de series y subseries (RQ02 - RF02): validaciones y algoritmo de
/// ciclos SIN EF y sin base de datos, testeables en Tronox.Application.Tests. El servicio se
/// limita a invocarlas y a resolver lo que si necesita la base (unicidad, existencia, uso en TRD).
///
/// Devuelven null cuando el dato es valido, o el mensaje de error listo para la presentacion
/// (mismo estilo que OrgStructureRules / ArchivisticaRules).
/// </summary>
public static class SerieRules
{
    public const int MaxCodigo = 20;
    public const int MaxNombre = 200;
    public const int MaxDescripcion = 500;

    /// <summary>
    /// Reglas de un nodo del catalogo (RF02 3.2.1): codigo y nombre obligatorios y acotados,
    /// descripcion opcional acotada. La unicidad (codigo por nivel, nombre por padre), la
    /// existencia del padre y la deteccion de ciclos NO se validan aqui: necesitan la base y
    /// viven en el servicio.
    /// </summary>
    public static string? ValidateSerie(string? codigo, string? nombre, string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(codigo)) { return "El codigo de la serie es obligatorio."; }
        if (codigo.Trim().Length > MaxCodigo) { return $"El codigo no puede superar {MaxCodigo} caracteres."; }
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre de la serie es obligatorio."; }
        if (nombre.Trim().Length > MaxNombre) { return $"El nombre no puede superar {MaxNombre} caracteres."; }
        if (descripcion is not null && descripcion.Trim().Length > MaxDescripcion)
        {
            return $"La descripcion no puede superar {MaxDescripcion} caracteres.";
        }
        return null;
    }

    /// <summary>
    /// Determina si asignar <paramref name="newParentId"/> como padre de <paramref name="serieId"/>
    /// crearia un ciclo (un nodo seria su propio ancestro). <paramref name="parentById"/> es el
    /// mapa hijo -> padre de TODAS las series del tenant. Puro: recibe el mapa ya cargado.
    ///
    /// Devuelve true si el nuevo padre ES el propio nodo o desciende de el.
    /// </summary>
    public static bool WouldCreateCycle(long serieId, long? newParentId, IReadOnlyDictionary<long, long?> parentById)
    {
        var cursor = newParentId;
        // Cota de seguridad: si el arbol ya viniera corrupto con un ciclo previo, no colgar.
        var saltos = 0;
        while (cursor is long id)
        {
            if (id == serieId) { return true; }
            if (!parentById.TryGetValue(id, out cursor)) { break; }
            if (++saltos > parentById.Count) { return true; }
        }
        return false;
    }
}
