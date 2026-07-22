using System.Security.Claims;
using Tronox.Application.Auth;
using Tronox.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Api.Endpoints;

/// <summary>Endpoints de autenticacion, selector de tenant y un recurso tenant-scoped de ejemplo.</summary>
public static class ConnectEndpoints
{
    public static void MapConnectEndpoints(this WebApplication app)
    {
        app.MapPost("/connect/token", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.AuthenticateAsync(request, ct);
            return result is null ? Results.Unauthorized() : Results.Ok(result);
        }).AllowAnonymous();

        app.MapPost("/connect/switch-tenant", async (SwitchTenantRequest request, ClaimsPrincipal user, IAuthService auth, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await auth.SwitchTenantAsync(userId.Value, request.TenantId, ct);
            return result is null
                ? Results.BadRequest(new { error = "Sin acceso al tenant solicitado." })
                : Results.Ok(result);
        }).RequireAuthorization();

        app.MapGet("/connect/me", async (ClaimsPrincipal user, IAuthService auth, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var me = await auth.GetMeAsync(userId.Value, GetTenantId(user), ct);
            return me is null ? Results.Unauthorized() : Results.Ok(me);
        }).RequireAuthorization();

        app.MapGet("/platform/me", (ClaimsPrincipal user) =>
            Results.Ok(new
            {
                userId = GetUserId(user),
                platformRole = user.FindFirst("platform_role")?.Value
            }))
            .RequireAuthorization("SuperAdminOnly");

        // Recurso tenant-scoped de ejemplo: el filtro global del DbContext limita por tenant_id del JWT.
        app.MapGet("/tenant/configurations", async (IApplicationDbContext db, CancellationToken ct) =>
        {
            var items = await db.TenantConfigurations
                .Select(c => new { c.Id, c.TenantId, c.ConfigKey, c.ConfigValue })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization("TenantMember");
    }

    private static long? GetUserId(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : null;

    private static long? GetTenantId(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst("tenant_id")?.Value, out var id) ? id : null;
}
