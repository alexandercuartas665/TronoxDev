using System.Text.RegularExpressions;

namespace Tronox.Application.Plantillas;

/// <summary>
/// Reglas puras de plantillas documentales (RQ04 - RF09): validacion de nombre, conteo de variables y
/// el catalogo base de variables (Sistema/Expediente/Firma/Terceros). Sin EF: testeable sin base.
/// </summary>
public static partial class PlantillaRules
{
    [GeneratedRegex(@"\{\{([^{}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariableRegex();

    public static string? ValidateNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre de la plantilla es obligatorio."; }
        return nombre.Trim().Length > 200 ? "El nombre no puede superar 200 caracteres." : null;
    }

    /// <summary>Cuenta variables {{...}} DISTINTAS en el contenido (informativo, RF09).</summary>
    public static int ContarVariables(string? contenidoHtml)
    {
        if (string.IsNullOrEmpty(contenidoHtml)) { return 0; }
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in VariableRegex().Matches(contenidoHtml))
        {
            set.Add(m.Groups[1].Value.Trim());
        }
        return set.Count;
    }

    /// <summary>
    /// Catalogo BASE de variables (RF09). Las de Terceros estan visibles pero DESHABILITADAS hasta el
    /// modulo de Terceros (RQ07, DAT-02). Los metadatos de la tipologia se agregan aparte (dinamicos).
    /// </summary>
    public static IReadOnlyList<VariableDto> VariablesBase() =>
    [
        new("Sistema", "{{Fecha_Actual}}", "Fecha actual", true),
        new("Sistema", "{{Usuario_Actual}}", "Usuario actual", true),
        new("Sistema", "{{Dependencia}}", "Dependencia", true),
        new("Sistema", "{{Entidad}}", "Entidad", true),
        new("Expediente", "{{Codigo_Expediente}}", "Código del expediente", true),
        new("Expediente", "{{Nombre_Expediente}}", "Nombre del expediente", true),
        new("Expediente", "{{Serie}}", "Serie", true),
        new("Expediente", "{{Subserie}}", "Subserie", true),
        new("Firma", "{{Firma}}", "Zona de firma (RQ05)", true),
        new("Terceros", "{{Nombre_Tercero}}", "Nombre del tercero (requiere RQ07)", false),
        new("Terceros", "{{NIT}}", "NIT del tercero (requiere RQ07)", false),
        new("Terceros", "{{Representante_Legal}}", "Representante legal (requiere RQ07)", false)
    ];
}
