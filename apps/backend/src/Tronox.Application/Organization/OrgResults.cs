namespace Tronox.Application.Organization;

/// <summary>
/// Resultado tipado de los servicios de organizacion (mismo patron que RuleResults /
/// TaskCoreResults, ADR-0013/0016/0017): sin excepciones crudas hacia la presentacion.
/// </summary>
public enum OrgServiceStatus
{
    Ok = 0,
    /// <summary>La entidad no existe (o pertenece a otro tenant: el filtro global la oculta).</summary>
    NotFound,
    /// <summary>Datos invalidos (validacion de negocio, ej. ciclo en el arbol).</summary>
    Invalid,
    /// <summary>Conflicto (unicidad, ej. miembro ya asignado a la unidad).</summary>
    Conflict
}

public sealed record OrgResult<T>(OrgServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == OrgServiceStatus.Ok;

    public static OrgResult<T> Ok(T value) => new(OrgServiceStatus.Ok, value, null);
    public static OrgResult<T> NotFound(string? error = null) => new(OrgServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static OrgResult<T> Invalid(string error) => new(OrgServiceStatus.Invalid, default, error);
    public static OrgResult<T> Conflict(string error) => new(OrgServiceStatus.Conflict, default, error);

    /// <summary>
    /// Reetiqueta un resultado de FALLO a otro tipo de valor, conservando estado y mensaje.
    /// Permite escribir la validacion una sola vez y devolverla desde metodos que retornan
    /// DTOs distintos. Un Ok no se puede reetiquetar (no habria valor que trasladar).
    /// </summary>
    public OrgResult<TOther> To<TOther>() => Status == OrgServiceStatus.Ok
        ? throw new InvalidOperationException("Solo se reetiquetan resultados de fallo.")
        : new OrgResult<TOther>(Status, default, Error);
}
