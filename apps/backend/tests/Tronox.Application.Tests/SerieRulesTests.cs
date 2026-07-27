using Tronox.Application.SeriesDocumentales;

namespace Tronox.Application.Tests;

/// <summary>
/// Validacion PURA del catalogo de series (RQ02 - RF02), sin base de datos: campos obligatorios y
/// acotados, y el algoritmo de deteccion de ciclos del arbol autorreferencial.
/// </summary>
public class SerieRulesTests
{
    // ---- ValidateSerie ----

    [Fact]
    public void SerieValida_NoDaError()
        => Assert.Null(SerieRules.ValidateSerie("01.1", "Actas de Comite", "Descripcion opcional"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CodigoVacio_EsInvalido(string? codigo)
        => Assert.NotNull(SerieRules.ValidateSerie(codigo, "Actas", null));

    [Fact]
    public void CodigoLargo_EsInvalido()
        => Assert.NotNull(SerieRules.ValidateSerie(new string('0', 21), "Actas", null));

    [Fact]
    public void NombreVacio_EsInvalido()
        => Assert.NotNull(SerieRules.ValidateSerie("01", "  ", null));

    [Fact]
    public void NombreLargo_EsInvalido()
        => Assert.NotNull(SerieRules.ValidateSerie("01", new string('A', 201), null));

    [Fact]
    public void DescripcionLarga_EsInvalida()
        => Assert.NotNull(SerieRules.ValidateSerie("01", "Actas", new string('D', 501)));

    // ---- WouldCreateCycle ----
    // Arbol de prueba: 1 (raiz) -> 2 -> 3 ; 4 (raiz suelta).

    private static readonly Dictionary<long, long?> Arbol = new()
    {
        [1] = null,
        [2] = 1,
        [3] = 2,
        [4] = null
    };

    [Fact]
    public void ColgarNodoDeSiMismo_EsCiclo()
        => Assert.True(SerieRules.WouldCreateCycle(2, 2, Arbol));

    [Fact]
    public void ColgarAncestroDeSuDescendiente_EsCiclo()
        => Assert.True(SerieRules.WouldCreateCycle(1, 3, Arbol)); // 1 no puede colgar de su nieto 3

    [Fact]
    public void MoverAUnaRaizAjena_NoEsCiclo()
        => Assert.False(SerieRules.WouldCreateCycle(2, 4, Arbol));

    [Fact]
    public void MoverARaiz_NoEsCiclo()
        => Assert.False(SerieRules.WouldCreateCycle(3, null, Arbol));
}
