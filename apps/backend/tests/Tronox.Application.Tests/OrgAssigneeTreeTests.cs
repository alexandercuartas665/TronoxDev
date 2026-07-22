using Tronox.Application.Organization;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Logica PURA del resolver de candidatos por nodo (asignacion por nodo, ADR-0035, ola F1).
/// Cubre: un Cargo resuelve sus Funcionarios; una Dependencia resuelve todos los funcionarios
/// descendientes; miembros y responsable se incluyen; sin unidades = vacio; distinct entre
/// varias unidades; tolerancia a ciclos en datos.
/// Arbol: Comercial(Dep) -> Asesor(Cargo) -> Ana(Func), Beto(Func); Finanzas(Dep) -> Aprobador(Cargo) -> Caro(Func).
/// </summary>
public class OrgAssigneeTreeTests
{
    private static readonly long Comercial = TestIds.Next();
    private static readonly long Asesor = TestIds.Next();
    private static readonly long FuncAna = TestIds.Next();
    private static readonly long FuncBeto = TestIds.Next();
    private static readonly long Finanzas = TestIds.Next();
    private static readonly long Aprobador = TestIds.Next();
    private static readonly long FuncCaro = TestIds.Next();

    private static readonly long UserAna = TestIds.Next();
    private static readonly long UserBeto = TestIds.Next();
    private static readonly long UserCaro = TestIds.Next();
    private static readonly long UserResp = TestIds.Next();
    private static readonly long UserMember = TestIds.Next();

    private static List<OrgAssigneeTree.UnitRow> Units(long? asesorResponsible = null) =>
    [
        new(Comercial, null, OrgUnitClassifier.Dependencia, null, null),
        new(Asesor, Comercial, OrgUnitClassifier.Cargo, asesorResponsible, null),
        new(FuncAna, Asesor, OrgUnitClassifier.Funcionario, null, UserAna),
        new(FuncBeto, Asesor, OrgUnitClassifier.Funcionario, null, UserBeto),
        new(Finanzas, null, OrgUnitClassifier.Dependencia, null, null),
        new(Aprobador, Finanzas, OrgUnitClassifier.Cargo, null, null),
        new(FuncCaro, Aprobador, OrgUnitClassifier.Funcionario, null, UserCaro)
    ];

    [Fact]
    public void Cargo_ResolvesItsFunctionaries()
    {
        var result = OrgAssigneeTree.ResolveForUnit(Asesor, Units(), []);
        Assert.Equal(2, result.Count);
        Assert.Contains(UserAna, result);
        Assert.Contains(UserBeto, result);
        Assert.DoesNotContain(UserCaro, result);
    }

    [Fact]
    public void Dependencia_ResolvesAllDescendantFunctionaries()
    {
        var result = OrgAssigneeTree.ResolveForUnit(Comercial, Units(), []);
        Assert.Equal(2, result.Count);
        Assert.Contains(UserAna, result);
        Assert.Contains(UserBeto, result);
    }

    [Fact]
    public void Members_And_Responsible_AreIncluded()
    {
        var members = new List<OrgAssigneeTree.MemberRow> { new(Asesor, UserMember) };
        var result = OrgAssigneeTree.ResolveForUnit(Asesor, Units(asesorResponsible: UserResp), members);
        Assert.Contains(UserAna, result);
        Assert.Contains(UserBeto, result);
        Assert.Contains(UserMember, result);
        Assert.Contains(UserResp, result);
    }

    [Fact]
    public void NoUnits_ResolvesEmpty()
    {
        var result = OrgAssigneeTree.ResolveForUnit(TestIds.Next(), Units(), []);
        Assert.Empty(result);
    }

    [Fact]
    public void MultipleUnits_AreDistinct()
    {
        // Ambas dependencias resuelven sus funcionarios sin duplicar entre si.
        var result = OrgAssigneeTree.ResolveForUnits([Comercial, Finanzas], Units(), []);
        Assert.Equal(3, result.Count);
        Assert.Contains(UserAna, result);
        Assert.Contains(UserBeto, result);
        Assert.Contains(UserCaro, result);
    }

    [Fact]
    public void OverlappingUnits_DoNotDuplicate()
    {
        // Comercial (padre) y Asesor (hijo) comparten a Ana y Beto: sin duplicados.
        var result = OrgAssigneeTree.ResolveForUnits([Comercial, Asesor], Units(), []);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CyclicData_DoesNotHang()
    {
        // Datos corruptos: A -> B -> A, ambos con funcionario. El recorrido termina.
        var a = TestIds.Next();
        var b = TestIds.Next();
        var units = new List<OrgAssigneeTree.UnitRow>
        {
            new(a, b, OrgUnitClassifier.Funcionario, null, UserAna),
            new(b, a, OrgUnitClassifier.Funcionario, null, UserBeto)
        };
        var result = OrgAssigneeTree.ResolveForUnit(a, units, []);
        Assert.Equal(2, result.Count);
    }
}
