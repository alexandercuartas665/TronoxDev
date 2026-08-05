using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Respuesta de un formulario dinamico como DOCUMENTO JSON (RQ08, port ECOREX / ADR-0015: se abandona
/// el EAV por-fila). Data = { fieldCode: { value, type } } en jsonb. Reference ancla la respuesta a un
/// caso externo (ej. numero de expediente o de tarea). TENANT-SCOPED, concurrencia optimista portable.
/// </summary>
public class FormResponse : TenantEntity, IVersioned
{
    public long DefinitionId { get; set; }
    public FormDefinition? Definition { get; set; }

    /// <summary>Referencia externa (ej. "EXP-100-150-2026-000001"). Null = respuesta suelta.</summary>
    public string? Reference { get; set; }

    public FormResponseStatus Status { get; set; } = FormResponseStatus.Draft;

    /// <summary>Documento JSON { fieldCode: { value, type } } (jsonb).</summary>
    public string Data { get; set; } = "{}";

    public DateTimeOffset? SubmittedAt { get; set; }

    public long? SubmittedByTenantUserId { get; set; }

    /// <summary>Token de concurrencia optimista portable (lo incrementa el interceptor).</summary>
    public long Version { get; set; }

    // ---- Registro transaccional (ola F3). Solo aplica cuando la definicion es transaccional;
    // INDEPENDIENTE de Status (Draft/Submitted, ciclo de envio). ----

    /// <summary>Numero/clave del registro (consecutivo o clave natural). Null hasta confirmar.</summary>
    public string? RecordNumber { get; set; }

    /// <summary>Ciclo del registro: Draft -> Confirmed -> Voided (anular no borra ni libera el numero).</summary>
    public FormRecordStatus RecordStatus { get; set; } = FormRecordStatus.Draft;

    /// <summary>Fecha del hecho (por sistema al confirmar; los campos fecha del formulario son aparte).</summary>
    public DateTimeOffset? TransactionDate { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }
    public long? VoidedByTenantUserId { get; set; }
    public string? VoidReason { get; set; }
}
