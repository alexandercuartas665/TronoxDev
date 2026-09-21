namespace Tronox.Application.Radicacion;

/// <summary>Resultado tipado de los servicios de Configuracion de Radicacion (RQ09 RF01).</summary>
public enum RadicacionStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict
}

public sealed record RadicacionResult<T>(RadicacionStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == RadicacionStatus.Ok;

    public static RadicacionResult<T> Ok(T value) => new(RadicacionStatus.Ok, value, null);
    public static RadicacionResult<T> NotFound(string? error = null) => new(RadicacionStatus.NotFound, default, error ?? "No encontrado.");
    public static RadicacionResult<T> Invalid(string error) => new(RadicacionStatus.Invalid, default, error);
    public static RadicacionResult<T> Conflict(string error) => new(RadicacionStatus.Conflict, default, error);
}
