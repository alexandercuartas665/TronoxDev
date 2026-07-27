using Tronox.Application.Common;
using Tronox.Application.TrdVersiones;
using Tronox.Domain.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Versiones de la TRD (RQ02 - RF01) sobre PostgreSQL real (Testcontainers). Cubre lo que necesita
/// base de datos:
/// 1. codigo_version unico por tenant;
/// 2. UNA sola version Vigente por tenant: activar una voltea la anterior a Historico;
/// 3. maquina de estados: activar/editar/descartar rechazados fuera de EnConstruccion;
/// 4. nunca borrado fisico: descartar pasa a Inactivo;
/// 5. aislamiento cross-tenant (DAT-01).
/// </summary>
public sealed class VersionesTrdTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private const long Actor = 1;
    private static readonly DateOnly Vig = new(2026, 1, 1);
    private readonly PostgresTenantIsolationFixture _fixture;

    public VersionesTrdTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Codigo_EsUnicoPorTenant()
    {
        var seed = await SeedTenantAsync("TRD Codigos");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        Assert.True((await svc.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v1", Vig), Actor)).IsOk);

        var dup = await svc.CreateAsync(new SaveTrdVersionRequest("trd-2026-v1", Vig), Actor); // case-insensitive
        Assert.Equal(TrdVersionServiceStatus.Conflict, dup.Status);
    }

    [Fact]
    public async Task Activar_HaceVigente_YVuelveLaAnteriorHistorica()
    {
        var seed = await SeedTenantAsync("TRD Vigencia");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var v1 = Ok(await svc.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v1", Vig), Actor));
        var v2 = Ok(await svc.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v2", Vig), Actor));

        // Ambas nacen En Construccion; no hay vigente todavia.
        Assert.False((await svc.GetKpisAsync()).HayVigente);

        // Activar v1: queda Vigente.
        var act1 = Ok(await svc.ActivarAsync(v1.Id, Actor));
        Assert.Equal(TrdVersionEstado.Vigente, act1.Estado);

        // Activar v2: v2 Vigente y v1 pasa AUTOMATICAMENTE a Historico (RF01 3.1.4-3).
        var act2 = Ok(await svc.ActivarAsync(v2.Id, Actor));
        Assert.Equal(TrdVersionEstado.Vigente, act2.Estado);
        Assert.Equal(TrdVersionEstado.Historico, (await svc.GetAsync(v1.Id))!.Estado);

        // Solo hay UNA vigente, y es v2.
        var vigentes = (await svc.ListAsync()).Where(v => v.EsVigente).ToList();
        Assert.Single(vigentes);
        Assert.Equal(v2.Id, vigentes[0].Id);

        // El indice unico parcial lo respalda en la base: exactamente una fila Vigente.
        Assert.Equal(1, await ctx.TrdVersiones.CountAsync(v => v.Estado == TrdVersionEstado.Vigente));
    }

    [Fact]
    public async Task NoSeActivaNiEdita_UnaVersionQueNoEstaEnConstruccion()
    {
        var seed = await SeedTenantAsync("TRD Estados");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var v = Ok(await svc.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v1", Vig), Actor));
        Ok(await svc.ActivarAsync(v.Id, Actor)); // ahora Vigente

        // Reactivar una Vigente: rechazado.
        Assert.Equal(TrdVersionServiceStatus.Invalid, (await svc.ActivarAsync(v.Id, Actor)).Status);
        // Editar una Vigente: rechazado (los datos de la version se congelan).
        var edit = await svc.UpdateAsync(v.Id, new SaveTrdVersionRequest("TRD-2026-v1-bis", Vig), Actor);
        Assert.Equal(TrdVersionServiceStatus.Invalid, edit.Status);
        // Descartar una Vigente: rechazado (se reemplaza activando otra, no descartando).
        Assert.Equal(TrdVersionServiceStatus.Invalid, (await svc.DescartarAsync(v.Id, Actor)).Status);
    }

    [Fact]
    public async Task Descartar_PasaAInactivo_SinBorradoFisico()
    {
        var seed = await SeedTenantAsync("TRD Descartar");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var v = Ok(await svc.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v1", Vig), Actor));
        var desc = Ok(await svc.DescartarAsync(v.Id, Actor));
        Assert.Equal(TrdVersionEstado.Inactivo, desc.Estado);

        // Invariante 8: sigue en base, inactiva. Nunca DELETE.
        Assert.Equal(1, await ctx.TrdVersiones.CountAsync());
        // Una inactiva no se puede activar.
        Assert.Equal(TrdVersionServiceStatus.Invalid, (await svc.ActivarAsync(v.Id, Actor)).Status);
    }

    [Fact]
    public async Task Vigente_NoChocaEntreTenantsDistintos()
    {
        var a = await SeedTenantAsync("TRD Tenant A");
        var b = await SeedTenantAsync("TRD Tenant B");

        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            var svcA = NewService(ctxA, a);
            var v = Ok(await svcA.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v1", Vig), Actor));
            Ok(await svcA.ActivarAsync(v.Id, Actor));
        }
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            var svcB = NewService(ctxB, b);
            // Mismo codigo Y una vigente propia: ninguna colisiona con las del tenant A (DAT-01).
            var v = Ok(await svcB.CreateAsync(new SaveTrdVersionRequest("TRD-2026-v1", Vig), Actor));
            var act = await svcB.ActivarAsync(v.Id, Actor);
            Assert.True(act.IsOk, act.Error);
        }
    }

    // ================= Helpers =================

    private static TrdVersionDto Ok(TrdVersionResult<TrdVersionDto> result)
    {
        Assert.True(result.IsOk, result.Error);
        return result.Value!;
    }

    private static TrdVersionService NewService(IApplicationDbContext ctx, SeedData seed)
        => new(ctx, new TestTenantContext(seed.TenantId, seed.TenantUserId), new NoOpAuditWriter());

    private async Task<SeedData> SeedTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var platformUser = new PlatformUser
            {
                Email = $"user-{tenantId}@trd.test",
                EmailVerified = true,
                Status = PlatformUserStatus.Active
            };
            ctx.PlatformUsers.Add(platformUser);
            var tenantUser = new TenantUser
            {
                TenantId = tenantId,
                PlatformUser = platformUser,
                Email = platformUser.Email
            };
            ctx.TenantUsers.Add(tenantUser);
            await ctx.SaveChangesAsync();
            return new SeedData(tenantId, tenantUser.Id);
        }
    }

    private sealed record SeedData(long TenantId, long TenantUserId);

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }

    private sealed class NoOpAuditWriter : IAuditWriter
    {
        public void Write(long actorUserId, string actionName, string entityName, long? entityId,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human)
        {
        }

        public void Write(long actorUserId, string actionName, string entityName, BaseEntity entity,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human)
        {
        }
    }
}
