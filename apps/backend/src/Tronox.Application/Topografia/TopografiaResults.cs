namespace Tronox.Application.Topografia;

/// <summary>
/// Resultado tipado de los servicios de topografia fisica (RQ02 - RF06), mismo patron que los demas
/// modulos: sin excepciones crudas hacia la presentacion.
/// </summary>
public enum TopografiaServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict
}

public sealed record TopografiaResult<T>(TopografiaServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == TopografiaServiceStatus.Ok;

    public static TopografiaResult<T> Ok(T value) => new(TopografiaServiceStatus.Ok, value, null);
    public static TopografiaResult<T> NotFound(string? error = null) => new(TopografiaServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static TopografiaResult<T> Invalid(string error) => new(TopografiaServiceStatus.Invalid, default, error);
    public static TopografiaResult<T> Conflict(string error) => new(TopografiaServiceStatus.Conflict, default, error);

    public TopografiaResult<TOther> To<TOther>() => Status == TopografiaServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new TopografiaResult<TOther>(Status, default, Error);
}
