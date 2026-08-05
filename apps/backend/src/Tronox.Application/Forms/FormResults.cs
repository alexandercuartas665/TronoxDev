namespace Tronox.Application.Forms;

/// <summary>
/// Resultado tipado de los servicios de formularios dinamicos (RQ08, port ECOREX / ADR-0013): sin
/// excepciones crudas hacia la presentacion. ValidationFailed lleva errores POR CAMPO
/// (fieldCode -> mensaje) para que el renderer los pinte bajo cada control.
/// </summary>
public enum FormServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict,
    ValidationFailed
}

public sealed record FormResult<T>(
    FormServiceStatus Status, T? Value, string? Error,
    IReadOnlyDictionary<string, string>? FieldErrors = null)
{
    public bool IsOk => Status == FormServiceStatus.Ok;

    public static FormResult<T> Ok(T value) => new(FormServiceStatus.Ok, value, null);
    public static FormResult<T> NotFound(string? error = null) => new(FormServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static FormResult<T> Invalid(string error) => new(FormServiceStatus.Invalid, default, error);
    public static FormResult<T> Conflict(string error) => new(FormServiceStatus.Conflict, default, error);
    public static FormResult<T> ValidationFailed(IReadOnlyDictionary<string, string> fieldErrors)
        => new(FormServiceStatus.ValidationFailed, default, "La respuesta tiene campos invalidos.", fieldErrors);
}
