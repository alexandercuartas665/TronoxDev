using Tronox.Application.Common;
using Tronox.Application.SeriesDocumentales;
using Tronox.Domain.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Catalogo de Series y Subseries (RQ02 - RF02) sobre PostgreSQL real (Testcontainers). Cubre lo
/// que necesita base de datos:
/// 1. codigo unico POR NIVEL (mismo padre), repetible bajo padres distintos;
/// 2. nombre unico entre hermanos (paridad con el legacy doc_catalogoTRD);
/// 3. jerarquia ilimitada (serie -> subserie -> sub-subserie);
/// 4. ciclos rechazados al reubicar;
/// 5. nunca borrado fisico: se inactiva, y no se inactiva con subseries activas;
/// 6. aislamiento cross-tenant (DAT-01).
/// </summary>
public sealed class CatalogoSeriesTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private const long Actor = 1;
    private readonly PostgresTenantIsolationFixture _fixture;

    public CatalogoSeriesTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Codigo_EsUnicoPorNivel_PeroSeRepiteBajoPadresDistintos()
    {
        var seed = await SeedTenantAsync("Series Codigos");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var actas = Ok(await svc.CreateAsync(new SaveSerieRequest("01", "Actas"), Actor));
        var contratos = Ok(await svc.CreateAsync(new SaveSerieRequest("02", "Contratos"), Actor));

        // Subserie bajo Actas.
        Assert.True((await svc.CreateAsync(new SaveSerieRequest("01.1", "Actas de Comite", ParentId: actas.Id), Actor)).IsOk);

        // MISMO codigo en la RAIZ: conflicto (dos series raiz no comparten codigo).
        var dupRaiz = await svc.CreateAsync(new SaveSerieRequest("01", "Otra"), Actor);
        Assert.Equal(SerieServiceStatus.Conflict, dupRaiz.Status);
        Assert.Contains("01", dupRaiz.Error!);

        // MISMO codigo "01.1" pero bajo OTRO padre (Contratos): permitido, la unicidad es por nivel.
        var enOtroPadre = await svc.CreateAsync(new SaveSerieRequest("01.1", "Contratos menores", ParentId: contratos.Id), Actor);
        Assert.True(enOtroPadre.IsOk, enOtroPadre.Error);
    }

    [Fact]
    public async Task Nombre_EsUnicoEntreHermanos()
    {
        // Paridad con el legacy CatalogoSeriesRepository.ExisteNombreDuplicadoEnMismoPadre.
        var seed = await SeedTenantAsync("Series Nombres");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        Ok(await svc.CreateAsync(new SaveSerieRequest("01", "Actas"), Actor));

        // Mismo nombre en el mismo nivel (aunque cambie el codigo): conflicto.
        var dup = await svc.CreateAsync(new SaveSerieRequest("02", "ACTAS"), Actor);
        Assert.Equal(SerieServiceStatus.Conflict, dup.Status);

        // El mismo nombre bajo un padre distinto SI se permite.
        var padre = Ok(await svc.CreateAsync(new SaveSerieRequest("03", "Gestion"), Actor));
        Assert.True((await svc.CreateAsync(new SaveSerieRequest("03.1", "Actas", ParentId: padre.Id), Actor)).IsOk);
    }

    [Fact]
    public async Task Jerarquia_Ilimitada_SeDevuelveComoArbol()
    {
        var seed = await SeedTenantAsync("Series Arbol");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var serie = Ok(await svc.CreateAsync(new SaveSerieRequest("01", "Actas"), Actor));
        var sub = Ok(await svc.CreateAsync(new SaveSerieRequest("01.1", "Actas de Comite", ParentId: serie.Id), Actor));
        Ok(await svc.CreateAsync(new SaveSerieRequest("01.1.1", "Comite Directivo", ParentId: sub.Id), Actor));

        var tree = await svc.GetTreeAsync();
        var raiz = Assert.Single(tree);
        Assert.Equal("01", raiz.Codigo);
        Assert.True(raiz.EsSerie);
        var nivel2 = Assert.Single(raiz.Children);
        Assert.False(nivel2.EsSerie);
        var nivel3 = Assert.Single(nivel2.Children);
        Assert.Equal("01.1.1", nivel3.Codigo);
    }

    [Fact]
    public async Task Reubicar_EnUnCiclo_EsRechazado()
    {
        var seed = await SeedTenantAsync("Series Ciclo");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var padre = Ok(await svc.CreateAsync(new SaveSerieRequest("01", "Padre"), Actor));
        var hija = Ok(await svc.CreateAsync(new SaveSerieRequest("01.1", "Hija", ParentId: padre.Id), Actor));

        // Colgar el padre de su propia hija = ciclo.
        var ciclo = await svc.UpdateAsync(padre.Id, new SaveSerieRequest("01", "Padre", ParentId: hija.Id), Actor);
        Assert.Equal(SerieServiceStatus.Invalid, ciclo.Status);
        Assert.Contains("ciclo", ciclo.Error!, StringComparison.OrdinalIgnoreCase);

        // Un nodo tampoco puede ser su propio padre.
        var autoPadre = await svc.UpdateAsync(hija.Id, new SaveSerieRequest("01.1", "Hija", ParentId: hija.Id), Actor);
        Assert.Equal(SerieServiceStatus.Invalid, autoPadre.Status);
    }

    [Fact]
    public async Task NoSeInactivaConSubseriesActivas_YNuncaHayBorradoFisico()
    {
        var seed = await SeedTenantAsync("Series Inactivar");
        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var padre = Ok(await svc.CreateAsync(new SaveSerieRequest("01", "Actas"), Actor));
        var hija = Ok(await svc.CreateAsync(new SaveSerieRequest("01.1", "Actas de Comite", ParentId: padre.Id), Actor));

        // Inactivar el padre con una subserie activa: bloqueado.
        var bloqueado = await svc.SetEstadoAsync(padre.Id, activar: false, Actor);
        Assert.Equal(SerieServiceStatus.Invalid, bloqueado.Status);

        // Inactivando primero la hija, el padre ya se puede inactivar.
        Assert.True((await svc.SetEstadoAsync(hija.Id, activar: false, Actor)).IsOk);
        var ahoraSi = await svc.SetEstadoAsync(padre.Id, activar: false, Actor);
        Assert.True(ahoraSi.IsOk, ahoraSi.Error);

        // Invariante 8: siguen ambas en base, inactivas. Nunca DELETE.
        Assert.Equal(2, await ctx.SeriesDocumentales.CountAsync());
        Assert.Equal(2, await ctx.SeriesDocumentales.CountAsync(s => s.Estado == SerieEstado.Inactivo));

        // Por defecto el arbol solo trae activas: vacio. Con includeInactivas vuelven a verse.
        Assert.Empty(await svc.GetTreeAsync());
        Assert.Single(await svc.GetTreeAsync(includeInactivas: true));
    }

    [Fact]
    public async Task Codigo_NoChocaEntreTenantsDistintos()
    {
        var a = await SeedTenantAsync("Series Tenant A");
        var b = await SeedTenantAsync("Series Tenant B");

        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            Assert.True((await NewService(ctxA, a).CreateAsync(new SaveSerieRequest("01", "Actas"), Actor)).IsOk);
        }
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            // Unicidad DENTRO del tenant (DAT-01): el mismo codigo en otro tenant no colisiona.
            var enB = await NewService(ctxB, b).CreateAsync(new SaveSerieRequest("01", "Actas"), Actor);
            Assert.True(enB.IsOk, enB.Error);
        }
    }

    // ================= Helpers =================

    private static SerieDto Ok(SerieResult<SerieDto> result)
    {
        Assert.True(result.IsOk, result.Error);
        return result.Value!;
    }

    private static SerieDocumentalService NewService(IApplicationDbContext ctx, SeedData seed)
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
                Email = $"user-{tenantId}@series.test",
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
