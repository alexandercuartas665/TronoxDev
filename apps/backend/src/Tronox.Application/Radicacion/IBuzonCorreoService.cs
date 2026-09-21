namespace Tronox.Application.Radicacion;

/// <summary>
/// Buzones de correo de recepcion (RQ09 RF01-4). La contrasena se cifra AES-256 (ISecretProtector) y
/// nunca sale en claro en los DTOs (solo TieneClave). El worker de captura de correos es integracion
/// posterior. Tenant-scoped.
/// </summary>
public interface IBuzonCorreoService
{
    Task<IReadOnlyList<BuzonCorreoDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<RadicacionResult<BuzonCorreoDto>> CreateAsync(SaveBuzonCorreoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza el buzon. Contrasena null = conserva la cifrada existente.</summary>
    Task<RadicacionResult<BuzonCorreoDto>> UpdateAsync(long id, SaveBuzonCorreoRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<RadicacionResult<bool>> SetActivoAsync(long id, bool activo, long actorUserId, CancellationToken cancellationToken = default);

    Task<RadicacionResult<bool>> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default);
}
