using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Pregunta (campo) de un formulario dinamico (RQ08, port ECOREX / ADR-0015+ADR-0021). FieldCode es
/// la clave del campo dentro del documento JSON de respuestas ({ fieldCode: { value, type } }) y es
/// unica por definicion. FK a la definicion en cascada; al contenedor NO ACTION. TENANT-SCOPED.
///
/// Primer slice: campos de diseño/captura/validacion. Se difieren lookups (SourceKind/AutofillMap),
/// maestro-detalle (Subform), campos calculados (CalcExpression), cascada y permisos por campo.
/// </summary>
public class FormQuestion : TenantEntity
{
    public long DefinitionId { get; set; }
    public FormDefinition? Definition { get; set; }

    /// <summary>Contenedor al que pertenece (null = raiz del formulario).</summary>
    public long? ContainerId { get; set; }
    public FormContainer? Container { get; set; }

    /// <summary>Clave del campo en el JSON de respuestas. Unica por definicion.</summary>
    public string FieldCode { get; set; } = null!;

    public string Label { get; set; } = null!;

    /// <summary>Subtitulo corto bajo la etiqueta.</summary>
    public string? Caption { get; set; }

    /// <summary>Texto de ayuda (tooltip / hint).</summary>
    public string? HelpText { get; set; }

    public FormControlType ControlType { get; set; } = FormControlType.Text;

    /// <summary>Opciones para Select/MultiCheck/Radio: [{"id","label","value"}] (jsonb).</summary>
    public string? OptionsJson { get; set; }

    public bool Required { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Columna del grid bootstrap del renderer (ej. "col-md-6"). Sincronizada con Width.</summary>
    public string GridCol { get; set; } = "col-12";

    /// <summary>Numeral impreso junto a la etiqueta (ej. "2.1").</summary>
    public string? Numeral { get; set; }

    /// <summary>Reglas de validacion: {"minLength","maxLength","pattern","minValue","maxValue"} (jsonb).</summary>
    public string? ValidationJson { get; set; }

    /// <summary>Ancho en columnas de la grilla de 12 del constructor (1..12). Fuente de verdad del layout.</summary>
    public int Width { get; set; } = 12;

    /// <summary>Placeholder del input.</summary>
    public string? PlaceholderText { get; set; }

    /// <summary>Valor por defecto. Doble uso: texto en Paragraph, alto en px en Spacer, valor inicial en captura.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Fijo en el layout: el constructor no permite reordenarlo.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Oculto: no se pinta en el renderer y no valida requerido.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Formato/mascara de presentacion (currency | percent | integer | phone | ...). Null = sin formato.</summary>
    public string? Format { get; set; }
}
