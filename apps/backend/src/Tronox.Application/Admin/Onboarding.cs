using Tronox.Domain.Enums;

namespace Tronox.Application.Admin;

public sealed record OnboardTenantRequest(
    string TenantName,
    string AdminEmail,
    string AdminPassword,
    string? AdminDisplayName = null,
    string? Country = null,
    string? Currency = null,
    long? PlanId = null,
    BillingFrequency BillingFrequency = BillingFrequency.Monthly,
    // Cuando viene un subject de Google, el admin se crea sin clave (login via Google).
    string? GoogleSubject = null);

public sealed record OnboardingResult(
    long TenantId,
    string TenantName,
    long AdminUserId,
    string AdminEmail,
    long? SubscriptionId);

public sealed record OnboardingOutcome(bool Success, OnboardingResult? Result, string? Error);

public interface IOnboardingService
{
    Task<OnboardingOutcome> OnboardAsync(OnboardTenantRequest request, long actorUserId, CancellationToken cancellationToken = default);
}
