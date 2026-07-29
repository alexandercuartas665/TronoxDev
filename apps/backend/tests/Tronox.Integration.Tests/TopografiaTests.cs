using Tronox.Application.Common;
using Tronox.Application.Topografia;
using Tronox.Domain.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Topografia fisica (RQ02 - RF06) sobre PostgreSQL real (Testcontainers). Cubre:
/// 1. niveles unicos por tenant (nombre/sigla/orden) y solo uno controla capacidad;
/// 2. config de niveles bloqueada si existen elementos;
/// 3. arbol de elementos con codigo topografico compuesto y jerarquia por orden;
/// 4. sigla unica entre hermanos; capacidad obligatoria si el nivel controla;
/// 5. aislamiento cross-tenant.
/// </summary>
public sealed class TopografiaTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private const long Actor = 1;
    private readonly PostgresTenantIsolationFixture _fixture;

    public TopografiaTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Niveles_Unicos_YSoloUnoControlaCapacidad()
    {
        var s = await SeedTenantAsync();
        await using var ctx = _fixture.CreateContext(s);
        var svc = NewService(ctx, s);

        Ok(await svc.CreateNivelAsync(new SaveNivelRequest("Bodega", "BOD", 1), Actor));
        Ok(await svc.CreateNivelAsync(new SaveNivelRequest("Caja", "CAJ", 2, ControlaCapacidad: true), Actor));

        // Nombre duplicado.
        Assert.Equal(TopografiaServiceStatus.Conflict, (await svc.CreateNivelAsync(new SaveNivelRequest("bodega", "X", 3), Actor)).Status);
        // Orden duplicado.
        Assert.Equal(TopografiaServiceStatus.Conflict, (await svc.CreateNivelAsync(new SaveNivelRequest("Otro", "OTR", 1), Actor)).Status);
        // Segundo nivel que controla capacidad: invalido.
        Assert.Equal(TopografiaServiceStatus.Invalid, (await svc.CreateNivelAsync(new SaveNivelRequest("Estante", "EST", 3, ControlaCapacidad: true), Actor)).Status);
    }

    [Fact]
    public async Task ConfigNiveles_SeBloquea_SiHayElementos()
    {
        var s = await SeedTenantAsync();
        await using var ctx = _fixture.CreateContext(s);
        var svc = NewService(ctx, s);
        var bodega = Ok(await svc.CreateNivelAsync(new SaveNivelRequest("Bodega", "BOD", 1), Actor));
        Ok(await svc.CreateElementoAsync(new SaveElementoRequest(bodega.Id, null, "Bodega Norte", "NOR"), Actor));

        // Ya hay un elemento: la config de niveles queda bloqueada.
        Assert.Equal(TopografiaServiceStatus.Invalid, (await svc.CreateNivelAsync(new SaveNivelRequest("Estante", "EST", 2), Actor)).Status);
        Assert.Equal(TopografiaServiceStatus.Invalid, (await svc.UpdateNivelAsync(bodega.Id, new SaveNivelRequest("Bodega", "BOD", 1), Actor)).Status);
        Assert.Equal(TopografiaServiceStatus.Invalid, (await svc.DeleteNivelAsync(bodega.Id, Actor)).Status);
    }

    [Fact]
    public async Task Arbol_CodigoTopografico_YJerarquia()
    {
        var s = await SeedTenantAsync();
        await using var ctx = _fixture.CreateContext(s);
        var svc = NewService(ctx, s);
        var bodega = Ok(await svc.CreateNivelAsync(new SaveNivelRequest("Bodega", "BOD", 1), Actor));
        var estante = Ok(await svc.CreateNivelAsync(new SaveNivelRequest("Estante", "EST", 2), Actor));
        var caja = Ok(await svc.CreateNivelAsync(new SaveNivelRequest("Caja", "CAJ", 3, ControlaCapacidad: true), Actor));

        var nor = Ok(await svc.CreateElementoAsync(new SaveElementoRequest(bodega.Id, null, "Bodega Norte", "NOR"), Actor));
        var est05 = Ok(await svc.CreateElementoAsync(new SaveElementoRequest(estante.Id, nor.Id, "Estante 05", "EST05"), Actor));
        var caja010 = Ok(await svc.CreateElementoAsync(new SaveElementoRequest(caja.Id, est05.Id, "Caja 010", "CAJ010", Capacidad: 50), Actor));

        Assert.Equal("NOR-EST05-CAJ010", caja010.CodigoTopografico);
        Assert.Equal(50, caja010.Capacidad);

        // Jerarquia: un Estante (orden 2) NO puede colgar de una Caja (orden 3).
        var malo = await svc.CreateElementoAsync(new SaveElementoRequest(estante.Id, caja010.Id, "Estante malo", "ESTX"), Actor);
        Assert.Equal(TopografiaServiceStatus.Invalid, malo.Status);

        // Sigla duplicada bajo el mismo padre.
        var dup = await svc.CreateElementoAsync(new SaveElementoRequest(estante.Id, nor.Id, "Otro estante", "est05"), Actor);
        Assert.Equal(TopografiaServiceStatus.Conflict, dup.Status);

        // Capacidad obligatoria en nivel que controla.
        var sinCap = await svc.CreateElementoAsync(new SaveElementoRequest(caja.Id, est05.Id, "Caja 011", "CAJ011"), Actor);
        Assert.Equal(TopografiaServiceStatus.Invalid, sinCap.Status);
    }

    [Fact]
    public async Task Elementos_NoSeVenEntreTenants()
    {
        var a = await SeedTenantAsync();
        await using (var ctxA = _fixture.CreateContext(a))
        {
            var svcA = NewService(ctxA, a);
            var niv = Ok(await svcA.CreateNivelAsync(new SaveNivelRequest("Bodega", "BOD", 1), Actor));
            Ok(await svcA.CreateElementoAsync(new SaveElementoRequest(niv.Id, null, "Bodega A", "BODA"), Actor));
        }
        var b = await SeedTenantAsync();
        await using (var ctxB = _fixture.CreateContext(b))
        {
            var svcB = NewService(ctxB, b);
            Assert.False(await svcB.HayElementosAsync());
            Assert.Empty(await svcB.GetArbolAsync());
            // El tenant B puede usar la misma sigla "BOD" sin colisionar (DAT-01).
            Assert.True((await svcB.CreateNivelAsync(new SaveNivelRequest("Bodega", "BOD", 1), Actor)).IsOk);
        }
    }

    // ================= Helpers =================

    private static TopografiaNivelDto Ok(TopografiaResult<TopografiaNivelDto> r) { Assert.True(r.IsOk, r.Error); return r.Value!; }
    private static TopografiaElementoNodeDto Ok(TopografiaResult<TopografiaElementoNodeDto> r) { Assert.True(r.IsOk, r.Error); return r.Value!; }

    private static TopografiaService NewService(IApplicationDbContext ctx, long tenantId)
        => new(ctx, new TestTenantContext(tenantId, 1), new NoOpAuditWriter());

    private async Task<long> SeedTenantAsync()
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = $"Topo {tenantId}" });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }

    private sealed class NoOpAuditWriter : IAuditWriter
    {
        public void Write(long actorUserId, string actionName, string entityName, long? entityId,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human) { }
        public void Write(long actorUserId, string actionName, string entityName, BaseEntity entity,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human) { }
    }
}
