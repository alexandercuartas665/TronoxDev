using Microsoft.EntityFrameworkCore;
using Tronox.Application.Admin;
using Tronox.Application.Common;
using Tronox.Application.MenuConfig;
using Tronox.Application.Roles;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Persistence;

namespace Tronox.Integration.Tests;

/// <summary>
/// Tests del ALTA DE TENANT completa (menu -> niveles -> roles) sobre PostgreSQL real, con las
/// implementaciones REALES de los tres aprovisionamientos, no los dobles no-op.
///
/// MOTIVO: un tenant dado de alta contra la aplicacion nacio con sus 7 roles y sus 4 niveles, pero
/// con rol_permisos VACIA para TODOS los roles, incluido Super Administrador. La cadena causal era
/// que el catalogo de modulos SE DERIVA del menu (nodos Item con ruta) y el menu sembrado solo
/// traia el acceso Inicio y las 7 secciones: cero modulos -> cero filas de matriz -> con el
/// enforcement fail-closed de ADR-004, un administrador que entra y no ve absolutamente nada.
///
/// Ninguna prueba lo detectaba porque los tests de matriz sembraban su propio menu de juguete con
/// dos items. Estos tests corren el arbol canonico de verdad.
/// </summary>
public sealed class MenuProvisioningTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private readonly PostgresTenantIsolationFixture _fixture;

    public MenuProvisioningTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    /// <summary>
    /// EL test de esta correccion. Da de alta un tenant por el camino real (TenantAdminService) y
    /// exige que el Super Administrador nazca con matriz, y que esa matriz cubra EXACTAMENTE los
    /// modulos que el menu sembrado declara.
    /// </summary>
    [Fact]
    public async Task AltaDeTenant_ElSuperAdministradorNaceConLaMatrizCompleta()
    {
        var tenantId = await AltaDeTenantAsync("Alta Fail Closed");

        await using var read = _fixture.CreateContext(tenantId);

        var superAdminId = await read.Roles.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.CodigoSistema == RolCatalogo.CodigoSuperAdministrador)
            .Select(r => r.Id)
            .SingleAsync();

        var filas = await read.RolPermisos.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.RolId == superAdminId)
            .ToListAsync();

        // 1. La matriz NO esta vacia: es literalmente el defecto reportado.
        Assert.NotEmpty(filas);

        // 2. Los modulos de la matriz coinciden con los items navegables del menu sembrado.
        var vistaId = await read.MenuViews.IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && v.IsDefault)
            .Select(v => v.Id)
            .SingleAsync();

        var rutasDelMenu = await read.MenuNodes.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.MenuViewId == vistaId
                && n.Kind == MenuNodeKind.Item
                && n.Route != null && n.Route != "")
            .Select(n => n.Route!)
            .ToListAsync();

        var modulosDeLaMatriz = filas.Select(f => f.Modulo).Distinct().OrderBy(m => m, StringComparer.Ordinal);

        Assert.Equal(
            rutasDelMenu.Distinct().OrderBy(m => m, StringComparer.Ordinal),
            modulosDeLaMatriz);

        // 3. Y el menu sembrado es el arbol canonico, no un subconjunto.
        Assert.Equal(
            MenuCatalogo.RutasDeItem.OrderBy(r => r, StringComparer.Ordinal),
            rutasDelMenu.OrderBy(r => r, StringComparer.Ordinal));

        // 4. Cada modulo con sus 6 acciones concedidas: es lo que sustituye al bypass AllowAll.
        Assert.Equal(MenuCatalogo.RutasDeItem.Count * PermissionActions.All.Count, filas.Count);
        Assert.All(filas, f => Assert.True(f.Permitido));
    }

    /// <summary>
    /// El otro rol con matriz completa (Administrador) tambien nace con ella, y un rol operativo
    /// (Radicador) sigue naciendo SIN filas: su matriz la define el tenant.
    /// </summary>
    [Fact]
    public async Task AltaDeTenant_SoloLosRolesDeGobiernoNacenConMatriz()
    {
        var tenantId = await AltaDeTenantAsync("Alta Matriz Por Rol");

        await using var read = _fixture.CreateContext(tenantId);
        var esperado = MenuCatalogo.RutasDeItem.Count * PermissionActions.All.Count;

        foreach (var codigo in new[] { RolCatalogo.CodigoSuperAdministrador, RolCatalogo.CodigoAdministrador })
        {
            Assert.Equal(esperado, await ContarMatrizAsync(read, tenantId, codigo));
        }

        foreach (var codigo in new[]
        {
            RolCatalogo.CodigoRadicador, RolCatalogo.CodigoArchivista,
            RolCatalogo.CodigoAdministradorArchivo, RolCatalogo.CodigoConsultaGeneral,
            RolCatalogo.CodigoLiderDependencia
        })
        {
            Assert.Equal(0, await ContarMatrizAsync(read, tenantId, codigo));
        }
    }

    /// <summary>El arbol sembrado es el del MAPA: secciones, modulos y pantallas.</summary>
    [Fact]
    public async Task Siembra_PersisteElArbolCanonicoCompleto()
    {
        var tenantId = await AltaDeTenantAsync("Alta Arbol");

        await using var read = _fixture.CreateContext(tenantId);
        var nodos = await NodosAsync(read, tenantId);

        Assert.Equal(MenuCatalogo.TotalNodos, nodos.Count);
        Assert.Equal(1, nodos.Count(n => n.Kind == MenuNodeKind.QuickLink));
        Assert.Equal(MenuCatalogo.Secciones.Count, nodos.Count(n => n.Kind == MenuNodeKind.Section));
        Assert.Equal(
            MenuCatalogo.Secciones.Sum(s => s.Grupos.Count),
            nodos.Count(n => n.Kind == MenuNodeKind.Subgroup));
        Assert.Equal(MenuCatalogo.RutasDeItem.Count, nodos.Count(n => n.Kind == MenuNodeKind.Item));

        // Todo item nace navegable: un item InDevelopment quedaria FUERA de la matriz, que es
        // justamente como el modulo terminaria inaccesible.
        Assert.All(nodos.Where(n => n.Kind == MenuNodeKind.Item), n =>
        {
            Assert.Equal(MenuNodeState.Ready, n.State);
            Assert.True(n.IsVisible);
            Assert.False(string.IsNullOrWhiteSpace(n.Route));
        });
    }

    /// <summary>RF09 5.9.5.3 sobre lo PERSISTIDO: ningun nodo rompe las reglas de anidamiento.</summary>
    [Fact]
    public async Task Siembra_CumpleLasReglasDeAnidamientoDeRF09()
    {
        var tenantId = await AltaDeTenantAsync("Alta Anidamiento");

        await using var read = _fixture.CreateContext(tenantId);
        var nodos = await NodosAsync(read, tenantId);
        var porId = nodos.ToDictionary(n => n.Id);

        foreach (var nodo in nodos)
        {
            MenuNodeKind? padre = nodo.ParentId is long pid ? porId[pid].Kind : null;
            var error = MenuNodeKindRules.Validate(nodo.Kind, padre);
            Assert.True(error is null, $"{nodo.Kind} '{nodo.Name}' bajo {padre?.ToString() ?? "raiz"}: {error}");
        }

        // Y en concreto lo que RF09 pone como ejemplo de invalido: una Seccion dentro de un Item.
        Assert.DoesNotContain(nodos, n =>
            n.Kind == MenuNodeKind.Section && n.ParentId is not null);
        Assert.DoesNotContain(nodos, n =>
            n.ParentId is long pid && porId[pid].Kind == MenuNodeKind.Item);
    }

    /// <summary>
    /// RF09 5.9.4 punto 3: repetir el aprovisionamiento no duplica nodos ni filas de matriz.
    /// </summary>
    [Fact]
    public async Task Siembra_EsIdempotente()
    {
        var tenantId = await AltaDeTenantAsync("Alta Idempotente");

        int nodosAntes, matrizAntes;
        await using (var read = _fixture.CreateContext(tenantId))
        {
            nodosAntes = (await NodosAsync(read, tenantId)).Count;
            matrizAntes = await read.RolPermisos.IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenantId);
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new MenuProvisioningService(ctx).EnsureDefaultMenuAsync(tenantId);
            await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(tenantId);
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using (var read = _fixture.CreateContext(tenantId))
        {
            Assert.Equal(nodosAntes, (await NodosAsync(read, tenantId)).Count);
            Assert.Equal(matrizAntes, await read.RolPermisos.IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenantId));
            Assert.Equal(1, await read.MenuViews.IgnoreQueryFilters().CountAsync(v => v.TenantId == tenantId));
        }
    }

    /// <summary>
    /// La siembra NO revierte personalizaciones: si el tenant renombro un nodo o denego una accion
    /// del Super Administrador, volver a aprovisionar respeta ambas decisiones.
    /// </summary>
    [Fact]
    public async Task Siembra_NoRevierteLasPersonalizacionesDelTenant()
    {
        var tenantId = await AltaDeTenantAsync("Alta Personalizada");
        long nodoId, superAdminId, permisoId;

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var nodo = await ctx.MenuNodes.IgnoreQueryFilters()
                .FirstAsync(n => n.TenantId == tenantId && n.Kind == MenuNodeKind.Section);
            nodo.Name = "MI SECCION";
            nodo.IsVisible = false;
            nodoId = nodo.Id;

            superAdminId = await ctx.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.CodigoSistema == RolCatalogo.CodigoSuperAdministrador)
                .Select(r => r.Id).SingleAsync();

            var permiso = await ctx.RolPermisos.IgnoreQueryFilters()
                .OrderBy(p => p.Id)
                .FirstAsync(p => p.RolId == superAdminId && p.Accion == PermissionAction.Delete);
            permiso.Permitido = false;
            permisoId = permiso.Id;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new MenuProvisioningService(ctx).EnsureDefaultMenuAsync(tenantId);
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        var releido = await read.MenuNodes.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(n => n.Id == nodoId);
        Assert.Equal("MI SECCION", releido.Name);
        Assert.False(releido.IsVisible);

        // La denegacion explicita sobrevive: la matriz solo RELLENA los pares sin fila.
        Assert.False(await read.RolPermisos.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == permisoId)
            .Select(p => p.Permitido)
            .SingleAsync());

        // Y no se duplico la fila denegada al reaprovisionar.
        Assert.Equal(
            MenuCatalogo.RutasDeItem.Count * PermissionActions.All.Count,
            await ContarMatrizAsync(read, tenantId, RolCatalogo.CodigoSuperAdministrador));
    }

    /// <summary>
    /// REGRESION EXACTA del tenant averiado: vista sembrada por la version incompleta del servicio
    /// (Inicio + 7 secciones, ningun item) y roles ya creados con la matriz vacia. Reaprovisionar
    /// tiene que COMPLETAR el arbol y RELLENAR la matriz, sin duplicar las secciones existentes.
    /// </summary>
    [Fact]
    public async Task Reaprovisionar_ReparaUnTenantQueNacioSinModulos()
    {
        var tenantId = TestIds.Next();
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant Averiado" });
            await ctx.SaveChangesAsync();
        }

        // Menu incompleto tal como lo dejaba la version anterior del aprovisionamiento.
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var vista = new MenuView
            {
                TenantId = tenantId,
                Name = MenuCatalogo.NombreVistaPredeterminada,
                IsDefault = true,
                SortOrder = 0
            };
            ctx.MenuViews.Add(vista);
            ctx.MenuNodes.Add(new MenuNode
            {
                TenantId = tenantId,
                MenuView = vista,
                Kind = MenuNodeKind.QuickLink,
                Name = "Inicio",
                Route = MenuCatalogo.Inicio.Ruta,
                SortOrder = 0
            });
            var orden = 1;
            foreach (var seccion in MenuCatalogo.Secciones)
            {
                ctx.MenuNodes.Add(new MenuNode
                {
                    TenantId = tenantId,
                    MenuView = vista,
                    Kind = MenuNodeKind.Section,
                    Name = seccion.Nombre,
                    IconKey = seccion.Icono,
                    Route = seccion.Slug,
                    SortOrder = orden++
                });
            }
            await ctx.SaveChangesAsync();

            await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(tenantId);
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        // El punto de partida: 7 roles y CERO permisos. Asi nacio el tenant reportado.
        await using (var read = _fixture.CreateContext(tenantId))
        {
            Assert.Equal(7, await read.Roles.IgnoreQueryFilters().CountAsync(r => r.TenantId == tenantId));
            Assert.Equal(0, await read.RolPermisos.IgnoreQueryFilters().CountAsync(p => p.TenantId == tenantId));
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new MenuProvisioningService(ctx).EnsureDefaultMenuAsync(tenantId);
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using (var read = _fixture.CreateContext(tenantId))
        {
            var nodos = await NodosAsync(read, tenantId);
            Assert.Equal(MenuCatalogo.TotalNodos, nodos.Count);
            // Las 7 secciones preexistentes se reutilizaron: no hay duplicados.
            Assert.Equal(MenuCatalogo.Secciones.Count, nodos.Count(n => n.Kind == MenuNodeKind.Section));
            Assert.Equal(1, await read.MenuViews.IgnoreQueryFilters().CountAsync(v => v.TenantId == tenantId));

            Assert.Equal(
                MenuCatalogo.RutasDeItem.Count * PermissionActions.All.Count,
                await ContarMatrizAsync(read, tenantId, RolCatalogo.CodigoSuperAdministrador));
        }
    }

    /// <summary>Aislamiento (DAT-01): sembrar el tenant A no toca al tenant B.</summary>
    [Fact]
    public async Task Siembra_EsPorTenant_NoSeFiltraEntreTenants()
    {
        var a = await AltaDeTenantAsync("Alta Aislada A");
        var b = await AltaDeTenantAsync("Alta Aislada B");

        await using var read = _fixture.CreateContext(tenantId: null);
        foreach (var tenantId in new[] { a, b })
        {
            Assert.Equal(MenuCatalogo.TotalNodos,
                await read.MenuNodes.IgnoreQueryFilters().CountAsync(n => n.TenantId == tenantId));
        }

        // Ningun nodo de A quedo colgando de una vista de B.
        var vistasDeA = await read.MenuViews.IgnoreQueryFilters()
            .Where(v => v.TenantId == a).Select(v => v.Id).ToListAsync();
        Assert.False(await read.MenuNodes.IgnoreQueryFilters()
            .AnyAsync(n => n.TenantId == b && vistasDeA.Contains(n.MenuViewId)));
    }

    // ---- Helpers ----

    /// <summary>Alta REAL de un tenant: el mismo camino que usa la aplicacion, sin dobles no-op.</summary>
    private async Task<long> AltaDeTenantAsync(string nombre)
    {
        await using var ctx = _fixture.CreateContext(tenantId: null);
        var servicio = new TenantAdminService(
            ctx,
            new AuditWriter(ctx),
            new MenuProvisioningService(ctx),
            new ClasificacionProvisioningService(ctx),
            new RolProvisioningService(ctx));

        var creado = await servicio.CreateAsync(new CreateTenantRequest(nombre), TestIds.Next());
        return creado.Id;
    }

    private static Task<List<MenuNode>> NodosAsync(TronoxDbContext ctx, long tenantId) =>
        ctx.MenuNodes.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.TenantId == tenantId)
            .ToListAsync();

    private static async Task<int> ContarMatrizAsync(TronoxDbContext ctx, long tenantId, string codigoRol)
    {
        var rolId = await ctx.Roles.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.CodigoSistema == codigoRol)
            .Select(r => r.Id)
            .SingleAsync();

        return await ctx.RolPermisos.IgnoreQueryFilters().CountAsync(p => p.RolId == rolId);
    }
}
