namespace Tronox.Application.TrdVersiones;

/// <summary>
/// Resultado tipado de los servicios de versiones de TRD (RQ02 - RF01), mismo patron que
/// SerieResult / OrgResult: sin excepciones crudas hacia la presentacion.
/// </summary>
public enum TrdVersionServiceStatus
{
    Ok = 0,
    /// <summary>La version no existe (o pertenece a otro tenant: el filtro global la oculta).</summary>
    NotFound,
    /// <summary>Datos invalidos o transicion de estado no permitida.</summary>
    Invalid,
    /// <summary>Conflicto de unicidad (codigo_version ya usado en el tenant).</summary>
    Conflict
}

public sealed record TrdVersionResult<T>(TrdVersionServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == TrdVersionServiceStatus.Ok;

    public static TrdVersionResult<T> Ok(T value) => new(TrdVersionServiceStatus.Ok, value, null);
    public static TrdVersionResult<T> NotFound(string? error = null) => new(TrdVersionServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static TrdVersionResult<T> Invalid(string error) => new(TrdVersionServiceStatus.Invalid, default, error);
    public static TrdVersionResult<T> Conflict(string error) => new(TrdVersionServiceStatus.Conflict, default, error);

    /// <summary>Reetiqueta un resultado de FALLO a otro tipo de valor, conservando estado y mensaje.</summary>
    public TrdVersionResult<TOther> To<TOther>() => Status == TrdVersionServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new TrdVersionResult<TOther>(Status, default, Error);
}
