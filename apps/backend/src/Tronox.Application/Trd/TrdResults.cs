namespace Tronox.Application.Trd;

/// <summary>
/// Resultado tipado de los servicios de construccion de la TRD (RQ02 - RF04), mismo patron que los
/// demas modulos: sin excepciones crudas hacia la presentacion.
/// </summary>
public enum TrdServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict
}

public sealed record TrdResult<T>(TrdServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == TrdServiceStatus.Ok;

    public static TrdResult<T> Ok(T value) => new(TrdServiceStatus.Ok, value, null);
    public static TrdResult<T> NotFound(string? error = null) => new(TrdServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static TrdResult<T> Invalid(string error) => new(TrdServiceStatus.Invalid, default, error);
    public static TrdResult<T> Conflict(string error) => new(TrdServiceStatus.Conflict, default, error);

    public TrdResult<TOther> To<TOther>() => Status == TrdServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new TrdResult<TOther>(Status, default, Error);
}
