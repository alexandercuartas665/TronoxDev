using Tronox.Domain.Enums;

namespace Tronox.Application.Organization;

/// <summary>
/// Logica PURA del arbol organizacional (sin EF, testeable en unit tests y CACHEABLE):
///
/// 1. <see cref="WouldCreateCycle"/> - validacion de ciclos FAIL-CLOSED heredada del backbone.
/// 2. <see cref="ResolveDependenciaId"/> - resolver de dependencia del Addendum del ADR-003:
///    dado un nodo (tipicamente el Cargo al que se ancla un usuario) sube por la cadena de
///    padres hasta el primer nodo con clasificador Dependencia.
/// 3. <see cref="DescendantsAndSelf"/> - subarbol de un nodo, para saber a quien arrastra un
///    movimiento.
///
/// La Capa 2 de permisos (visibilidad por area documental) se resuelve caminando ESTE arbol
/// en memoria, no con una consulta por cada verificacion de acceso.
/// </summary>
public static class OrgUnitTree
{
    /// <summary>Proyeccion minima de un nodo para el resolver de dependencia.</summary>
    public readonly record struct NodeRef(long Id, long? ParentId, OrgUnitClassifier Classifier);

    /// <summary>
    /// True si poner <paramref name="unitId"/> bajo <paramref name="newParentId"/> crea un
    /// ciclo: el padre propuesto es el propio nodo o un descendiente suyo. Camina la cadena
    /// de ancestros del padre propuesto con un set de visitados, de modo que un arbol YA
    /// corrupto (ciclo preexistente en datos) tambien se reporta como ciclo en vez de
    /// colgarse. FAIL-CLOSED: ante datos corruptos se rechaza, no se permite.
    /// </summary>
    /// <param name="unitId">Nodo que se mueve/edita.</param>
    /// <param name="newParentId">Padre propuesto (null = raiz, nunca hay ciclo).</param>
    /// <param name="parentByUnit">Mapa Id -&gt; ParentId de TODOS los nodos del tenant.</param>
    public static bool WouldCreateCycle(long unitId, long? newParentId, IReadOnlyDictionary<long, long?> parentByUnit)
    {
        if (newParentId is null)
        {
            return false;
        }
        var visited = new HashSet<long>();
        var current = newParentId;
        while (current is long currentId)
        {
            if (currentId == unitId)
            {
                return true;
            }
            if (!visited.Add(currentId))
            {
                // Ciclo preexistente en los datos: tratarlo como invalido (fail-closed).
                return true;
            }
            current = parentByUnit.TryGetValue(currentId, out var parent) ? parent : null;
        }
        return false;
    }

    /// <summary>
    /// RESOLVER DE DEPENDENCIA (ADR-003, Addendum punto 1). Funcion PURA: arbol + nodo ->
    /// dependencia. Sin acceso a base de datos, para poder testearse sin infraestructura y
    /// cachearse por tenant.
    ///
    /// Sube por la cadena de padres desde <paramref name="nodeId"/> hasta el primer nodo con
    /// clasificador Dependencia y devuelve su Id. Si el propio nodo ya es una Dependencia, se
    /// devuelve a si mismo.
    ///
    /// FAIL-CLOSED, devuelve null (= SIN dependencia, SIN visibilidad documental; NUNCA
    /// visibilidad total) cuando:
    /// - el nodo no existe en el arbol (ej. usuario sin Cargo, o Cargo archivado/fuera del
    ///   conjunto visible);
    /// - se llega a la raiz sin encontrar ninguna Dependencia por encima;
    /// - el arbol esta corrupto y la cadena de ancestros cicla (set de visitados).
    /// </summary>
    /// <param name="nodeId">Nodo de partida (tipicamente el Cargo del usuario).</param>
    /// <param name="nodesById">Mapa Id -&gt; nodo de TODOS los nodos del tenant.</param>
    public static long? ResolveDependenciaId(long nodeId, IReadOnlyDictionary<long, NodeRef> nodesById)
    {
        var visited = new HashSet<long>();
        long? current = nodeId;
        while (current is long currentId)
        {
            if (!visited.Add(currentId))
            {
                // Arbol corrupto: nunca se resuelve a "todo", se resuelve a nada.
                return null;
            }
            if (!nodesById.TryGetValue(currentId, out var node))
            {
                return null;
            }
            if (node.Classifier == OrgUnitClassifier.Dependencia)
            {
                return node.Id;
            }
            current = node.ParentId;
        }
        return null;
    }

    /// <summary>
    /// Ids del subarbol que cuelga de <paramref name="rootId"/>, incluido el propio nodo.
    /// Es lo que ARRASTRA un movimiento: mover un Cargo mueve tambien sus sub-cargos y sus
    /// funcionarios. Camina con set de visitados (tolera ciclos preexistentes sin colgarse).
    /// </summary>
    public static IReadOnlySet<long> DescendantsAndSelf(
        long rootId, IReadOnlyDictionary<long, long?> parentByUnit)
    {
        var childrenByParent = parentByUnit
            .Where(kv => kv.Value is not null)
            .GroupBy(kv => kv.Value!.Value)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToList());

        var result = new HashSet<long>();
        var stack = new Stack<long>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current))
            {
                continue;
            }
            if (childrenByParent.TryGetValue(current, out var children))
            {
                foreach (var child in children)
                {
                    stack.Push(child);
                }
            }
        }
        return result;
    }
}
