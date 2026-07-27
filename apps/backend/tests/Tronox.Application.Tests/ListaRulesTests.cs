using Tronox.Application.Listas;

namespace Tronox.Application.Tests;

/// <summary>
/// Validacion PURA del administrador de listas (RQ02 - RF03), sin base de datos: campos
/// obligatorios y acotados, y la regla de usabilidad (>= 2 opciones activas).
/// </summary>
public class ListaRulesTests
{
    [Fact]
    public void ListaValida_NoDaError()
        => Assert.Null(ListaRules.ValidateLista("Tipo de Vinculacion", "Opcional"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NombreVacio_EsInvalido(string? nombre)
        => Assert.NotNull(ListaRules.ValidateLista(nombre, null));

    [Fact]
    public void NombreLargo_EsInvalido()
        => Assert.NotNull(ListaRules.ValidateLista(new string('N', 101), null));

    [Fact]
    public void DescripcionLarga_EsInvalida()
        => Assert.NotNull(ListaRules.ValidateLista("Lista", new string('D', 301)));

    [Fact]
    public void OpcionValida_NoDaError()
        => Assert.Null(ListaRules.ValidateOpcion("LIBRE_REMOCION", "Libre Remocion"));

    [Theory]
    [InlineData(null, "Valor")]
    [InlineData("CLAVE", null)]
    [InlineData("   ", "Valor")]
    [InlineData("CLAVE", "  ")]
    public void OpcionConCampoVacio_EsInvalida(string? clave, string? valor)
        => Assert.NotNull(ListaRules.ValidateOpcion(clave, valor));

    [Fact]
    public void ClaveLarga_EsInvalida()
        => Assert.NotNull(ListaRules.ValidateOpcion(new string('C', 51), "Valor"));

    [Fact]
    public void ValorLargo_EsInvalido()
        => Assert.NotNull(ListaRules.ValidateOpcion("CLAVE", new string('V', 201)));

    // ---- Usabilidad (RF03 3.3.4-2) ----

    [Theory]
    [InlineData(true, 2, true)]   // activa + 2 activas -> usable
    [InlineData(true, 3, true)]
    [InlineData(true, 1, false)]  // solo 1 activa -> no usable
    [InlineData(true, 0, false)]
    [InlineData(false, 5, false)] // inactiva -> nunca usable
    public void EsUsable_ExigeActivaYDosOpcionesActivas(bool activa, int opcionesActivas, bool esperado)
        => Assert.Equal(esperado, ListaRules.EsUsable(activa, opcionesActivas));
}
