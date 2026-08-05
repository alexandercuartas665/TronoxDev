using Tronox.Domain.Enums;

namespace Tronox.Application.Forms;

// ---- Formularios dinamicos (RQ08, port ECOREX / ADR-0015) ----

/// <summary>Opcion de un control Select/MultiCheck/Radio ([{id,label,value}] en OptionsJson).</summary>
public sealed record FormOption(string Id, string Label, string? Value = null);

/// <summary>Reglas de validacion declaradas en ValidationJson de la pregunta.</summary>
public sealed record FormValidationRules(
    int? MinLength = null, int? MaxLength = null, string? Pattern = null,
    decimal? MinValue = null, decimal? MaxValue = null);

/// <summary>Valor de un campo en el documento de respuesta: { fieldCode: { value, type } }.</summary>
public sealed record FormFieldValue(string? Value, string Type);

/// <summary>ResponseCount y RuleCount alimentan los KPIs del indice.</summary>
public sealed record FormDefinitionListItemDto(
    long Id, string Code, string Title, string? Description, FormStatus Status,
    int Revision, bool IsArchived, int QuestionCount, long Version,
    int ResponseCount = 0, int RuleCount = 0);

public sealed record FormContainerDto(
    long Id, string Name, FormContainerType ContainerType, long? ParentId,
    int SortOrder, string? Style,
    string? TabsJson = null, int Width = 12, bool IsLocked = false, bool IsHidden = false,
    bool InlineLabels = false);

public sealed record FormQuestionDto(
    long Id, long? ContainerId, string FieldCode, string Label, string? Caption,
    string? HelpText, FormControlType ControlType, string? OptionsJson, bool Required,
    int SortOrder, string GridCol, string? Numeral, string? ValidationJson,
    int Width = 12, string? PlaceholderText = null, string? DefaultValue = null,
    bool IsLocked = false, bool IsHidden = false,
    // Origen de datos / lookup (ola F1).
    FormSourceKind SourceKind = FormSourceKind.Options, string? SourceRef = null,
    string? DisplayField = null, string? ValueField = null, string? FilterJson = null,
    string? AutofillMapJson = null, FormFieldPresentation Presentation = FormFieldPresentation.Autocomplete,
    // Calculo / agregacion (ola F2).
    string? CalcExpression = null, FormAggregate Aggregate = FormAggregate.None,
    // Maestro-detalle (ola F5): definicion hija del campo Subform.
    long? SubformDefinitionId = null,
    // Transversales (ola F6): default dinamico + formato + permisos por campo.
    FormDefaultDynamic DefaultDynamic = FormDefaultDynamic.None, string? Format = null,
    string? FieldVisibilityJson = null,
    // Configurador en cascada (motor generico): taxonomia de la pregunta CascadeConfigurator.
    string? CascadeConfigJson = null);

public sealed record FormDefinitionDetailDto(
    long Id, string Code, string Title, string? Description, FormStatus Status,
    int Revision, bool IsArchived, long Version,
    IReadOnlyList<FormContainerDto> Containers,
    IReadOnlyList<FormQuestionDto> Questions,
    // Transaccionalidad (ola F3).
    bool IsTransactional = false, FormIdentityMode IdentityMode = FormIdentityMode.None,
    string? IdentitySourceFieldCode = null,
    // Formulario como modulo (ola F4).
    bool IsModule = false, string? ModuleIcon = null,
    string? ListColumnsJson = null, string? FilterFieldsJson = null,
    // Ancho de la tarjeta al llenar (Normal/Ancho/Completo). Configurable por formulario.
    FormCardLayout CardLayout = FormCardLayout.Normal);

/// <summary>Config transaccional de la definicion (ola F3): panel "Propiedades del formulario".
/// Lleva ademas el ancho de tarjeta (CardLayout), que vive en el mismo panel.</summary>
public sealed record SetFormTransactionalRequest(
    bool IsTransactional, FormIdentityMode IdentityMode, string? IdentitySourceFieldCode,
    FormCardLayout CardLayout = FormCardLayout.Normal);

/// <summary>Fila de la bandeja del formulario-modulo (ola F4): un registro enviado. Fields son los
/// valores de campo (fieldCode -> valor) para las columnas configurables de la bandeja / BI.</summary>
public sealed record FormRecordListItemDto(
    long Id, string? RecordNumber, FormRecordStatus RecordStatus,
    DateTimeOffset? TransactionDate, DateTimeOffset? SubmittedAt, string? Reference,
    IReadOnlyDictionary<string, string?> Fields);

/// <summary>Config de formulario-modulo (ola F4). Al promover, el usuario elige la vista de menu y el
/// grupo padre DONDE colgar el modulo; el icono es opcional. ListColumns y FilterFields son field
/// codes para las columnas/filtros de la bandeja.</summary>
public sealed record SetFormModuleRequest(
    bool IsModule, long? MenuViewId, long? ParentNodeId, string? Icon,
    IReadOnlyList<string>? ListColumns = null, IReadOnlyList<string>? FilterFields = null,
    string? MenuLabel = null);

public sealed record CreateFormDefinitionRequest(string Code, string Title, string? Description = null);

/// <summary>Version es el token de concurrencia optimista leido por el cliente.</summary>
public sealed record UpdateFormDefinitionRequest(string Title, string? Description, long Version);

public sealed record SaveFormContainerRequest(
    string Name, FormContainerType ContainerType = FormContainerType.Segment,
    long? ParentId = null, string? Style = null,
    string? TabsJson = null, int Width = 12, bool IsLocked = false, bool IsHidden = false,
    bool InlineLabels = false);

/// <summary>
/// Width (1..12) es la fuente del layout del constructor. Si viene en 12 (el default) y GridCol trae
/// una columna bootstrap parseable, Width se deriva de GridCol; en cualquier otro caso GridCol se
/// sincroniza desde Width (col-12 / col-md-N).
/// </summary>
public sealed record SaveFormQuestionRequest(
    long? ContainerId, string FieldCode, string Label, FormControlType ControlType,
    string? Caption = null, string? HelpText = null, string? OptionsJson = null,
    bool Required = false, string GridCol = "col-12", string? Numeral = null,
    string? ValidationJson = null,
    int Width = 12, string? PlaceholderText = null, string? DefaultValue = null,
    bool IsLocked = false, bool IsHidden = false,
    // Origen de datos / lookup (ola F1).
    FormSourceKind SourceKind = FormSourceKind.Options, string? SourceRef = null,
    string? DisplayField = null, string? ValueField = null, string? FilterJson = null,
    string? AutofillMapJson = null, FormFieldPresentation Presentation = FormFieldPresentation.Autocomplete,
    // Calculo / agregacion (ola F2).
    string? CalcExpression = null, FormAggregate Aggregate = FormAggregate.None,
    // Maestro-detalle (ola F5): definicion hija del campo Subform.
    long? SubformDefinitionId = null,
    // Transversales (ola F6): default dinamico + formato + permisos por campo.
    FormDefaultDynamic DefaultDynamic = FormDefaultDynamic.None, string? Format = null,
    string? FieldVisibilityJson = null,
    // Configurador en cascada (motor generico): taxonomia de la pregunta CascadeConfigurator.
    string? CascadeConfigJson = null);

public sealed record FormResponseDto(
    long Id, long DefinitionId, string? Reference, FormResponseStatus Status,
    IReadOnlyDictionary<string, FormFieldValue> Data,
    DateTimeOffset? SubmittedAt, long? SubmittedByTenantUserId, long Version,
    // Registro transaccional (ola F3).
    string? RecordNumber = null, FormRecordStatus RecordStatus = FormRecordStatus.Draft,
    DateTimeOffset? TransactionDate = null);

/// <summary>Fila de la bandeja simple de respuestas de un formulario (no-modulo). Fields aplana el
/// documento JSON a fieldCode -> valor para las columnas de la bandeja.</summary>
public sealed record FormResponseListItemDto(
    long Id, string? Reference, FormResponseStatus Status, DateTimeOffset? SubmittedAt,
    DateTimeOffset CreatedAt, IReadOnlyDictionary<string, string?> Fields);

/// <summary>Regla condicional autocontenida (D4) para el diseñador y el renderer.</summary>
public sealed record FormFieldConditionDto(
    long Id, string SourceFieldCode, string Operator, string? Value,
    string Action, string TargetFieldCode, string? SetValue, int SortOrder);

public sealed record SaveFormFieldConditionRequest(
    string SourceFieldCode, string Operator, string? Value,
    string Action, string TargetFieldCode, string? SetValue = null);

/// <summary>Opciones de emision de un token de publicacion por URL.</summary>
public sealed record EmitFormTokenRequest(
    string? Reference = null, int ExpirationHours = 24,
    bool SingleUse = false, bool AllowAnonymous = true);

/// <summary>El Token viaja EN CLARO una unica vez (solo se persiste el hash SHA-256).</summary>
public sealed record EmitFormTokenResult(long TokenId, string Token, DateTimeOffset ExpiresAt);

public sealed record FormTokenDto(
    long Id, long DefinitionId, string? Reference, DateTimeOffset ExpiresAt,
    bool SingleUse, DateTimeOffset? UsedAt, DateTimeOffset? RevokedAt,
    bool AllowAnonymous, DateTimeOffset CreatedAt);

/// <summary>
/// Resultado de validar un token del visor publico. Cuando IsValid es false NO se expone el motivo
/// para no filtrar informacion: el visor muestra siempre el mismo mensaje neutro.
/// </summary>
public sealed record FormTokenValidation(
    bool IsValid, long? TokenId = null, long? TenantId = null, long? DefinitionId = null,
    string? Reference = null, bool SingleUse = false, bool AllowAnonymous = false)
{
    public static readonly FormTokenValidation Invalid = new(false);
}

/// <summary>
/// Formulario exigido por un paso current del flujo de una tarea (RQ08 x RQ11). Si el nodo tiene una
/// compuerta exclusiva adelante (IsGatewayAhead), ApprovalOptions trae las salidas del gateway: la UI
/// pide esa decision JUNTO al formulario y la propaga al enviar.
/// </summary>
public sealed record TaskStepFormDto(
    long ResponseId, long DefinitionId, string FormCode, string FormTitle,
    long WorkflowInstanceId, long WorkflowNodeId, string? NodeName,
    FormFlowLinkStatus LinkStatus, FormResponseStatus ResponseStatus, string? Reference,
    bool IsGatewayAhead = false, IReadOnlyList<string>? ApprovalOptions = null);
