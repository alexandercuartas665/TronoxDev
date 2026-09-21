namespace Tronox.Application.Plantillas;

/// <summary>Resultado tipado de los servicios de plantillas documentales (RQ04 - RF09).</summary>
public enum PlantillaServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict
}

public sealed record PlantillaResult<T>(PlantillaServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == PlantillaServiceStatus.Ok;

    public static PlantillaResult<T> Ok(T value) => new(PlantillaServiceStatus.Ok, value, null);
    public static PlantillaResult<T> NotFound(string? error = null) => new(PlantillaServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static PlantillaResult<T> Invalid(string error) => new(PlantillaServiceStatus.Invalid, default, error);
    public static PlantillaResult<T> Conflict(string error) => new(PlantillaServiceStatus.Conflict, default, error);
}
