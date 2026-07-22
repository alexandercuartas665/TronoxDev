using Tronox.Application.Common.Auth;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Tronox.Integration.Tests.Auth;

/// <summary>
/// Levanta la Api real contra un PostgreSQL efimero (Testcontainers), aplica migraciones
/// y siembra datos para probar login, selector de tenant, politicas y aislamiento por JWT.
/// </summary>
public sealed class TronoxApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public const string SigningKey = "tronox-test-signing-key-must-be-long-enough-256bit-aaaa";
    public const string Password = "Secret123!";
    public const string SingleEmail = "single@tronox.tareas";
    public const string MultiEmail = "multi@tronox.tareas";
    public const string SuperEmail = "super@tronox.tareas";

    public long TenantAId { get; } = TestIds.Next();
    public long TenantBId { get; } = TestIds.Next();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Database:AutoMigrate", "false");
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TronoxDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await ctx.Database.MigrateAsync();

        ctx.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Agencia A", Status = TenantStatus.Active },
            new Tenant { Id = TenantBId, Name = "Agencia B", Status = TenantStatus.Active });

        var single = new PlatformUser
        {
            Email = SingleEmail,
            EmailVerified = true,
            Status = PlatformUserStatus.Active,
            PasswordHash = hasher.Hash(Password)
        };
        var multi = new PlatformUser
        {
            Email = MultiEmail,
            EmailVerified = true,
            Status = PlatformUserStatus.Active,
            PasswordHash = hasher.Hash(Password)
        };
        var super = new PlatformUser
        {
            Email = SuperEmail,
            EmailVerified = true,
            Status = PlatformUserStatus.Active,
            PasswordHash = hasher.Hash(Password),
            PlatformRole = PlatformRole.SuperAdmin
        };
        ctx.PlatformUsers.AddRange(single, multi, super);

        // El Id de PlatformUser lo asigna la base al insertar (BIGINT identidad): antes de
        // SaveChanges vale 0. Se enlaza por la propiedad de navegacion para que EF resuelva
        // la FK y el orden de insercion.
        ctx.TenantUsers.AddRange(
            new TenantUser { TenantId = TenantAId, PlatformUser = single, Email = SingleEmail, TenantRole = TenantRole.Advisor, Status = PlatformUserStatus.Active },
            new TenantUser { TenantId = TenantAId, PlatformUser = multi, Email = MultiEmail, TenantRole = TenantRole.Advisor, Status = PlatformUserStatus.Active },
            new TenantUser { TenantId = TenantBId, PlatformUser = multi, Email = MultiEmail, TenantRole = TenantRole.Admin, Status = PlatformUserStatus.Active });

        ctx.TenantConfigurations.AddRange(
            new TenantConfiguration { TenantId = TenantAId, ConfigKey = "tono", ConfigValue = "formal" },
            new TenantConfiguration { TenantId = TenantBId, ConfigKey = "tono", ConfigValue = "informal" },
            new TenantConfiguration { TenantId = TenantBId, ConfigKey = "horario", ConfigValue = "8-18" });

        await ctx.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}
