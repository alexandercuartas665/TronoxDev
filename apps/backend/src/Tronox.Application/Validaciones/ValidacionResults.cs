namespace Tronox.Application.Validaciones;

/// <summary>Resultado tipado de los servicios de validaciones (RQ04 - RF11/RF12).</summary>
public enum ValidacionServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict,
    Forbidden
}

public sealed record ValidacionResult<T>(ValidacionServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == ValidacionServiceStatus.Ok;

    public static ValidacionResult<T> Ok(T value) => new(ValidacionServiceStatus.Ok, value, null);
    public static ValidacionResult<T> NotFound(string? error = null) => new(ValidacionServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static ValidacionResult<T> Invalid(string error) => new(ValidacionServiceStatus.Invalid, default, error);
    public static ValidacionResult<T> Conflict(string error) => new(ValidacionServiceStatus.Conflict, default, error);
    public static ValidacionResult<T> Forbidden(string error) => new(ValidacionServiceStatus.Forbidden, default, error);
}
