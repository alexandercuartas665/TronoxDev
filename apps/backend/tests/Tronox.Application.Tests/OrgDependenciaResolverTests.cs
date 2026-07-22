using Tronox.Application.Organization;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Resolver de dependencia del Addendum del ADR-003. Es logica PURA (arbol + nodo ->
/// dependencia): estos tests corren SIN base de datos y sin ninguna infraestructura, que es
/// justamente lo que permite cachear la resolucion en la Capa 2 de permisos.
///
/// Lo que se fija aqui:
/// - subir desde un Cargo anidado varios niveles hasta su Dependencia;
/// - el caso FAIL-CLOSED: un Cargo que cuelga de la raiz, sin Dependencia por encima, resuelve
///   a SIN dependencia (null), NUNCA a visibilidad total;
/// - un arbol ya corrupto no cuelga el resolver: tambien resuelve fail-closed.
/// </summary>
public class OrgDependenciaResolverTests
{
    private static readonly long Ministerio = TestIds.Next();
    private static readonly long Viceministerio = TestIds.Next();
    private static readonly long Oficina = TestIds.Next();
    private static readonly long CargoJefe = TestIds.Next();
    private static readonly long CargoAuxiliar = TestIds.Next();
    private static readonly long Funcionario = TestIds.Next();
    private static readonly long CargoHuerfano = TestIds.Next();

    /// <summary>
    /// Ministerio (Dep) -> Viceministerio (Dep) -> Oficina (Dep) -> CargoJefe -> Funcionario,
    /// y ademas Oficina -> CargoAuxiliar. CargoHuerfano cuelga de la RAIZ (sin Dependencia).
    /// </summary>
    private static Dictionary<long, OrgUnitTree.NodeRef> Arbol() => new()
    {
        [Ministerio] = new(Ministerio, null, OrgUnitClassifier.Dependencia),
        [Viceministerio] = new(Viceministerio, Ministerio, OrgUnitClassifier.Dependencia),
        [Oficina] = new(Oficina, Viceministerio, OrgUnitClassifier.Dependencia),
        [CargoJefe] = new(CargoJefe, Oficina, OrgUnitClassifier.Cargo),
        [CargoAuxiliar] = new(CargoAuxiliar, Oficina, OrgUnitClassifier.Cargo),
        [Funcionario] = new(Funcionario, CargoJefe, OrgUnitClassifier.Funcionario),
        [CargoHuerfano] = new(CargoHuerfano, null, OrgUnitClassifier.Cargo)
    };

    [Fact]
    public void CargoAnidado_ResuelveALaDependenciaMasCercana()
    {
        // Sube CargoJefe -> Oficina: la PRIMERA Dependencia hacia arriba, no la raiz del arbol.
        Assert.Equal(Oficina, OrgUnitTree.ResolveDependenciaId(CargoJefe, Arbol()));
        Assert.Equal(Oficina, OrgUnitTree.ResolveDependenciaId(CargoAuxiliar, Arbol()));
    }

    [Fact]
    public void FuncionarioAnidado_SubeVariosNiveles_HastaLaDependencia()
    {
        // Funcionario -> CargoJefe -> Oficina: atraviesa un nodo Cargo por el camino.
        Assert.Equal(Oficina, OrgUnitTree.ResolveDependenciaId(Funcionario, Arbol()));
    }

    [Fact]
    public void NodoQueYaEsDependencia_SeResuelveASiMismo()
    {
        Assert.Equal(Viceministerio, OrgUnitTree.ResolveDependenciaId(Viceministerio, Arbol()));
    }

    [Fact]
    public void CargoColgadoDeLaRaiz_SinDependenciaEncima_EsFailClosed()
    {
        // EL CASO DEL ADDENDUM: sin Dependencia por encima -> SIN dependencia, es decir SIN
        // visibilidad documental. Jamas visibilidad total.
        Assert.Null(OrgUnitTree.ResolveDependenciaId(CargoHuerfano, Arbol()));
    }

    [Fact]
    public void NodoInexistente_EsFailClosed()
    {
        // Usuario sin Cargo, o Cargo que ya no esta en el conjunto cargado.
        Assert.Null(OrgUnitTree.ResolveDependenciaId(TestIds.Next(), Arbol()));
    }

    [Fact]
    public void ArbolCorrupto_NoSeCuelga_YResuelveFailClosed()
    {
        // Datos corruptos: dos Cargos que se apuntan mutuamente y ninguna Dependencia arriba.
        var a = TestIds.Next();
        var b = TestIds.Next();
        var corrupto = new Dictionary<long, OrgUnitTree.NodeRef>
        {
            [a] = new(a, b, OrgUnitClassifier.Cargo),
            [b] = new(b, a, OrgUnitClassifier.Cargo)
        };
        Assert.Null(OrgUnitTree.ResolveDependenciaId(a, corrupto));
    }

    [Fact]
    public void CadenaLarga_ResuelveSinLimiteDeProfundidad()
    {
        // Jerarquia ILIMITADA (RF03): 200 dependencias anidadas y un Cargo al fondo.
        var nodes = new Dictionary<long, OrgUnitTree.NodeRef>();
        long? padre = null;
        long ultimaDependencia = 0;
        for (var i = 0; i < 200; i++)
        {
            var id = TestIds.Next();
            nodes[id] = new(id, padre, OrgUnitClassifier.Dependencia);
            padre = id;
            ultimaDependencia = id;
        }
        var cargo = TestIds.Next();
        nodes[cargo] = new(cargo, padre, OrgUnitClassifier.Cargo);

        Assert.Equal(ultimaDependencia, OrgUnitTree.ResolveDependenciaId(cargo, nodes));
    }

    // ---- Subarbol arrastrado por un movimiento ----

    [Fact]
    public void DescendantsAndSelf_IncluyeElNodoYTodoSuSubarbol()
    {
        var parents = Arbol().ToDictionary(kv => kv.Key, kv => kv.Value.ParentId);
        var subtree = OrgUnitTree.DescendantsAndSelf(CargoJefe, parents);

        // Mover el CargoJefe arrastra a su Funcionario, pero no al CargoAuxiliar hermano.
        Assert.Contains(CargoJefe, subtree);
        Assert.Contains(Funcionario, subtree);
        Assert.DoesNotContain(CargoAuxiliar, subtree);
        Assert.DoesNotContain(Oficina, subtree);
    }

    [Fact]
    public void DescendantsAndSelf_ConCicloPreexistente_NoSeCuelga()
    {
        var a = TestIds.Next();
        var b = TestIds.Next();
        var corrupto = new Dictionary<long, long?> { [a] = b, [b] = a };
        var subtree = OrgUnitTree.DescendantsAndSelf(a, corrupto);
        Assert.Equal(2, subtree.Count);
    }
}
