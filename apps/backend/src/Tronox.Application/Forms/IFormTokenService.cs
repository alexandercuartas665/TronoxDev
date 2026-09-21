namespace Tronox.Application.Forms;

/// <summary>
/// Emision y validacion de tokens opacos de publicacion de un formulario por URL (/f/{token},
/// RQ08, port ECOREX / ADR-0015). El token en claro sale UNA sola vez al emitirlo; solo se
/// persiste su hash SHA-256. Las operaciones de administracion (emitir, revocar, marcar usado,
/// listar) son tenant-scoped; la unica excepcion es <see cref="ValidateAsync"/>, que el visor
/// anonimo usa sin tenant en contexto y por eso resuelve por hash exacto ignorando el filtro
/// global, devolviendo el TenantId del token para que el visor fije el ambiente.
/// </summary>
public interface IFormTokenService
{
    /// <summary>Emite un token para una definicion Active; el token en claro viaja en el resultado
    /// una unica vez (solo se guarda su hash).</summary>
    Task<FormResult<EmitFormTokenResult>> EmitAsync(long definitionId, EmitFormTokenRequest options, CancellationToken cancellationToken = default);

    /// <summary>Valida un token presentado por el visor publico. Ante CUALQUIER fallo devuelve
    /// <see cref="FormTokenValidation.Invalid"/> sin filtrar el motivo.</summary>
    Task<FormTokenValidation> ValidateAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Revoca un token (tenant-scoped). Idempotente.</summary>
    Task<FormResult<bool>> RevokeAsync(long tokenId, CancellationToken cancellationToken = default);

    /// <summary>Marca el token como usado (UsedAt). Idempotente.</summary>
    Task MarkUsedAsync(long tokenId, CancellationToken cancellationToken = default);

    /// <summary>Lista los tokens emitidos de una definicion (tenant-scoped), mas recientes primero.</summary>
    Task<IReadOnlyList<FormTokenDto>> ListAsync(long definitionId, CancellationToken cancellationToken = default);
}
