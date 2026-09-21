using Tronox.Application.Forms;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests del evaluador de condiciones AUTOCONTENIDO de formularios (RQ08). Reemplaza el motor de
/// Reglas externo (podado): dada una lista de reglas y los valores actuales, calcula que campos se
/// ocultan/requieren/fijan. Puro, sin EF.
/// </summary>
public class FormConditionEvaluatorTests
{
    private static FormFieldConditionDto Cond(string src, string op, string? val, string action, string target, string? set = null, int order = 0)
        => new(order, src, op, val, action, target, set, order);

    [Fact]
    public void Equals_Hide_OcultaElDestino()
    {
        var conds = new[] { Cond("tipo", "equals", "juridica", "hide", "cedula") };
        var eff = FormConditionEvaluator.Evaluate(conds, new Dictionary<string, string?> { ["tipo"] = "juridica" });
        Assert.Contains("cedula", eff.Hidden);
    }

    [Fact]
    public void Equals_NoCoincide_NoOculta()
    {
        var conds = new[] { Cond("tipo", "equals", "juridica", "hide", "cedula") };
        var eff = FormConditionEvaluator.Evaluate(conds, new Dictionary<string, string?> { ["tipo"] = "natural" });
        Assert.DoesNotContain("cedula", eff.Hidden);
    }

    [Fact]
    public void NotEmpty_Require_ExigeElDestino()
    {
        var conds = new[] { Cond("otro", "notEmpty", null, "require", "detalle") };
        var eff = FormConditionEvaluator.Evaluate(conds, new Dictionary<string, string?> { ["otro"] = "algo" });
        Assert.Contains("detalle", eff.Required);
    }

    [Fact]
    public void Gt_SetValue_FijaValor()
    {
        var conds = new[] { Cond("monto", "gt", "1000000", "setValue", "requiereAprobacion", set: "true") };
        var eff = FormConditionEvaluator.Evaluate(conds, new Dictionary<string, string?> { ["monto"] = "2000000" });
        Assert.True(eff.SetValues.TryGetValue("requiereAprobacion", out var v) && v == "true");
    }

    [Fact]
    public void TriggerFields_DevuelveOrigenesDistintos()
    {
        var conds = new[]
        {
            Cond("a", "equals", "x", "hide", "b"),
            Cond("a", "equals", "y", "show", "c"),
            Cond("d", "notEmpty", null, "require", "e"),
        };
        var triggers = FormConditionEvaluator.TriggerFields(conds);
        Assert.Equal(2, triggers.Count);
        Assert.Contains("a", triggers);
        Assert.Contains("d", triggers);
    }
}
