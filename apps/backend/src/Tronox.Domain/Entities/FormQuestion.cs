using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Pregunta (campo) de un formulario dinamico (RQ08, port ECOREX / ADR-0015+ADR-0021). FieldCode es
/// la clave del campo dentro del documento JSON de respuestas ({ fieldCode: { value, type } }) y es
/// unica por definicion. FK a la definicion en cascada; al contenedor NO ACTION. TENANT-SCOPED.
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

    // ---- Constructor ----

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

    // ---- Origen de datos / lookup (ola F1) ----

    /// <summary>
    /// De donde salen las opciones/valores del campo: fijas (OptionsJson, default) o desde una fuente
    /// con dominio del tenant (contenedor, terceros). Persistido como string.
    /// </summary>
    public FormSourceKind SourceKind { get; set; } = FormSourceKind.Options;

    /// <summary>Referencia de la fuente cuando SourceKind != Options (id o clave). String para admitir ambos.</summary>
    public string? SourceRef { get; set; }

    /// <summary>Campo de la fuente que se MUESTRA al usuario (ej. "Nombre").</summary>
    public string? DisplayField { get; set; }

    /// <summary>Campo de la fuente que se GUARDA como valor del campo (el id).</summary>
    public string? ValueField { get; set; }

    /// <summary>Scope del catalogo como JSON (ej. {"EsCliente": true}). Siempre acotado al tenant. jsonb.</summary>
    public string? FilterJson { get; set; }

    /// <summary>
    /// Mapa de autollenado como JSON: campo de la fuente -> campo del formulario. Al elegir una opcion
    /// se COPIAN (snapshot) esos valores a los campos destino. jsonb.
    /// </summary>
    public string? AutofillMapJson { get; set; }

    /// <summary>Como se ofrece el catalogo al llenar: autocompletar (default), lista o buscador modal.</summary>
    public FormFieldPresentation Presentation { get; set; } = FormFieldPresentation.Autocomplete;

    /// <summary>
    /// Maestro-detalle (ola F5): definicion HIJA que este campo Subform embebe. Los registros hijos se
    /// enlazan al padre por FormRecordLink. Null salvo en campos Subform.
    /// </summary>
    public long? SubformDefinitionId { get; set; }

    // ---- Transversales (ola F6) ----

    /// <summary>Valor por defecto DINAMICO (Hoy / Usuario actual / ...), resuelto al abrir a llenar.</summary>
    public FormDefaultDynamic DefaultDynamic { get; set; } = FormDefaultDynamic.None;

    /// <summary>Formato/mascara de presentacion (currency | percent | integer | phone | ...). Null = sin formato.</summary>
    public string? Format { get; set; }

    /// <summary>
    /// Permisos por campo como JSON: { "hide": ["Advisor"], "readonly": ["Supervisor"] } (nombres de rol).
    /// Un rol en hide no ve el campo; en readonly lo ve pero no lo edita. Null = sin restriccion.
    /// </summary>
    public string? FieldVisibilityJson { get; set; }

    // ---- Calculo y agregacion (ola F2) ----

    /// <summary>
    /// Expresion de campo calculado sobre otros campos, ej. "{cantidad} * {precio}". Evaluada en un
    /// sandbox tipado (allow-list, sin codigo arbitrario); en el render el campo es de solo lectura.
    /// </summary>
    public string? CalcExpression { get; set; }

    /// <summary>
    /// Agregado de la columna cuando la pregunta es una columna de un GridDetail: pinta la fila de
    /// totales y alimenta el roll-up al encabezado. None = sin total.
    /// </summary>
    public FormAggregate Aggregate { get; set; } = FormAggregate.None;

    // ---- Configurador en cascada (motor generico, config-driven) ----

    /// <summary>
    /// Configuracion del control CascadeConfigurator como JSON (jsonb): niveles en cascada, sus opciones,
    /// juegos de columnas y mapeo rama->tabla. Campo aparte (no reusa OptionsJson).
    /// </summary>
    public string? CascadeConfigJson { get; set; }
}
