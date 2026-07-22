using Tronox.Domain.Enums;

namespace Tronox.Application.Admin;

public interface ITenantAdminService
{
    Task<TenantDetail> CreateAsync(CreateTenantRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantListItem>> ListAsync(TenantStatus? status = null, string? search = null, CancellationToken cancellationToken = default);
    Task<TenantDetail?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<TenantDetail?> ChangeStatusAsync(long id, ChangeTenantStatusRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza el perfil de la agencia (nombre, datos fiscales, contacto, logo). Devuelve null si no existe.</summary>
    Task<TenantDetail?> UpdateProfileAsync(long id, UpdateTenantProfileRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Lista los usuarios de un tenant para la ficha de empresa del operador de plataforma
    /// (modulo 000072, solo lectura). Acceso cross-tenant acotado a este tenant (ADR-0026).</summary>
    Task<IReadOnlyList<TenantUserListItem>> ListUsersAsync(long tenantId, CancellationToken cancellationToken = default);
}
