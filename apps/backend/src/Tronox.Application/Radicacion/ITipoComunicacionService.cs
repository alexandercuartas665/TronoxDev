namespace Tronox.Application.Radicacion;

/// <summary>
/// Catalogo de tipos de comunicacion radicables (RQ09 RF01-2). Los 13 tipos base normativos se
/// siembran por tenant (EsBase): no se eliminan, solo se editan o inactivan. Tenant-scoped.
/// </summary>
public interface ITipoComunicacionService
{
    Task<IReadOnlyList<TipoComunicacionDto>> ListAsync(bool includeInactive = true, CancellationToken cancellationToken = default);

    Task<RadicacionResult<TipoComunicacionDto>> CreateAsync(SaveTipoComunicacionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<RadicacionResult<TipoComunicacionDto>> UpdateAsync(long id, SaveTipoComunicacionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Activa/inactiva un tipo. Los base solo se pueden inactivar (no eliminar).</summary>
    Task<RadicacionResult<bool>> SetActivoAsync(long id, bool activo, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Elimina un tipo NO base sin uso. Los base nunca se eliminan.</summary>
    Task<RadicacionResult<bool>> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default);
}
