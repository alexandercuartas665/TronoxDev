using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Respuesta de un formulario dinamico como DOCUMENTO JSON (RQ08, port ECOREX / ADR-0015: se abandona
/// el EAV por-fila). Data = { fieldCode: { value, type } } en jsonb. Reference ancla la respuesta a un
/// caso externo (ej. numero de expediente o de tarea). TENANT-SCOPED, concurrencia optimista portable.
///
/// Primer slice: ciclo Draft -> Submitted. Se difieren los campos de registro transaccional
/// (RecordNumber, RecordStatus, void) y la conversion a documento/vinculacion a expediente.
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
}
