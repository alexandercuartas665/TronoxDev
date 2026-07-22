using System.Security.Claims;
using Tronox.Application.Admin;
using Tronox.Domain.Enums;

namespace Tronox.Api.Endpoints;

/// <summary>Endpoints de la consola Super Admin. Todos exigen la politica SuperAdminOnly.</summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/admin").RequireAuthorization("SuperAdminOnly");

        // --- Tenants ---
        admin.MapGet("/tenants", async (ITenantAdminService svc, TenantStatus? status, string? search, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(status, search, ct)));

        admin.MapGet("/tenants/{id:long}", async (long id, ITenantAdminService svc, CancellationToken ct) =>
        {
            var tenant = await svc.GetAsync(id, ct);
            return tenant is null ? Results.NotFound() : Results.Ok(tenant);
        });

        admin.MapPost("/tenants", async (CreateTenantRequest request, ClaimsPrincipal user, ITenantAdminService svc, CancellationToken ct) =>
        {
            var tenant = await svc.CreateAsync(request, ActorId(user), ct);
            return Results.Created($"/admin/tenants/{tenant.Id}", tenant);
        });

        admin.MapPost("/tenants/{id:long}/status", async (long id, ChangeTenantStatusRequest request, ClaimsPrincipal user, ITenantAdminService svc, CancellationToken ct) =>
        {
            var tenant = await svc.ChangeStatusAsync(id, request, ActorId(user), ct);
            return tenant is null ? Results.NotFound() : Results.Ok(tenant);
        });

        // --- Onboarding (alta integral de agencia) ---
        admin.MapPost("/onboarding", async (OnboardTenantRequest request, ClaimsPrincipal user, IOnboardingService svc, CancellationToken ct) =>
        {
            var outcome = await svc.OnboardAsync(request, ActorId(user), ct);
            return outcome.Success
                ? Results.Created($"/admin/tenants/{outcome.Result!.TenantId}", outcome.Result)
                : Results.BadRequest(new { error = outcome.Error });
        });

        // --- Planes ---
        admin.MapGet("/plans", async (IPlanAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)));

        admin.MapGet("/plans/{id:long}", async (long id, IPlanAdminService svc, CancellationToken ct) =>
        {
            var plan = await svc.GetAsync(id, ct);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        });

        admin.MapPost("/plans", async (CreatePlanRequest request, ClaimsPrincipal user, IPlanAdminService svc, CancellationToken ct) =>
        {
            var plan = await svc.CreateAsync(request, ActorId(user), ct);
            return Results.Created($"/admin/plans/{plan.Id}", plan);
        });

        admin.MapPut("/plans/{id:long}", async (long id, CreatePlanRequest request, ClaimsPrincipal user, IPlanAdminService svc, CancellationToken ct) =>
        {
            var plan = await svc.UpdateAsync(id, request, ActorId(user), ct);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        });

        admin.MapPost("/plans/{id:long}/active", async (long id, SetPlanActiveRequest request, ClaimsPrincipal user, IPlanAdminService svc, CancellationToken ct) =>
        {
            var plan = await svc.SetActiveAsync(id, request.IsActive, ActorId(user), ct);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        });

        // --- Suscripciones ---
        admin.MapPost("/subscriptions", async (AssignSubscriptionRequest request, ClaimsPrincipal user, ISubscriptionAdminService svc, CancellationToken ct) =>
        {
            var subscription = await svc.AssignAsync(request, ActorId(user), ct);
            return subscription is null
                ? Results.BadRequest(new { error = "Tenant o plan inexistente." })
                : Results.Created($"/admin/subscriptions/{subscription.Id}", subscription);
        });

        admin.MapGet("/tenants/{tenantId:long}/subscriptions", async (long tenantId, ISubscriptionAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListByTenantAsync(tenantId, ct)));

        // --- Pagos ---
        admin.MapPost("/payments", async (RegisterPaymentRequest request, ClaimsPrincipal user, IPaymentAdminService svc, CancellationToken ct) =>
        {
            var payment = await svc.RegisterAsync(request, ActorId(user), ct);
            return payment is null
                ? Results.BadRequest(new { error = "Suscripcion inexistente para el tenant." })
                : Results.Created($"/admin/payments/{payment.Id}", payment);
        });

        admin.MapGet("/payments", async (IPaymentAdminService svc, long? tenantId, PaymentStatus? status, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(tenantId, status, ct)));
    }

    private static long ActorId(ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : 0;

    private sealed record SetPlanActiveRequest(bool IsActive);
}
