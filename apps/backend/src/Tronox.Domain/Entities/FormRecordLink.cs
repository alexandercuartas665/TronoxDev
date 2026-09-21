using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Enlace maestro-detalle entre registros de formulario (RQ08, ola F5): un campo Subform del
/// formulario padre agrupa N registros HIJOS (respuestas de otra definicion). A diferencia del
/// GridDetail (filas embebidas en el jsonb del padre), cada hijo es un <see cref="FormResponse"/>
/// propio, reportable/consultable aparte. TENANT-SCOPED.
/// </summary>
public class FormRecordLink : TenantEntity
{
    /// <summary>Registro padre (la respuesta que contiene el campo Subform).</summary>
    public long ParentResponseId { get; set; }
    public FormResponse? ParentResponse { get; set; }

    /// <summary>FieldCode del campo Subform en el padre (un padre puede tener varios subformularios).</summary>
    public string ParentFieldCode { get; set; } = null!;

    /// <summary>Registro hijo (respuesta de la definicion hija).</summary>
    public long ChildResponseId { get; set; }
    public FormResponse? ChildResponse { get; set; }

    public int SortOrder { get; set; }
}
