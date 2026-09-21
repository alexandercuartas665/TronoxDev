using System.Globalization;

namespace Tronox.Application.Forms;

/// <summary>
/// Efectos de UI resultantes de evaluar las reglas condicionales de un formulario sobre los valores
/// actuales. <see cref="Hidden"/>/<see cref="Required"/>/<see cref="Optional"/> son conjuntos de
/// FieldCode; <see cref="SetValues"/> son valores a fijar (FieldCode -> valor).
/// </summary>
public sealed record FormConditionEffects(
    IReadOnlySet<string> Hidden,
    IReadOnlySet<string> Required,
    IReadOnlySet<string> Optional,
    IReadOnlyDictionary<string, string?> SetValues);

/// <summary>
/// Motor de reglas condicionales AUTOCONTENIDO del formulario dinamico (RQ08). Reemplaza al
/// FormRuleDispatcher de ECOREX, que delegaba en un motor de Reglas externo (podado en TRONOX).
/// Es PURO (sin EF, sin estado, sin IO): dada la lista de reglas y el diccionario de valores
/// actuales (FieldCode -> valor) calcula el estado de UI de forma DETERMINISTA, procesando las
/// reglas en orden de <see cref="FormFieldConditionDto.SortOrder"/>. Testeable sin base de datos.
/// </summary>
public static class FormConditionEvaluator
{
    /// <summary>
    /// Evalua las reglas contra los valores actuales y devuelve los efectos agregados. Las reglas se
    /// procesan ordenadas por SortOrder para que el resultado sea deterministico (una accion posterior
    /// puede revertir a una anterior, ej. show retira un hide previo).
    /// </summary>
    public static FormConditionEffects Evaluate(
        IReadOnlyList<FormFieldConditionDto> conditions,
        IReadOnlyDictionary<string, string?> values)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);
        var optional = new HashSet<string>(StringComparer.Ordinal);
        var setValues = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (conditions is null || conditions.Count == 0)
        {
            return new FormConditionEffects(hidden, required, optional, setValues);
        }

        var ordered = conditions.OrderBy(c => c.SortOrder).ThenBy(c => c.Id);
        foreach (var cond in ordered)
        {
            var current = values is not null && values.TryGetValue(cond.SourceFieldCode, out var v) ? v : null;
            if (!Matches(cond.Operator, current, cond.Value))
            {
                continue;
            }

            var target = cond.TargetFieldCode;
            switch ((cond.Action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hide":
                    hidden.Add(target);
                    break;
                case "show":
                    // show gana sobre un hide anterior (se procesa en orden).
                    hidden.Remove(target);
                    break;
                case "require":
                    required.Add(target);
                    optional.Remove(target);
                    break;
                case "optional":
                    optional.Add(target);
                    required.Remove(target);
                    break;
                case "setvalue":
                    setValues[target] = cond.SetValue;
                    break;
            }
        }

        return new FormConditionEffects(hidden, required, optional, setValues);
    }

    /// <summary>
    /// Campos fuente distintos de las reglas (los que el renderer debe observar para reaccionar a un
    /// cambio y re-evaluar). Preserva el primer orden de aparicion.
    /// </summary>
    public static IReadOnlyList<string> TriggerFields(IReadOnlyList<FormFieldConditionDto> conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return Array.Empty<string>();
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var c in conditions)
        {
            if (!string.IsNullOrEmpty(c.SourceFieldCode) && seen.Add(c.SourceFieldCode))
            {
                result.Add(c.SourceFieldCode);
            }
        }
        return result;
    }

    // ---- Semantica de operadores (case-insensitive) ----

    private static bool Matches(string? op, string? current, string? expected)
    {
        switch ((op ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "equals":
                return string.Equals(current, expected, StringComparison.Ordinal);
            case "notequals":
                return !string.Equals(current, expected, StringComparison.Ordinal);
            case "empty":
                return string.IsNullOrWhiteSpace(current);
            case "notempty":
                return !string.IsNullOrWhiteSpace(current);
            case "contains":
                return current is not null && expected is not null
                    && current.Contains(expected, StringComparison.OrdinalIgnoreCase);
            case "gt":
                return TryDecimal(current, out var cg) && TryDecimal(expected, out var eg) && cg > eg;
            case "lt":
                return TryDecimal(current, out var cl) && TryDecimal(expected, out var el) && cl < el;
            default:
                return false;
        }
    }

    private static bool TryDecimal(string? s, out decimal value)
        => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
}
