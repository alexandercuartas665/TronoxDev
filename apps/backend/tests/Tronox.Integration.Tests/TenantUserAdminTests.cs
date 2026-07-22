using Tronox.Application.Common;
using Tronox.Application.MenuConfig;
using Tronox.Application.Tenancy;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Tests de integracion del modulo Administracion de usuarios del tenant (000073, ADR-0031) en
/// matriz dual PostgreSQL / SQL Server (Testcontainers). Cubre invitar (con y sin clave), cambiar
/// rol, cambiar estado, resetear clave (Invited -> Active y hash verificable), asignar vista de
/// menu (via IMenuConfigService) con relectura persistida, y AISLAMIENTO cross-tenant (el tenant B
/// no ve ni afecta a los usuarios del tenant A). Reusa las fixtures de aislamiento dual.
/// </summary>
public abstract class TenantUserAdminTestsBase
{
    private readonly TenantIsolationDbFixture _fixture;
    private static readonly Pbkdf2PasswordHasher Hasher = new();

    protected TenantUserAdminTestsBase(TenantIsolationDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Invite_WithPassword_CreatesActiveUser_AndRelists()
    {
        var tenantId = await NewTenantAsync("Invite Con Clave");
        var email = $"asesor-{Guid.NewGuid():N}@empresa.local";

        var created = await RunAsync(tenantId, s =>
            s.InviteAsync(new InviteTenantUserRequest(email, TenantRole.Advisor, "Clave123", "Ana Perez"), TestIds.Next()));

        Assert.NotNull(created);
        Assert.Equal(email, created!.Email);
        Assert.Equal(TenantRole.Advisor, created.TenantRole);

        var list = await RunAsync(tenantId, s => s.ListAsync());
        var row = Assert.Single(list, u => u.Email == email);
        Assert.Equal("Ana Perez", row.DisplayName);

        // La cuenta de plataforma quedo activa (tiene clave) y el hash verifica.
        await using var ctx = _fixture.CreateContext(tenantId);
        var pu = await ctx.PlatformUsers.IgnoreQueryFilters().FirstAsync(p => p.Email == email);
        Assert.Equal(PlatformUserStatus.Active, pu.Status);
        Assert.NotNull(pu.PasswordHash);
        Assert.True(Hasher.Verify(pu.PasswordHash!, "Clave123"));
    }

    [Fact]
    public async Task Invite_WithoutPassword_LeavesPlatformUserInvited()
    {
        var tenantId = await NewTenantAsync("Invite Sin Clave");
        var email = $"invitado-{Guid.NewGuid():N}@empresa.local";

        var created = await RunAsync(tenantId, s =>
            s.InviteAsync(new InviteTenantUserRequest(email, TenantRole.Advisor), TestIds.Next()));
        Assert.NotNull(created);

        await using var ctx = _fixture.CreateContext(tenantId);
        var pu = await ctx.PlatformUsers.IgnoreQueryFilters().FirstAsync(p => p.Email == email);
        Assert.Equal(PlatformUserStatus.Invited, pu.Status);
        Assert.Null(pu.PasswordHash);
    }

    [Fact]
    public async Task ChangeRole_And_SetStatus_Persist()
    {
        var tenantId = await NewTenantAsync("Rol Y Estado");
        var email = $"usr-{Guid.NewGuid():N}@empresa.local";
        var created = await RunAsync(tenantId, s =>
            s.InviteAsync(new InviteTenantUserRequest(email, TenantRole.Advisor, "Clave123"), TestIds.Next()));

        await RunAsync(tenantId, s => s.ChangeRoleAsync(created!.Id, TenantRole.Supervisor, TestIds.Next()));
        await RunAsync(tenantId, s => s.SetStatusAsync(created!.Id, PlatformUserStatus.Suspended, TestIds.Next()));

        var row = Assert.Single(await RunAsync(tenantId, s => s.ListAsync()), u => u.Id == created!.Id);
        Assert.Equal(TenantRole.Supervisor, row.TenantRole);
        Assert.Equal(PlatformUserStatus.Suspended, row.Status);
    }

    [Fact]
    public async Task ResetPassword_HashesAndActivatesInvited()
    {
        var tenantId = await NewTenantAsync("Reset Clave");
        var email = $"inv-{Guid.NewGuid():N}@empresa.local";
        // Sin clave -> Invited.
        var created = await RunAsync(tenantId, s =>
            s.InviteAsync(new InviteTenantUserRequest(email, TenantRole.Advisor), TestIds.Next()));

        var updated = await RunAsync(tenantId, s => s.ResetPasswordAsync(created!.Id, "NuevaClave1", TestIds.Next()));
        Assert.NotNull(updated);
        Assert.Equal(PlatformUserStatus.Active, updated!.Status);

        await using var ctx = _fixture.CreateContext(tenantId);
        var pu = await ctx.PlatformUsers.IgnoreQueryFilters().FirstAsync(p => p.Email == email);
        Assert.Equal(PlatformUserStatus.Active, pu.Status);
        Assert.True(Hasher.Verify(pu.PasswordHash!, "NuevaClave1"));
    }

    [Fact]
    public async Task AssignMenuView_PersistsOnTenantUser()
    {
        var tenantId = await NewTenantAsync("Asignar Vista");
        var email = $"vis-{Guid.NewGuid():N}@empresa.local";
        var created = await RunAsync(tenantId, s =>
            s.InviteAsync(new InviteTenantUserRequest(email, TenantRole.Advisor, "Clave123"), TestIds.Next()));

        // Vista de menu del tenant.
        long viewId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var view = new MenuView { TenantId = tenantId, Name = "Completo", IsDefault = true, SortOrder = 0 };
            ctx.MenuViews.Add(view);
            ctx.MenuNodes.Add(new MenuNode
            {
                TenantId = tenantId,
                MenuViewId = view.Id,
                Kind = MenuNodeKind.QuickLink,
                Name = "Inicio",
                Route = "inicio",
                SortOrder = 0
            });
            await ctx.SaveChangesAsync();
            viewId = view.Id;
        }

        var res = await RunMenuAsync(tenantId, s => s.AssignUserToViewAsync(created!.Id, viewId));
        Assert.True(res.IsOk, res.Error);

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var tu = await ctx.TenantUsers.FirstAsync(u => u.Id == created!.Id);
            Assert.Equal(viewId, tu.MenuViewId);
        }
    }

    [Fact]
    public async Task CrossTenant_Isolation_BCannotSeeOrAffectAUsers()
    {
        var a = await NewTenantAsync("Tenant A");
        var b = await NewTenantAsync("Tenant B");

        var emailA = $"a-{Guid.NewGuid():N}@empresa.local";
        var userA = await RunAsync(a, s =>
            s.InviteAsync(new InviteTenantUserRequest(emailA, TenantRole.Admin, "Clave123"), TestIds.Next()));

        // B no ve al usuario de A.
        var bList = await RunAsync(b, s => s.ListAsync());
        Assert.DoesNotContain(bList, u => u.Id == userA!.Id);

        // B no puede cambiar rol/estado/clave de un usuario de A (filtro global -> null).
        Assert.Null(await RunAsync(b, s => s.ChangeRoleAsync(userA!.Id, TenantRole.Owner, TestIds.Next())));
        Assert.Null(await RunAsync(b, s => s.SetStatusAsync(userA!.Id, PlatformUserStatus.Blocked, TestIds.Next())));
        Assert.Null(await RunAsync(b, s => s.ResetPasswordAsync(userA!.Id, "Hackeada1", TestIds.Next())));

        // El usuario de A quedo intacto.
        var row = Assert.Single(await RunAsync(a, s => s.ListAsync()), u => u.Id == userA!.Id);
        Assert.Equal(TenantRole.Admin, row.TenantRole);
        Assert.Equal(PlatformUserStatus.Active, row.Status);
    }

    // ---- Helpers ----

    private async Task<long> NewTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    private async Task<T> RunAsync<T>(long tenantId, Func<ITenantUserService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var tenantCtx = new TestTenantContext(tenantId);
        var service = new TenantUserService(ctx, tenantCtx, Hasher, new AuditWriter(ctx));
        return await action(service);
    }

    private async Task<T> RunMenuAsync<T>(long tenantId, Func<IMenuConfigService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var service = new MenuConfigService(ctx, new TestTenantContext(tenantId));
        return await action(service);
    }

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }
}

/// <summary>Matriz dual, motor PostgreSQL (contenedor efimero postgres:16-alpine).</summary>
public sealed class TenantUserAdminTests_Postgres
    : TenantUserAdminTestsBase, IClassFixture<PostgresTenantIsolationFixture>
{
    public TenantUserAdminTests_Postgres(PostgresTenantIsolationFixture fixture) : base(fixture) { }
}

// La variante SQL Server de la matriz dual se elimina: TRONOX usa PostgreSQL como motor unico.
