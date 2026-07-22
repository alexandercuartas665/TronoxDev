using Tronox.Application.Common;

namespace Tronox.Api.Auth;

/// <summary>
/// Resuelve el tenant y usuario actuales desde los claims del JWT del request
/// (claims "tenant_id" y "sub"). En requests sin token quedan en null (fail-closed).
/// </summary>
public sealed class HttpContextTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public long? TenantId => ReadLongClaim("tenant_id");
    public long? UserId => ReadLongClaim("sub");

    private long? ReadLongClaim(string claimType)
    {
        var value = _accessor.HttpContext?.User.FindFirst(claimType)?.Value;
        return long.TryParse(value, out var parsed) ? parsed : null;
    }
}
