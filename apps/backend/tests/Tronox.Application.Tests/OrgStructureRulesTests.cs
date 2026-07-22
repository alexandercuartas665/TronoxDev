using Tronox.Application.Organization;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Validacion PURA de un nodo del arbol organizacional (RF03/RF04, ADR-003), sin base de datos.
/// Lo que importa fijar aqui es que las obligaciones son POR CLASIFICADOR: fondo solo en
/// Dependencia, nivel jerarquico solo en Cargo.
/// </summary>
public class OrgStructureRulesTests
{
    private const long FondoId = 77;
    private static readonly DateOnly Desde = new(2024, 1, 1);

    private static string? Dependencia(
        long? fondoId = FondoId, string? codigo = "DG", DateOnly? desde = null, DateOnly? hasta = null,
        string? nombre = "Direccion General")
        => OrgStructureRules.ValidateNode(
            OrgUnitClassifier.Dependencia, nombre, null, fondoId, codigo,
            desde ?? Desde, hasta, null, null, null, null);

    private static string? Cargo(
        NivelJerarquico? nivel = NivelJerarquico.Profesional, string? codigoCargo = null,
        string? codigoDafp = null)
        => OrgStructureRules.ValidateNode(
            OrgUnitClassifier.Cargo, "Profesional Universitario", null, null, null,
            null, null, codigoCargo, codigoDafp, nivel, null);

    // ---- fondo_id: OBLIGATORIO solo en Dependencia ----

    [Fact]
    public void Dependencia_SinFondo_EsInvalida()
    {
        var error = Dependencia(fondoId: null);
        Assert.NotNull(error);
        Assert.Contains("fondo", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dependencia_ConFondo_EsValida()
    {
        Assert.Null(Dependencia());
    }

    [Fact]
    public void Cargo_SinFondo_EsValido()
    {
        // fondo_id NO aplica al Cargo: exigirselo seria replicar la regla en el nodo equivocado.
        Assert.Null(Cargo());
    }

    [Fact]
    public void Funcionario_SinFondo_EsValido_PeroExigeOcupante()
    {
        var sinUsuario = OrgStructureRules.ValidateNode(
            OrgUnitClassifier.Funcionario, "Juan Perez", null, null, null,
            null, null, null, null, null, null);
        Assert.NotNull(sinUsuario);

        var conUsuario = OrgStructureRules.ValidateNode(
            OrgUnitClassifier.Funcionario, "Juan Perez", null, null, null,
            null, null, null, null, null, 42);
        Assert.Null(conUsuario);
    }

    // ---- Resto de atributos de Dependencia (RF03) ----

    [Fact]
    public void Dependencia_SinCodigo_EsInvalida()
    {
        Assert.NotNull(Dependencia(codigo: "  "));
    }

    [Fact]
    public void Dependencia_CodigoDeMasDeVeinte_EsInvalida()
    {
        Assert.NotNull(Dependencia(codigo: new string('X', 21)));
        Assert.Null(Dependencia(codigo: new string('X', 20)));
    }

    [Fact]
    public void Dependencia_SinVigenteDesde_EsInvalida()
    {
        var error = OrgStructureRules.ValidateNode(
            OrgUnitClassifier.Dependencia, "Direccion", null, FondoId, "DG",
            null, null, null, null, null, null);
        Assert.NotNull(error);
        Assert.Contains("vigencia", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dependencia_VigenteHastaAnteriorADesde_EsInvalida()
    {
        Assert.NotNull(Dependencia(hasta: new DateOnly(2023, 12, 31)));
        // NULL en vigente_hasta significa "sigue vigente", no "sin dato".
        Assert.Null(Dependencia(hasta: null));
    }

    [Fact]
    public void Nombre_DeMasDeDoscientos_EsInvalido()
    {
        Assert.NotNull(Dependencia(nombre: new string('N', 201)));
        Assert.Null(Dependencia(nombre: new string('N', 200)));
    }

    // ---- Cargo (RF04) ----

    [Fact]
    public void Cargo_SinNivelJerarquico_EsInvalido()
    {
        var error = Cargo(nivel: null);
        Assert.NotNull(error);
        Assert.Contains("nivel", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cargo_CodigosSonOpcionales_PeroAcotadosAVeinte()
    {
        // codigo_dafp solo aplica a entidades publicas: por eso es opcional.
        Assert.Null(Cargo(codigoCargo: null, codigoDafp: null));
        Assert.Null(Cargo(codigoCargo: "PU-01", codigoDafp: "219"));
        Assert.NotNull(Cargo(codigoCargo: new string('C', 21)));
        Assert.NotNull(Cargo(codigoDafp: new string('D', 21)));
    }

    [Fact]
    public void TodosLosNivelesJerarquicosDeLaSpecSonValidos()
    {
        foreach (var nivel in new[]
        {
            NivelJerarquico.Directivo, NivelJerarquico.Asesor, NivelJerarquico.Profesional,
            NivelJerarquico.Tecnico, NivelJerarquico.Asistencial
        })
        {
            Assert.Null(Cargo(nivel));
        }
    }

    // ---- Coherencia padre -> hijo ----

    [Fact]
    public void Cargo_BajoDependencia_OBajoRaiz_EsValido()
    {
        Assert.Null(OrgStructureRules.ValidateParent(OrgUnitClassifier.Cargo, OrgUnitClassifier.Dependencia));
        // Colgar un Cargo de la raiz se PERMITE: es el caso fail-closed del Addendum, no un error.
        Assert.Null(OrgStructureRules.ValidateParent(OrgUnitClassifier.Cargo, null));
    }

    [Fact]
    public void Cargo_BajoOtroCargo_EsInvalido()
    {
        Assert.NotNull(OrgStructureRules.ValidateParent(OrgUnitClassifier.Cargo, OrgUnitClassifier.Cargo));
    }

    [Fact]
    public void Dependencia_BajoDependencia_EsValida_ProfundidadIlimitada()
    {
        Assert.Null(OrgStructureRules.ValidateParent(OrgUnitClassifier.Dependencia, OrgUnitClassifier.Dependencia));
        Assert.Null(OrgStructureRules.ValidateParent(OrgUnitClassifier.Dependencia, null));
        Assert.NotNull(OrgStructureRules.ValidateParent(OrgUnitClassifier.Dependencia, OrgUnitClassifier.Cargo));
    }

    [Fact]
    public void Funcionario_DebeColgarDeUnCargo()
    {
        Assert.Null(OrgStructureRules.ValidateParent(OrgUnitClassifier.Funcionario, OrgUnitClassifier.Cargo));
        Assert.NotNull(OrgStructureRules.ValidateParent(OrgUnitClassifier.Funcionario, OrgUnitClassifier.Dependencia));
    }
}
