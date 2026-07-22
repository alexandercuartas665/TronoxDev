using Tronox.Application.Common;
using Tronox.Application.Common.Auth;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Admin;

/// <summary>
/// Alta integral de una agencia (modulo 1.1): crea el tenant, su usuario administrador
/// (Owner) y, opcionalmente, una suscripcion, en una sola operacion con auditoria.
/// </summary>
public sealed class OnboardingService : IOnboardingService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditWriter _audit;
    private readonly Tronox.Application.MenuConfig.IMenuProvisioningService _menuProvisioning;
    private readonly Tronox.Application.Archivistica.IClasificacionProvisioningService _clasificacionProvisioning;
    private readonly Tronox.Application.Roles.IRolProvisioningService _rolProvisioning;

    public OnboardingService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IAuditWriter audit,
        Tronox.Application.MenuConfig.IMenuProvisioningService menuProvisioning,
        Tronox.Application.Archivistica.IClasificacionProvisioningService clasificacionProvisioning,
        Tronox.Application.Roles.IRolProvisioningService rolProvisioning)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _menuProvisioning = menuProvisioning;
        _clasificacionProvisioning = clasificacionProvisioning;
        _rolProvisioning = rolProvisioning;
    }

    public async Task<OnboardingOutcome> OnboardAsync(OnboardTenantRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var email = request.AdminEmail.Trim().ToLowerInvariant();
        var isGoogle = !string.IsNullOrWhiteSpace(request.GoogleSubject);
        if (string.IsNullOrWhiteSpace(email))
        {
            return new OnboardingOutcome(false, null, "El correo del administrador es obligatorio.");
        }
        if (!isGoogle && string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return new OnboardingOutcome(false, null, "Correo y clave del administrador son obligatorios.");
        }

        if (await _db.PlatformUsers.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return new OnboardingOutcome(false, null, "Ya existe un usuario con ese correo.");
        }

        if (request.PlanId is long planId && !await _db.SaasPlans.AnyAsync(p => p.Id == planId, cancellationToken))
        {
            return new OnboardingOutcome(false, null, "Plan inexistente.");
        }

        var tenant = new Tenant
        {
            // Convencion del sistema: el NOMBRE de la empresa cliente va en MAYUSCULA.
            Name = TenantAdminService.NormalizeName(request.TenantName),
            Country = request.Country?.Trim(),
            Currency = request.Currency?.Trim(),
            Status = TenantStatus.Active,
            Kind = TenantKind.Standard
        };

        var admin = new PlatformUser
        {
            Email = email,
            DisplayName = request.AdminDisplayName?.Trim(),
            EmailVerified = isGoogle,
            Status = PlatformUserStatus.Active,
            AuthProvider = isGoogle ? "google" : "local",
            GoogleSubject = isGoogle ? request.GoogleSubject : null,
            PasswordHash = isGoogle ? null : _passwordHasher.Hash(request.AdminPassword)
        };

        // Los Id son BIGINT de identidad: solo existen despues del INSERT. Por eso el alta se
        // hace en dos guardados (tenant + admin, luego lo que depende de sus Id) dentro de una
        // sola transaccion, para que el conjunto siga siendo atomico como antes.
        var joinTx = _db.HasActiveTransaction;
        var tx = joinTx ? null : await _db.BeginTransactionAsync(cancellationToken);
        long? subscriptionId;
        try
        {
            _db.Tenants.Add(tenant);
            _db.PlatformUsers.Add(admin);
            await _db.SaveChangesAsync(cancellationToken);

            _db.TenantUsers.Add(new TenantUser
            {
                TenantId = tenant.Id,
                PlatformUserId = admin.Id,
                Email = email,
                TenantRole = TenantRole.Owner,
                Status = PlatformUserStatus.Active
            });

            TenantSubscription? subscription = null;
            if (request.PlanId is long plan)
            {
                var startsAt = DateTimeOffset.UtcNow;
                subscription = new TenantSubscription
                {
                    TenantId = tenant.Id,
                    PlanId = plan,
                    Status = SubscriptionStatus.Active,
                    BillingFrequency = request.BillingFrequency,
                    StartsAt = startsAt,
                    CurrentPeriodEndsAt = request.BillingFrequency == BillingFrequency.Yearly
                        ? startsAt.AddYears(1)
                        : startsAt.AddMonths(1)
                };
                _db.TenantSubscriptions.Add(subscription);
            }

            // Se audita la ENTIDAD, no su id: el id lo genera la base al insertar (aqui vale 0).
            _audit.Write(actorUserId, "tenant.onboard", nameof(Tenant), tenant,
                previousValue: null,
                newValue: new { tenant.Name, AdminEmail = email, HasSubscription = subscription is not null });

            await _db.SaveChangesAsync(cancellationToken);
            subscriptionId = subscription?.Id;

            if (tx is not null) { await tx.CommitAsync(cancellationToken); }
        }
        catch
        {
            if (tx is not null) { await tx.RollbackAsync(cancellationToken); }
            throw;
        }
        finally
        {
            if (tx is not null) { await tx.DisposeAsync(); }
        }

        // El tenant nace CON menu: vista "Completo" (por defecto) con el arbol canonico. Sin esto el
        // cliente quedaba sin ninguna vista y sus usuarios no veian nada (bug real detectado en prod).
        await _menuProvisioning.EnsureDefaultMenuAsync(tenant.Id, cancellationToken);

        // ... y CON sus 4 niveles de clasificacion documental (RF01-P.3). Idempotente.
        await _clasificacionProvisioning.EnsureNivelesClasificacionAsync(tenant.Id, cancellationToken);

        // ... y CON sus roles predeterminados (RF05). DE ULTIMO: necesita los niveles (FK
        // obligatorio) y el menu (de el sale la matriz completa del Super Administrador). Ademas
        // ancla al Owner recien creado a "Super Administrador": el sistema es FAIL-CLOSED, asi que
        // sin esa asignacion explicita el administrador del tenant nuevo no podria hacer nada.
        await _rolProvisioning.EnsureRolesPredeterminadosAsync(tenant.Id, cancellationToken);

        return new OnboardingOutcome(true,
            new OnboardingResult(tenant.Id, tenant.Name, admin.Id, admin.Email, subscriptionId),
            null);
    }
}
