namespace Tronox.Application.Expedientes;

/// <summary>
/// Resultado tipado de los servicios de expedientes (RQ03), mismo patron que los demas modulos:
/// nunca excepciones crudas hacia la presentacion.
/// </summary>
public enum ExpedienteServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict,
    Forbidden
}

public sealed record ExpedienteResult<T>(ExpedienteServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == ExpedienteServiceStatus.Ok;

    public static ExpedienteResult<T> Ok(T value) => new(ExpedienteServiceStatus.Ok, value, null);
    public static ExpedienteResult<T> NotFound(string? error = null) => new(ExpedienteServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static ExpedienteResult<T> Invalid(string error) => new(ExpedienteServiceStatus.Invalid, default, error);
    public static ExpedienteResult<T> Conflict(string error) => new(ExpedienteServiceStatus.Conflict, default, error);
    public static ExpedienteResult<T> Forbidden(string error) => new(ExpedienteServiceStatus.Forbidden, default, error);
}
