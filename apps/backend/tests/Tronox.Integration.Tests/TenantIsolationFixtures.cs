using Tronox.Application.Common;
using Tronox.Infrastructure.Persistence;
using Tronox.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Tronox.Integration.Tests;

/// <summary>
/// Fixture base para los tests de aislamiento multi-tenant en matriz dual (ADR-001).
/// Cada fixture concreto levanta un contenedor efimero de su motor, aplica las migraciones
/// del proveedor y fabrica contextos con el mismo interceptor de auditoria/tenant que usa
/// la aplicacion. El contenedor se comparte entre los tests de la clase (IClassFixture).
/// </summary>
public abstract class TenantIsolationDbFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await StartContainerAsync();
        await using var ctx = CreateContext(tenantId: null);
        await ctx.Database.MigrateAsync();
    }

    public abstract Task DisposeAsync();

    protected abstract Task StartContainerAsync();

    /// <summary>
    /// Crea un contexto EF con el tenant activo indicado (null = sin tenant, fail-closed).
    /// Espeja la configuracion de DependencyInjection: proveedor + snake_case + interceptor.
    /// </summary>
    public abstract TronoxDbContext CreateContext(Guid? tenantId);

    protected sealed class FixedTenantContext(Guid? tenantId, Guid? userId = null) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
        public Guid? UserId { get; } = userId;
    }
}

/// <summary>Fixture PostgreSQL: contenedor efimero postgres:16-alpine via Testcontainers.</summary>
public sealed class PostgresTenantIsolationFixture : TenantIsolationDbFixture
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    protected override Task StartContainerAsync() => _db.StartAsync();

    public override async Task DisposeAsync() => await _db.DisposeAsync();

    public override TronoxDbContext CreateContext(Guid? tenantId)
    {
        var tenantContext = new FixedTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<TronoxDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableTenantInterceptor(tenantContext, TimeProvider.System))
            .Options;

        return new TronoxDbContext(options, tenantContext);
    }
}

// La fixture de SQL Server del backbone se elimina: TRONOX usa PostgreSQL como motor unico,
// asi que el test de aislamiento cross-tenant corre contra un solo proveedor real (DAT-01).
