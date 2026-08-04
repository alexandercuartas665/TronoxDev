namespace Tronox.Application.Documentos;

/// <summary>Resultado tipado de los servicios de documentos (RQ04), mismo patron que el resto.</summary>
public enum DocumentoServiceStatus
{
    Ok = 0,
    NotFound,
    Invalid,
    Conflict,
    Forbidden
}

public sealed record DocumentoResult<T>(DocumentoServiceStatus Status, T? Value, string? Error)
{
    public bool IsOk => Status == DocumentoServiceStatus.Ok;

    public static DocumentoResult<T> Ok(T value) => new(DocumentoServiceStatus.Ok, value, null);
    public static DocumentoResult<T> NotFound(string? error = null) => new(DocumentoServiceStatus.NotFound, default, error ?? "No encontrado.");
    public static DocumentoResult<T> Invalid(string error) => new(DocumentoServiceStatus.Invalid, default, error);
    public static DocumentoResult<T> Conflict(string error) => new(DocumentoServiceStatus.Conflict, default, error);
    public static DocumentoResult<T> Forbidden(string error) => new(DocumentoServiceStatus.Forbidden, default, error);
}
