using Tronox.Application.Common;
using Tronox.Application.Listas;
using Tronox.Domain.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Administrador de Listas (RQ02 - RF03) sobre PostgreSQL real (Testcontainers). Cubre lo que
/// necesita base de datos:
/// 1. nombre de lista unico por tenant;
/// 2. clave de opcion unica dentro de la lista (repetible en otra lista);
/// 3. orden automatico al agregar y reordenamiento 1..N;
/// 4. usabilidad = Activa + >= 2 opciones activas;
/// 5. nunca borrado fisico: inactivar lista y opcion;
/// 6. aislamiento cross-tenant (DAT-01).
/// </summary>
public sealed class AdminListasTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private const long Actor = 1;
    private readonly PostgresTenantIsolationFixture _fixture;

    public AdminListasTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task NombreDeLista_EsUnicoPorTenant()
    {
        var seed = await SeedTenantAsync("Listas Nombres");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        Ok(await svc.CreateListaAsync(new SaveListaRequest("Tipo de Vinculacion"), Actor));

        var dup = await svc.CreateListaAsync(new SaveListaRequest("tipo de vinculacion"), Actor); // case-insensitive
        Assert.Equal(ListaServiceStatus.Conflict, dup.Status);
    }

    [Fact]
    public async Task ClaveDeOpcion_EsUnicaEnLaLista_PeroSeRepiteEnOtra()
    {
        var seed = await SeedTenantAsync("Listas Claves");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var a = Ok(await svc.CreateListaAsync(new SaveListaRequest("Lista A"), Actor));
        var b = Ok(await svc.CreateListaAsync(new SaveListaRequest("Lista B"), Actor));

        Ok(await svc.AddOpcionAsync(a.Id, new SaveOpcionRequest("CARRERA", "Carrera Administrativa"), Actor));

        // Misma clave en la MISMA lista: conflicto.
        var dup = await svc.AddOpcionAsync(a.Id, new SaveOpcionRequest("carrera", "Otra"), Actor);
        Assert.Equal(ListaServiceStatus.Conflict, dup.Status);

        // Misma clave en OTRA lista: permitido.
        Assert.True((await svc.AddOpcionAsync(b.Id, new SaveOpcionRequest("CARRERA", "Carrera"), Actor)).IsOk);
    }

    [Fact]
    public async Task Opciones_OrdenAutomatico_YReordenar()
    {
        var seed = await SeedTenantAsync("Listas Orden");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var lista = Ok(await svc.CreateListaAsync(new SaveListaRequest("Prioridad"), Actor));
        var alta = Ok(await svc.AddOpcionAsync(lista.Id, new SaveOpcionRequest("ALTA", "Alta"), Actor));
        var media = Ok(await svc.AddOpcionAsync(lista.Id, new SaveOpcionRequest("MEDIA", "Media"), Actor));
        var baja = Ok(await svc.AddOpcionAsync(lista.Id, new SaveOpcionRequest("BAJA", "Baja"), Actor));

        // Orden automatico 1,2,3 en el orden de alta.
        Assert.Equal(1, alta.Orden);
        Assert.Equal(2, media.Orden);
        Assert.Equal(3, baja.Orden);

        // Reordenar: baja, alta, media.
        Ok2(await svc.ReordenarOpcionesAsync(lista.Id, [baja.Id, alta.Id, media.Id], Actor));
        var recargada = (await svc.GetAsync(lista.Id))!;
        Assert.Equal(new[] { "BAJA", "ALTA", "MEDIA" }, recargada.Opciones.Select(o => o.Clave).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, recargada.Opciones.Select(o => o.Orden).ToArray());

        // Reordenar con un conjunto que no calza: rechazado.
        var malo = await svc.ReordenarOpcionesAsync(lista.Id, [baja.Id, alta.Id], Actor);
        Assert.Equal(ListaServiceStatus.Invalid, malo.Status);
    }

    [Fact]
    public async Task Usabilidad_ExigeDosOpcionesActivas()
    {
        var seed = await SeedTenantAsync("Listas Usable");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var lista = Ok(await svc.CreateListaAsync(new SaveListaRequest("Estado Civil"), Actor));
        Assert.False((await svc.GetAsync(lista.Id))!.EsUsable); // 0 opciones

        var o1 = Ok(await svc.AddOpcionAsync(lista.Id, new SaveOpcionRequest("SOLTERO", "Soltero"), Actor));
        Assert.False((await svc.GetAsync(lista.Id))!.EsUsable); // 1 activa

        Ok(await svc.AddOpcionAsync(lista.Id, new SaveOpcionRequest("CASADO", "Casado"), Actor));
        Assert.True((await svc.GetAsync(lista.Id))!.EsUsable); // 2 activas

        // Inactivar una: vuelve a no usable, pero la opcion sigue en base (invariante 8).
        Ok2(await svc.SetOpcionEstadoAsync(o1.Id, activar: false, Actor));
        var tras = (await svc.GetAsync(lista.Id))!;
        Assert.False(tras.EsUsable);
        Assert.Equal(2, await ctx.ListaOpciones.CountAsync());

        // Inactivar la lista entera: KPI de usables baja, nada se borra.
        Ok2(await svc.SetListaEstadoAsync(lista.Id, activar: false, Actor));
        Assert.False((await svc.GetAsync(lista.Id))!.EsUsable);
        Assert.Equal(1, await ctx.ListasMaestras.CountAsync());
    }

    [Fact]
    public async Task Listas_NoChocanEntreTenantsDistintos()
    {
        var a = await SeedTenantAsync("Listas Tenant A");
        var b = await SeedTenantAsync("Listas Tenant B");

        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            Assert.True((await NewService(ctxA, a).CreateListaAsync(new SaveListaRequest("Comunes"), Actor)).IsOk);
        }
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            var enB = await NewService(ctxB, b).CreateListaAsync(new SaveListaRequest("Comunes"), Actor);
            Assert.True(enB.IsOk, enB.Error);
        }
    }

    // ================= Helpers =================

    private static ListaMaestraDto Ok(ListaResult<ListaMaestraDto> result)
    {
        Assert.True(result.IsOk, result.Error);
        return result.Value!;
    }

    private static ListaOpcionDto Ok(ListaResult<ListaOpcionDto> result)
    {
        Assert.True(result.IsOk, result.Error);
        return result.Value!;
    }

    private static void Ok2(ListaResult<bool> result) => Assert.True(result.IsOk, result.Error);

    private static ListaMaestraService NewService(IApplicationDbContext ctx, SeedData seed)
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
                Email = $"user-{tenantId}@listas.test",
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
