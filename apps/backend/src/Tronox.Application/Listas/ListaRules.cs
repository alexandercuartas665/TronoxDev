namespace Tronox.Application.Listas;

/// <summary>
/// LOGICA PURA del administrador de listas (RQ02 - RF03): validaciones SIN EF y sin base de datos,
/// testeables en Tronox.Application.Tests. La unicidad (nombre por tenant, clave por lista) y el
/// conteo de opciones activas necesitan la base y viven en el servicio.
///
/// Devuelven null cuando el dato es valido, o el mensaje de error para la presentacion.
/// </summary>
public static class ListaRules
{
    public const int MaxNombre = 100;
    public const int MaxDescripcion = 300;
    public const int MaxClave = 50;
    public const int MaxValor = 200;

    /// <summary>Minimo de opciones ACTIVAS para que una lista pueda usarse en un metadato (RF03 3.3.4-2).</summary>
    public const int MinOpcionesActivas = 2;

    public static string? ValidateLista(string? nombreLista, string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombreLista)) { return "El nombre de la lista es obligatorio."; }
        if (nombreLista.Trim().Length > MaxNombre) { return $"El nombre no puede superar {MaxNombre} caracteres."; }
        if (descripcion is not null && descripcion.Trim().Length > MaxDescripcion)
        {
            return $"La descripcion no puede superar {MaxDescripcion} caracteres.";
        }
        return null;
    }

    public static string? ValidateOpcion(string? clave, string? valor)
    {
        if (string.IsNullOrWhiteSpace(clave)) { return "La clave de la opcion es obligatoria."; }
        if (clave.Trim().Length > MaxClave) { return $"La clave no puede superar {MaxClave} caracteres."; }
        if (string.IsNullOrWhiteSpace(valor)) { return "El valor de la opcion es obligatorio."; }
        if (valor.Trim().Length > MaxValor) { return $"El valor no puede superar {MaxValor} caracteres."; }
        return null;
    }

    /// <summary>
    /// Una lista es USABLE en un metadato si esta Activa y tiene al menos <see cref="MinOpcionesActivas"/>
    /// opciones activas (RF03 3.3.4-2). Puro: recibe el conteo ya hecho.
    /// </summary>
    public static bool EsUsable(bool listaActiva, int opcionesActivas)
        => listaActiva && opcionesActivas >= MinOpcionesActivas;
}
