using Tronox.Application.Expedientes;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS (sin base de datos) de las reglas de expedientes (RQ03): codigo estructurado (RF04),
/// fecha de apertura (RF03), herencia de clasificacion "solo elevar" (RF10) y obligatoriedad de
/// metadatos (DAT-04). Lo que necesita base de datos (unicidad de codigo, consecutivo concurrente,
/// filtro fail-closed real) se cubre en Tronox.Integration.Tests.
/// </summary>
public class ExpedienteRulesTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 4);

    [Fact]
    public void ComponerCodigo_ArmaFormatoEstructurado()
    {
        var codigo = ExpedienteRules.ComponerCodigo("GTH", "0102", 2026, "000045");
        Assert.Equal("GTH-0102-2026-000045", codigo);
    }

    [Fact]
    public void SequenceCode_EsPorAnio()
    {
        Assert.Equal("EXP-2026", ExpedienteRules.SequenceCode(2026));
        Assert.NotEqual(ExpedienteRules.SequenceCode(2025), ExpedienteRules.SequenceCode(2026));
    }

    [Fact]
    public void ValidateNombre_Obligatorio()
    {
        Assert.NotNull(ExpedienteRules.ValidateNombre(""));
        Assert.NotNull(ExpedienteRules.ValidateNombre("   "));
        Assert.Null(ExpedienteRules.ValidateNombre("Contrato 045"));
    }

    [Fact]
    public void ValidateNombre_NoSupera200()
    {
        Assert.NotNull(ExpedienteRules.ValidateNombre(new string('a', 201)));
        Assert.Null(ExpedienteRules.ValidateNombre(new string('a', 200)));
    }

    [Fact]
    public void ValidateFechaApertura_NoFutura()
    {
        Assert.NotNull(ExpedienteRules.ValidateFechaApertura(Hoy.AddDays(1), Hoy));
        Assert.Null(ExpedienteRules.ValidateFechaApertura(Hoy, Hoy));
        Assert.Null(ExpedienteRules.ValidateFechaApertura(Hoy.AddDays(-10), Hoy));
    }

    [Theory]
    [InlineData(2, 2, true)]   // igual al heredado: permitido
    [InlineData(2, 3, true)]   // elevar: permitido
    [InlineData(2, 1, false)]  // bajar: prohibido
    public void PuedeElevar_SoloIgualOMayor(int heredado, int elegido, bool esperado)
    {
        Assert.Equal(esperado, ExpedienteRules.PuedeElevar(heredado, elegido));
    }

    [Fact]
    public void ValidateMetadatosObligatorios_DetectaFaltante()
    {
        var defs = new (long, string, bool)[]
        {
            (10, "Numero de contrato", true),
            (11, "Observaciones", false)
        };
        var valores = new Dictionary<long, string?> { [11] = "nota" };
        var error = ExpedienteRules.ValidateMetadatosObligatorios(defs, valores);
        Assert.NotNull(error);
        Assert.Contains("Numero de contrato", error);
    }

    [Fact]
    public void ValidateMetadatosObligatorios_PasaCuandoCompleto()
    {
        var defs = new (long, string, bool)[] { (10, "Numero de contrato", true) };
        var valores = new Dictionary<long, string?> { [10] = "045" };
        Assert.Null(ExpedienteRules.ValidateMetadatosObligatorios(defs, valores));
    }
}
