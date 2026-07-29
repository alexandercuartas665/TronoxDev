namespace Tronox.Application.Topografia;

/// <summary>
/// LOGICA PURA de la topografia fisica (RQ02 - RF06): codigo topografico, jerarquia de niveles,
/// regla de capacidad y deteccion de ciclos. Sin EF ni base de datos, testeable en
/// Tronox.Application.Tests. La unicidad, existencia y "config bloqueada si hay elementos" necesitan
/// la base y viven en el servicio.
/// </summary>
public static class TopografiaRules
{
    public const int MaxNombre = 100;
    public const int MaxSiglaBase = 10;
    public const int MaxSiglaElemento = 20;

    // ---- Niveles ----

    public static string? ValidateNivel(string? nombreNivel, string? siglaBase, int orden)
    {
        if (string.IsNullOrWhiteSpace(nombreNivel)) { return "El nombre del nivel es obligatorio."; }
        if (nombreNivel.Trim().Length > MaxNombre) { return $"El nombre no puede superar {MaxNombre} caracteres."; }
        if (string.IsNullOrWhiteSpace(siglaBase)) { return "La sigla base es obligatoria."; }
        if (siglaBase.Trim().Length > MaxSiglaBase) { return $"La sigla base no puede superar {MaxSiglaBase} caracteres."; }
        if (orden < 1) { return "El orden debe ser un entero mayor o igual a 1 (1 = nivel mas alto)."; }
        return null;
    }

    /// <summary>
    /// Regla RF06 3.6.6-2: solo UN nivel puede controlar capacidad y debe ser el de MAYOR orden.
    /// Recibe, para el nivel que se quiere marcar como controlador, su orden, y la lista de los DEMAS
    /// niveles (orden, controla). Puro: la lista ya viene cargada. Devuelve null si es valido.
    /// </summary>
    public static string? ValidateControlaCapacidad(int ordenCandidato, IReadOnlyCollection<(int Orden, bool Controla)> otrosNiveles)
    {
        if (otrosNiveles.Any(n => n.Controla))
        {
            return "Ya existe un nivel que controla la capacidad. Solo puede haber uno por entidad.";
        }
        var maxOrdenOtros = otrosNiveles.Count == 0 ? 0 : otrosNiveles.Max(n => n.Orden);
        if (ordenCandidato < maxOrdenOtros)
        {
            return "El nivel que controla la capacidad debe ser el de mayor orden (el contenedor mas pequeno).";
        }
        return null;
    }

    // ---- Elementos ----

    public static string? ValidateElemento(string? nombre, string? sigla)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre del elemento es obligatorio."; }
        if (nombre.Trim().Length > MaxNombre) { return $"El nombre no puede superar {MaxNombre} caracteres."; }
        if (string.IsNullOrWhiteSpace(sigla)) { return "La sigla del elemento es obligatoria."; }
        if (sigla.Trim().Length > MaxSiglaElemento) { return $"La sigla no puede superar {MaxSiglaElemento} caracteres."; }
        return null;
    }

    /// <summary>
    /// Codigo topografico (RF06 3.6.3): concatena las siglas desde la RAIZ hasta el elemento, unidas
    /// por "-". No se almacena; se calcula en runtime. Recibe el mapa elemento -> (padre, sigla) de
    /// todo el tenant. Fail-closed ante ciclos previos (corta si se excede la profundidad posible).
    /// </summary>
    public static string CodigoTopografico(long elementoId, IReadOnlyDictionary<long, (long? Parent, string Sigla)> arbol)
    {
        var siglas = new List<string>();
        var cursor = (long?)elementoId;
        var saltos = 0;
        while (cursor is long id && arbol.TryGetValue(id, out var nodo))
        {
            siglas.Add(nodo.Sigla);
            cursor = nodo.Parent;
            if (++saltos > arbol.Count) { break; } // arbol corrupto: no colgar
        }
        siglas.Reverse(); // de la raiz hacia la hoja
        return string.Join("-", siglas);
    }

    /// <summary>
    /// El nivel de un hijo debe tener MAYOR orden que el del padre (RF06: un contenedor solo aloja
    /// contenedores mas pequenos). ordenPadre null = el elemento es raiz (cualquier nivel vale).
    /// </summary>
    public static string? ValidateJerarquia(int? ordenPadre, int ordenNivelHijo)
        => ordenPadre is int op && ordenNivelHijo <= op
            ? "El elemento hijo debe pertenecer a un nivel de orden mayor que el de su contenedor."
            : null;

    public static bool WouldCreateCycle(long elementoId, long? newParentId, IReadOnlyDictionary<long, long?> parentById)
    {
        var cursor = newParentId;
        var saltos = 0;
        while (cursor is long id)
        {
            if (id == elementoId) { return true; }
            if (!parentById.TryGetValue(id, out cursor)) { break; }
            if (++saltos > parentById.Count) { return true; }
        }
        return false;
    }

    /// <summary>
    /// Ocupacion de un elemento = numero de hijos directos (paridad con el legacy
    /// RecalcularEstadoLleno). Un elemento que controla capacidad pasa a Lleno cuando la alcanza.
    /// </summary>
    public static bool EstaLleno(int hijosDirectos, int? capacidad)
        => capacidad is int cap && cap > 0 && hijosDirectos >= cap;
}
