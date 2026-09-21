using Tronox.Application.Plantillas;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS de las reglas de plantillas documentales (RQ04 - RF09): nombre, conteo de variables y
/// el catalogo base (con Terceros deshabilitado hasta RQ07, DAT-02).
/// </summary>
public class PlantillaRulesTests
{
    [Fact]
    public void ValidateNombre_Obligatorio()
    {
        Assert.NotNull(PlantillaRules.ValidateNombre(""));
        Assert.NotNull(PlantillaRules.ValidateNombre(new string('a', 201)));
        Assert.Null(PlantillaRules.ValidateNombre("Oficio estandar"));
    }

    [Fact]
    public void ContarVariables_CuentaDistintas()
    {
        Assert.Equal(0, PlantillaRules.ContarVariables(null));
        Assert.Equal(0, PlantillaRules.ContarVariables("<p>Sin variables</p>"));
        Assert.Equal(2, PlantillaRules.ContarVariables("{{Fecha_Actual}} y {{Entidad}}"));
        // Repetidas cuentan una sola vez (case-insensitive).
        Assert.Equal(1, PlantillaRules.ContarVariables("{{Entidad}} {{entidad}} {{ENTIDAD}}"));
    }

    [Fact]
    public void VariablesBase_TercerosDeshabilitado()
    {
        var vars = PlantillaRules.VariablesBase();
        var terceros = vars.Where(v => v.Grupo == "Terceros").ToList();
        Assert.NotEmpty(terceros);
        Assert.All(terceros, v => Assert.False(v.Habilitada));
        // Las de sistema/expediente/firma estan habilitadas.
        Assert.Contains(vars, v => v.Token == "{{Fecha_Actual}}" && v.Habilitada);
        Assert.Contains(vars, v => v.Token == "{{Firma}}" && v.Habilitada);
    }
}
