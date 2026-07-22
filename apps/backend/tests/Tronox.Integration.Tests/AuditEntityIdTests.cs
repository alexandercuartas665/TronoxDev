using Tronox.Application.Admin;
using Tronox.Application.Common;
using Tronox.Application.Roles;
using Tronox.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Regresion de RNF-04 (pista de auditoria completa e inalterable): el asiento de un ALTA debe
/// identificar la fila creada.
///
/// El Id de toda entidad es BIGINT de identidad: lo genera la base al insertar y EF lo materializa
/// DURANTE SaveChanges. Los casos de uso auditan antes de guardar, asi que copiar "entidad.Id" en
/// ese momento producia un asiento con EntityId = 0 (apuntando a nada). El arreglo difiere la
/// resolucion del id (IAuditWriter.Write(..., BaseEntity, ...) +
/// IApplicationDbContext.DeferUntilIdsAssigned) hasta que el id real existe.
///
/// Estos tests usan el AuditWriter REAL (no el doble no-op del resto de la suite) y leen la fila
/// de super_admin_audit_logs de vuelta desde PostgreSQL.
/// </summary>
public sealed class AuditEntityIdTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private readonly PostgresTenantIsolationFixture _fixture;

    public AuditEntityIdTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateRol_AuditRow_PointsToTheCreatedEntity()
    {
        var tenantId = await NewTenantAsync("Auditoria Rol");
        var actorUserId = TestIds.Next();

        long rolId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var service = new RolService(ctx, new TestTenantContext(tenantId), new AuditWriter(ctx));
            var created = await service.SaveAsync(null, "Auditado", null, true, actorUserId);
            Assert.True(created.IsOk, created.Error);
            rolId = created.Value!.Id;
        }

        Assert.NotEqual(0, rolId);

        await using var read = _fixture.CreateContext(tenantId);
        var log = await read.SuperAdminAuditLogs.AsNoTracking()
            .SingleAsync(l => l.ActionName == "rol.create" && l.ActorUserId == actorUserId);

        // El defecto: EntityId quedaba en 0 (id copiado antes de que la base lo generara).
        Assert.NotNull(log.EntityId);
        Assert.NotEqual(0, log.EntityId!.Value);
        Assert.Equal(rolId, log.EntityId!.Value);
        Assert.Equal(nameof(Rol), log.EntityName);
        // El tenant del asiento tambien se resuelve de la entidad, no de un 0 pre-guardado.
        Assert.Equal(tenantId, log.TenantId);

        // Y el id apunta a una fila que existe de verdad.
        Assert.True(await read.Roles.AnyAsync(r => r.Id == log.EntityId!.Value));
    }

    [Fact]
    public async Task CreateTenant_AuditRow_PointsToTheCreatedEntity()
    {
        var actorUserId = TestIds.Next();

        long tenantId;
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            var service = new TenantAdminService(ctx, new AuditWriter(ctx), new NoOpMenuProvisioning(), new NoOpClasificacionProvisioning());
            var created = await service.CreateAsync(new CreateTenantRequest("Auditoria Tenant"), actorUserId);
            tenantId = created.Id;
        }

        Assert.NotEqual(0, tenantId);

        await using var read = _fixture.CreateContext(tenantId: null);
        var log = await read.SuperAdminAuditLogs.AsNoTracking()
            .SingleAsync(l => l.ActionName == "tenant.create" && l.ActorUserId == actorUserId);

        Assert.NotNull(log.EntityId);
        Assert.NotEqual(0, log.EntityId!.Value);
        Assert.Equal(tenantId, log.EntityId!.Value);
        Assert.Equal(tenantId, log.TenantId);
        Assert.True(await read.Tenants.AnyAsync(t => t.Id == log.EntityId!.Value));
    }

    /// <summary>
    /// El segundo guardado que resuelve el id no debe sobrevivir a un fallo: si la transaccion
    /// del caso de uso se revierte, no queda asiento huerfano ni entidad a medias.
    /// </summary>
    [Fact]
    public async Task DeferredAudit_IsRolledBackWithTheCallerTransaction()
    {
        var tenantId = await NewTenantAsync("Auditoria Rollback");
        var actorUserId = TestIds.Next();

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await using var tx = await ctx.BeginTransactionAsync();
            var service = new RolService(ctx, new TestTenantContext(tenantId), new AuditWriter(ctx));
            var created = await service.SaveAsync(null, "Revertido", null, true, actorUserId);
            Assert.True(created.IsOk, created.Error);
            await tx.RollbackAsync();
        }

        await using var read = _fixture.CreateContext(tenantId);
        Assert.False(await read.SuperAdminAuditLogs.AnyAsync(l => l.ActorUserId == actorUserId));
        Assert.False(await read.Roles.AnyAsync(r => r.Name == "Revertido"));
    }

    private async Task<long> NewTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }
}
