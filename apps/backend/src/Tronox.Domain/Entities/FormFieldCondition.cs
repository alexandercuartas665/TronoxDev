using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Regla condicional AUTOCONTENIDA de un formulario dinamico (RQ08). Reemplaza el vinculo a un motor
/// de Reglas externo (podado en TRONOX): el diseñador arma "Si el campo [SourceFieldCode] [Operator]
/// [Value] entonces [Action] el campo [TargetFieldCode]", y el renderer la evalua al cambiar el campo
/// fuente para mostrar/ocultar/requerir el campo destino (o fijarle un valor). FK a la definicion en
/// cascada. TENANT-SCOPED.
/// </summary>
public class FormFieldCondition : TenantEntity
{
    public long DefinitionId { get; set; }
    public FormDefinition? Definition { get; set; }

    /// <summary>Campo cuyo cambio dispara la evaluacion (FieldCode).</summary>
    public string SourceFieldCode { get; set; } = null!;

    /// <summary>Operador: equals | notEquals | empty | notEmpty | contains | gt | lt.</summary>
    public string Operator { get; set; } = "equals";

    /// <summary>Valor de comparacion (no aplica a empty/notEmpty).</summary>
    public string? Value { get; set; }

    /// <summary>Accion sobre el campo destino: show | hide | require | optional | setValue.</summary>
    public string Action { get; set; } = "show";

    /// <summary>Campo afectado por la accion (FieldCode).</summary>
    public string TargetFieldCode { get; set; } = null!;

    /// <summary>Valor a fijar cuando Action = setValue.</summary>
    public string? SetValue { get; set; }

    public int SortOrder { get; set; }
}
