namespace Tronox.Application.Auth;

public sealed record LoginRequest(string Email, string Password, long? TenantId = null);

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    long? TenantId,
    bool TenantSelectionRequired);

public sealed record SwitchTenantRequest(long TenantId);

public sealed record TenantSummary(long TenantId, string Name, string TenantRole);

public sealed record MeResponse(
    long UserId,
    string Email,
    string? DisplayName,
    string? PlatformRole,
    long? CurrentTenantId,
    IReadOnlyList<TenantSummary> Tenants);
