using Tronox.Application.Forms;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS del validador servidor de formularios dinamicos (RQ08, port ECOREX). Sin EF: valida
/// por tipo de control (requerido, longitud, patron, rango, opcion valida).
/// </summary>
public class FormFieldValidatorTests
{
    [Fact]
    public void NoInput_NuncaValida()
    {
        Assert.Null(FormFieldValidator.Validate(FormControlType.Heading, required: true, value: null));
        Assert.Null(FormFieldValidator.Validate(FormControlType.Divider, required: true, value: null));
    }

    [Fact]
    public void Requerido_VacioFalla()
    {
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Text, required: true, value: ""));
        Assert.Null(FormFieldValidator.Validate(FormControlType.Text, required: false, value: ""));
        Assert.Null(FormFieldValidator.Validate(FormControlType.Text, required: true, value: "hola"));
    }

    [Fact]
    public void Texto_RespetaLongitudYPatron()
    {
        var rules = new FormValidationRules(MinLength: 3, MaxLength: 5, Pattern: "^[a-z]+$");
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Text, true, "ab", rules: rules));
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Text, true, "abcdef", rules: rules));
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Text, true, "AB1", rules: rules));
        Assert.Null(FormFieldValidator.Validate(FormControlType.Text, true, "abcd", rules: rules));
    }

    [Fact]
    public void Numero_ValidaRango()
    {
        var rules = new FormValidationRules(MinValue: 1, MaxValue: 10);
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Number, true, "abc", rules: rules));
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Number, true, "0", rules: rules));
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Number, true, "11", rules: rules));
        Assert.Null(FormFieldValidator.Validate(FormControlType.Number, true, "5", rules: rules));
    }

    [Fact]
    public void Select_ExigeOpcionValida()
    {
        var opts = new List<FormOption> { new("a", "A"), new("b", "B") };
        Assert.NotNull(FormFieldValidator.Validate(FormControlType.Select, true, "z", opts));
        Assert.Null(FormFieldValidator.Validate(FormControlType.Select, true, "a", opts));
    }

    [Fact]
    public void ParseOptions_ToleraJsonInvalido()
    {
        Assert.Empty(FormFieldValidator.ParseOptions("no es json"));
        Assert.Empty(FormFieldValidator.ParseOptions(null));
        Assert.Single(FormFieldValidator.ParseOptions("[{\"id\":\"a\",\"label\":\"A\"}]"));
    }
}
