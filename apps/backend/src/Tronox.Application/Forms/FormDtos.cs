using Tronox.Domain.Enums;

namespace Tronox.Application.Forms;

// ---- Formularios dinamicos (RQ08, port ECOREX / ADR-0015). Primer slice: DTOs del nucleo
// (definicion + arbol + captura). Se difieren token/modulo/transaccional/lookups/subform. ----

/// <summary>Opcion de un control Select/MultiCheck/Radio ([{id,label,value}] en OptionsJson).</summary>
public sealed record FormOption(string Id, string Label, string? Value = null);

/// <summary>Reglas de validacion declaradas en ValidationJson de la pregunta.</summary>
public sealed record FormValidationRules(
    int? MinLength = null, int? MaxLength = null, string? Pattern = null,
    decimal? MinValue = null, decimal? MaxValue = null);

/// <summary>Valor de un campo en el documento de respuesta: { fieldCode: { value, type } }.</summary>
public sealed record FormFieldValue(string? Value, string Type);

public sealed record FormDefinitionListItemDto(
    long Id, string Code, string Title, string? Description, FormStatus Status,
    int Revision, bool IsArchived, int QuestionCount, long Version, int ResponseCount = 0);

public sealed record FormContainerDto(
    long Id, string Name, FormContainerType ContainerType, long? ParentId,
    int SortOrder, string? Style, string? TabsJson = null, int Width = 12,
    bool IsLocked = false, bool IsHidden = false, bool InlineLabels = false);

public sealed record FormQuestionDto(
    long Id, long? ContainerId, string FieldCode, string Label, string? Caption,
    string? HelpText, FormControlType ControlType, string? OptionsJson, bool Required,
    int SortOrder, string GridCol, string? Numeral, string? ValidationJson,
    int Width = 12, string? PlaceholderText = null, string? DefaultValue = null,
    bool IsLocked = false, bool IsHidden = false, string? Format = null);

public sealed record FormDefinitionDetailDto(
    long Id, string Code, string Title, string? Description, FormStatus Status,
    int Revision, bool IsArchived, long Version,
    IReadOnlyList<FormContainerDto> Containers,
    IReadOnlyList<FormQuestionDto> Questions);

public sealed record CreateFormDefinitionRequest(string Code, string Title, string? Description = null);

/// <summary>Version es el token de concurrencia optimista leido por el cliente (ADR-0013).</summary>
public sealed record UpdateFormDefinitionRequest(string Title, string? Description, long Version);

public sealed record SaveFormContainerRequest(
    string Name, FormContainerType ContainerType = FormContainerType.Segment,
    long? ParentId = null, string? Style = null, string? TabsJson = null, int Width = 12,
    bool IsLocked = false, bool IsHidden = false, bool InlineLabels = false);

/// <summary>
/// Width (1..12) es la fuente del layout del constructor (ADR-0021). GridCol se sincroniza desde
/// Width (col-12 / col-md-N) para no romper el renderer bootstrap.
/// </summary>
public sealed record SaveFormQuestionRequest(
    long? ContainerId, string FieldCode, string Label, FormControlType ControlType,
    string? Caption = null, string? HelpText = null, string? OptionsJson = null,
    bool Required = false, string GridCol = "col-12", string? Numeral = null,
    string? ValidationJson = null, int Width = 12, string? PlaceholderText = null,
    string? DefaultValue = null, bool IsLocked = false, bool IsHidden = false, string? Format = null);

public sealed record FormResponseDto(
    long Id, long DefinitionId, string? Reference, FormResponseStatus Status,
    IReadOnlyDictionary<string, FormFieldValue> Data,
    DateTimeOffset? SubmittedAt, long? SubmittedByTenantUserId, long Version);

/// <summary>Fila de la bandeja de respuestas de un formulario.</summary>
public sealed record FormResponseListItemDto(
    long Id, string? Reference, FormResponseStatus Status,
    DateTimeOffset? SubmittedAt, DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string?> Fields);
