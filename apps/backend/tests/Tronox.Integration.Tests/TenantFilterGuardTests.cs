using Tronox.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Guarda ESTRUCTURAL del aislamiento multi-tenant (DAT-01).
///
/// Existe por un incidente real: durante la poda del backbone se elimino por accidente la
/// linea que aplica HasQueryFilter en TronoxDbContext.ApplyTenantFilter. El metodo quedo con
/// el cuerpo vacio, la solucion siguio compilando, la aplicacion siguio arrancando y el
/// sistema habria servido datos de todos los tenants sin una sola senal de error.
///
/// Los tests de TenantIsolationTests comprueban el COMPORTAMIENTO con dos tenants sembrados.
/// Estos comprueban la ESTRUCTURA del modelo, y fallan aunque nadie escriba un caso de prueba
/// para una entidad nueva. Son la red que atrapa la regresion silenciosa dentro de seis meses.
/// </summary>
public sealed class TenantFilterGuardTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private readonly PostgresTenantIsolationFixture _fixture;

    public TenantFilterGuardTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public void EveryTenantScopedEntity_HasAGlobalQueryFilter()
    {
        using var db = _fixture.CreateContext(tenantId: 1);

        var sinFiltro = db.Model.GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            sinFiltro.Count == 0,
            "Estas entidades implementan ITenantScoped pero NO tienen filtro global de tenant, " +
            "asi que sus consultas devuelven filas de todos los tenants: " +
            string.Join(", ", sinFiltro));
    }

    [Fact]
    public void AtLeastOneEntity_IsTenantScoped()
    {
        // Blindaje del test anterior: si el modelo se quedara sin entidades ITenantScoped
        // (por ejemplo tras un refactor que rompa la herencia de TenantEntity), la primera
        // prueba pasaria en vacio y no protegeria nada.
        using var db = _fixture.CreateContext(tenantId: 1);

        var scoped = db.Model.GetEntityTypes()
            .Count(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType));

        Assert.True(scoped > 0, "Ninguna entidad del modelo implementa ITenantScoped.");
    }

    [Fact]
    public void EveryTenantScopedEntity_HasTenantIdAsNonNullableBigint()
    {
        // DAT-01: tenant_id es obligatorio en toda tabla scoped. Una columna nullable
        // permitiria filas huerfanas que ningun filtro por tenant devolveria.
        using var db = _fixture.CreateContext(tenantId: 1);

        var malDeclaradas = db.Model.GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .Select(e => new { Entidad = e.ClrType.Name, Prop = e.FindProperty(nameof(ITenantScoped.TenantId)) })
            .Where(x => x.Prop is null || x.Prop.IsNullable)
            .Select(x => x.Entidad)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            malDeclaradas.Count == 0,
            "Estas entidades no declaran tenant_id como obligatorio: " + string.Join(", ", malDeclaradas));
    }
}
