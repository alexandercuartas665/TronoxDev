using Tronox.Application.Topografia;

namespace Tronox.Application.Tests;

/// <summary>
/// Logica PURA de la topografia fisica (RQ02 - RF06), sin base de datos: codigo topografico, regla
/// de capacidad, jerarquia por orden, ciclos y ocupacion.
/// </summary>
public class TopografiaRulesTests
{
    // ---- Validaciones de campos ----

    [Fact]
    public void Nivel_Valido() => Assert.Null(TopografiaRules.ValidateNivel("Caja", "CAJ", 4));

    [Theory]
    [InlineData(null, "CAJ", 1)]
    [InlineData("Caja", null, 1)]
    [InlineData("Caja", "CAJ", 0)]
    public void Nivel_Invalido(string? nombre, string? sigla, int orden)
        => Assert.NotNull(TopografiaRules.ValidateNivel(nombre, sigla, orden));

    [Fact]
    public void Elemento_Valido() => Assert.Null(TopografiaRules.ValidateElemento("Bodega Norte", "NOR"));

    [Theory]
    [InlineData(null, "NOR")]
    [InlineData("Bodega", null)]
    public void Elemento_Invalido(string? nombre, string? sigla)
        => Assert.NotNull(TopografiaRules.ValidateElemento(nombre, sigla));

    // ---- Controla capacidad (RF06 3.6.6-2) ----

    [Fact]
    public void ControlaCapacidad_SoloUno()
    {
        var otros = new List<(int, bool)> { (1, false), (2, true) }; // ya hay uno con capacidad
        Assert.NotNull(TopografiaRules.ValidateControlaCapacidad(3, otros));
    }

    [Fact]
    public void ControlaCapacidad_DebeSerMayorOrden()
    {
        var otros = new List<(int, bool)> { (1, false), (2, false), (4, false) }; // el mayor es 4
        // Candidato con orden 3 (< 4): no puede controlar.
        Assert.NotNull(TopografiaRules.ValidateControlaCapacidad(3, otros));
        // Candidato con orden 4 (= mayor): valido.
        var otros2 = new List<(int, bool)> { (1, false), (2, false), (3, false) };
        Assert.Null(TopografiaRules.ValidateControlaCapacidad(4, otros2));
    }

    // ---- Codigo topografico (RF06 3.6.3) ----

    [Fact]
    public void CodigoTopografico_ConcatenaSiglasRaizAHoja()
    {
        // Arbol: 1 NOR (raiz) -> 2 EST05 -> 3 ENT03 -> 4 CAJ010
        var arbol = new Dictionary<long, (long? Parent, string Sigla)>
        {
            [1] = (null, "NOR"),
            [2] = (1, "EST05"),
            [3] = (2, "ENT03"),
            [4] = (3, "CAJ010"),
        };
        Assert.Equal("NOR", TopografiaRules.CodigoTopografico(1, arbol));
        Assert.Equal("NOR-EST05", TopografiaRules.CodigoTopografico(2, arbol));
        Assert.Equal("NOR-EST05-ENT03-CAJ010", TopografiaRules.CodigoTopografico(4, arbol));
    }

    // ---- Jerarquia por orden ----

    [Fact]
    public void Jerarquia_HijoDebeSerOrdenMayor()
    {
        Assert.Null(TopografiaRules.ValidateJerarquia(null, 1));   // raiz: cualquier nivel
        Assert.Null(TopografiaRules.ValidateJerarquia(2, 3));       // hijo orden 3 bajo padre orden 2: ok
        Assert.NotNull(TopografiaRules.ValidateJerarquia(3, 3));    // igual orden: invalido
        Assert.NotNull(TopografiaRules.ValidateJerarquia(3, 2));    // hijo orden menor: invalido
    }

    // ---- Ciclos y ocupacion ----

    [Fact]
    public void Ciclo_DetectaAncestroPropio()
    {
        var parent = new Dictionary<long, long?> { [1] = null, [2] = 1, [3] = 2 };
        Assert.True(TopografiaRules.WouldCreateCycle(1, 3, parent)); // 1 no puede colgar de su nieto
        Assert.False(TopografiaRules.WouldCreateCycle(3, 1, parent));
    }

    [Theory]
    [InlineData(20, 20, true)]
    [InlineData(18, 20, false)]
    [InlineData(21, 20, true)]
    [InlineData(5, null, false)] // sin capacidad nunca esta lleno
    public void EstaLleno_SegunOcupacionYCapacidad(int hijos, int? capacidad, bool esperado)
        => Assert.Equal(esperado, TopografiaRules.EstaLleno(hijos, capacidad));
}
