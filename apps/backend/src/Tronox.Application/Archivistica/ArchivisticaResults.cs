namespace Tronox.Application.Archivistica;

/// <summary>
/// Resultado tipado de los servicios de configuracion archivistica (mismo patron que
/// RolResults / OrgResults): sin excepciones crudas hacia la presentacion.
/// </summary>
public enum ArchivisticaServiceStatus
{
    Ok = 0,
    /// <summary>La entidad no existe (o pertenece a otro tenant: el filtro global la oculta).</summary>
    NotFound,
    /// <summary>Datos invalidos o regla de negocio violada (ej. fondo Cerrado en solo lectura).</summary>
    Invalid,
    /// <summary>Conflicto de unicidad (ej. codigo_fondo ya usado dentro del tenant).</summary>
    Conflict
}

public sealed record ArchivisticaResult<T>(ArchivisticaServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == ArchivisticaServiceStatus.Ok;

    public static ArchivisticaResult<T> Ok(T value) => new(ArchivisticaServiceStatus.Ok, value, null);
    public static ArchivisticaResult<T> NotFound(string? error = null) => new(ArchivisticaServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static ArchivisticaResult<T> Invalid(string error) => new(ArchivisticaServiceStatus.Invalid, default, error);
    public static ArchivisticaResult<T> Conflict(string error) => new(ArchivisticaServiceStatus.Conflict, default, error);
}
