namespace Tronox.Application.Listas;

/// <summary>
/// Resultado tipado de los servicios del administrador de listas (RQ02 - RF03), mismo patron que
/// SerieResult / TrdVersionResult: sin excepciones crudas hacia la presentacion.
/// </summary>
public enum ListaServiceStatus
{
    Ok = 0,
    /// <summary>La lista u opcion no existe (o pertenece a otro tenant: el filtro global la oculta).</summary>
    NotFound,
    /// <summary>Datos invalidos (validacion de negocio).</summary>
    Invalid,
    /// <summary>Conflicto de unicidad (nombre de lista por tenant, clave de opcion por lista).</summary>
    Conflict
}

public sealed record ListaResult<T>(ListaServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == ListaServiceStatus.Ok;

    public static ListaResult<T> Ok(T value) => new(ListaServiceStatus.Ok, value, null);
    public static ListaResult<T> NotFound(string? error = null) => new(ListaServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static ListaResult<T> Invalid(string error) => new(ListaServiceStatus.Invalid, default, error);
    public static ListaResult<T> Conflict(string error) => new(ListaServiceStatus.Conflict, default, error);

    /// <summary>Reetiqueta un resultado de FALLO a otro tipo de valor, conservando estado y mensaje.</summary>
    public ListaResult<TOther> To<TOther>() => Status == ListaServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new ListaResult<TOther>(Status, default, Error);
}
