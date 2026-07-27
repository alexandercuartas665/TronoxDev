namespace Tronox.Application.SeriesDocumentales;

/// <summary>
/// Resultado tipado de los servicios del catalogo de series (RQ02 - RF02), mismo patron que
/// OrgResult / ArchivisticaResults: sin excepciones crudas hacia la presentacion.
/// </summary>
public enum SerieServiceStatus
{
    Ok = 0,
    /// <summary>La serie no existe (o pertenece a otro tenant: el filtro global la oculta).</summary>
    NotFound,
    /// <summary>Datos invalidos (validacion de negocio, ej. ciclo en el arbol).</summary>
    Invalid,
    /// <summary>Conflicto de unicidad (codigo por nivel, nombre por padre).</summary>
    Conflict
}

public sealed record SerieResult<T>(SerieServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == SerieServiceStatus.Ok;

    public static SerieResult<T> Ok(T value) => new(SerieServiceStatus.Ok, value, null);
    public static SerieResult<T> NotFound(string? error = null) => new(SerieServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static SerieResult<T> Invalid(string error) => new(SerieServiceStatus.Invalid, default, error);
    public static SerieResult<T> Conflict(string error) => new(SerieServiceStatus.Conflict, default, error);

    /// <summary>Reetiqueta un resultado de FALLO a otro tipo de valor, conservando estado y mensaje.</summary>
    public SerieResult<TOther> To<TOther>() => Status == SerieServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new SerieResult<TOther>(Status, default, Error);
}
