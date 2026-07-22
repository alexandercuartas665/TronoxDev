using Microsoft.EntityFrameworkCore;
using Tronox.Application.Roles;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Persistence;

namespace Tronox.Integration.Tests;

/// <summary>
/// Tests de la SIEMBRA de roles predeterminados (RQ01 - RF05) sobre PostgreSQL real.
///
/// Usa la implementacion REAL (RolProvisioningService), no el doble no-op: el objetivo es
/// justamente comprobar que el tenant nace con sus 7 roles, que la siembra es IDEMPOTENTE y que
/// el Owner queda anclado a "Super Administrador" (sin ese anclaje, el enforcement fail-closed
/// dejaria al tenant recien creado sin nadie capaz de repartir permisos).
/// </summary>
public sealed class RolProvisioningTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private readonly PostgresTenantIsolationFixture _fixture;

    public RolProvisioningTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Siembra_CreaLosSieteRolesPredeterminados()
    {
        var tenantId = await NuevoTenantConNivelesAsync("Siembra Roles");

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        var roles = await read.Roles.AsNoTracking().Where(r => r.TenantId == tenantId).ToListAsync();

        Assert.Equal(7, roles.Count);
        Assert.All(roles, r => Assert.True(r.IsSystem));
        Assert.All(roles, r => Assert.Equal(RolEstado.Activo, r.Estado));
        // Todos con nivel de acceso asignado (FK obligatorio).
        Assert.All(roles, r => Assert.NotEqual(0, r.NivelAccesoMaximoId));

        Assert.Equal(
            RolCatalogo.Codigos.OrderBy(c => c, StringComparer.Ordinal),
            roles.Select(r => r.CodigoSistema!).OrderBy(c => c, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(RolCatalogo.CodigoSuperAdministrador, "Super Administrador", 4)]
    [InlineData(RolCatalogo.CodigoAdministrador, "Administrador", 4)]
    [InlineData(RolCatalogo.CodigoAdministradorArchivo, "Administrador de Archivo", 3)]
    [InlineData(RolCatalogo.CodigoRadicador, "Radicador", 2)]
    [InlineData(RolCatalogo.CodigoArchivista, "Archivista", 3)]
    [InlineData(RolCatalogo.CodigoConsultaGeneral, "Consulta General", 1)]
    public async Task Siembra_CadaRolQuedaConSuNivelDeLaSpec(string codigo, string nombre, int nivelOrdenEsperado)
    {
        var tenantId = await NuevoTenantConNivelesAsync($"Nivel {codigo}");
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        var rol = await read.Roles.AsNoTracking()
            .Include(r => r.NivelAccesoMaximo)
            .SingleAsync(r => r.TenantId == tenantId && r.CodigoSistema == codigo);

        Assert.Equal(nombre, rol.Name);
        Assert.Equal(nivelOrdenEsperado, rol.NivelAccesoMaximo!.NivelOrden);
    }

    [Fact]
    public async Task Siembra_EsIdempotente()
    {
        var tenantId = await NuevoTenantConNivelesAsync("Siembra Idempotente");

        for (var i = 0; i < 3; i++)
        {
            await using var ctx = _fixture.CreateContext(tenantId);
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        Assert.Equal(7, await read.Roles.CountAsync(r => r.TenantId == tenantId));
        // Tampoco duplica las asignaciones del Owner ni las filas de permiso.
        Assert.Equal(
            await read.UsuariosRoles.CountAsync(ur => ur.TenantId == tenantId),
            await read.UsuariosRoles.Select(ur => ur.Id).Distinct().CountAsync());
    }

    [Fact]
    public async Task Siembra_NoRevierteUnRolRenombradoPorElTenant()
    {
        // La identidad de un rol predeterminado es su CodigoSistema, NO su nombre: si fuera el
        // nombre, resembrar despues de renombrar "Lider de Dependencia" crearia un duplicado.
        var tenantId = await NuevoTenantConNivelesAsync("Siembra Renombrado");
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var lider = await ctx.Roles.SingleAsync(r => r.CodigoSistema == RolCatalogo.CodigoLiderDependencia);
            lider.Name = "Secretario General";
            await ctx.SaveChangesAsync();
        }

        // Resembrar no debe recrearlo ni devolverle el nombre original.
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        Assert.Equal(7, await read.Roles.CountAsync(r => r.TenantId == tenantId));
        var lider2 = await read.Roles.AsNoTracking()
            .SingleAsync(r => r.CodigoSistema == RolCatalogo.CodigoLiderDependencia);
        Assert.Equal("Secretario General", lider2.Name);
    }

    [Fact]
    public async Task LiderDeDependencia_NaceRenombrable_YLosDemasNo()
    {
        var tenantId = await NuevoTenantConNivelesAsync("Siembra Lider");
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        var roles = await read.Roles.AsNoTracking().Where(r => r.TenantId == tenantId).ToListAsync();

        var lider = roles.Single(r => r.CodigoSistema == RolCatalogo.CodigoLiderDependencia);
        Assert.True(lider.AllowRename);
        Assert.True(lider.IsSystem);   // renombrable, pero NO eliminable

        Assert.All(
            roles.Where(r => r.CodigoSistema != RolCatalogo.CodigoLiderDependencia),
            r => Assert.False(r.AllowRename));
    }

    [Fact]
    public async Task SuperAdministrador_NaceConLaMatrizCompletaDelMenu()
    {
        // Esto es lo que sustituye al bypass "AllowAll": el acceso total es una MATRIZ auditable.
        var tenantId = await NuevoTenantConNivelesAsync("Siembra Matriz");
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await SembrarMenuAsync(ctx, tenantId);
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        var superAdmin = await read.Roles.AsNoTracking()
            .SingleAsync(r => r.CodigoSistema == RolCatalogo.CodigoSuperAdministrador);

        var filas = await read.RolPermisos.AsNoTracking()
            .Where(p => p.RolId == superAdmin.Id).ToListAsync();

        // 2 modulos Ready del menu x 6 acciones = 12 filas, todas concedidas.
        Assert.Equal(12, filas.Count);
        Assert.All(filas, f => Assert.True(f.Permitido));
        foreach (var modulo in new[] { "expedientes", "radicacion" })
        {
            Assert.Equal(
                PermissionActions.All.OrderBy(a => a),
                filas.Where(f => f.Modulo == modulo).Select(f => f.Accion).OrderBy(a => a));
        }

        // Y un rol SIN matriz completa (ej. Radicador) nace sin ninguna fila: se la define el tenant.
        var radicador = await read.Roles.AsNoTracking()
            .SingleAsync(r => r.CodigoSistema == RolCatalogo.CodigoRadicador);
        Assert.Equal(0, await read.RolPermisos.CountAsync(p => p.RolId == radicador.Id));
    }

    [Fact]
    public async Task Siembra_AnclaAlOwnerA_SuperAdministrador()
    {
        // Sin este anclaje el tenant nuevo nace inutilizable: fail-closed + nadie con permisos.
        var tenantId = await NuevoTenantConNivelesAsync("Siembra Owner");
        long ownerTenantUserId, advisorTenantUserId;

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await SembrarMenuAsync(ctx, tenantId);

            var ownerPu = new PlatformUser { Email = "owner@siembra.local", DisplayName = "Owner" };
            var advisorPu = new PlatformUser { Email = "adv@siembra.local", DisplayName = "Adv" };
            ctx.PlatformUsers.AddRange(ownerPu, advisorPu);
            var owner = new TenantUser { TenantId = tenantId, PlatformUser = ownerPu, Email = "owner@siembra.local", TenantRole = TenantRole.Owner };
            var advisor = new TenantUser { TenantId = tenantId, PlatformUser = advisorPu, Email = "adv@siembra.local", TenantRole = TenantRole.Advisor };
            ctx.TenantUsers.AddRange(owner, advisor);
            await ctx.SaveChangesAsync();
            ownerTenantUserId = owner.Id;
            advisorTenantUserId = advisor.Id;

            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        var superAdminId = await read.Roles.AsNoTracking()
            .Where(r => r.CodigoSistema == RolCatalogo.CodigoSuperAdministrador)
            .Select(r => r.Id).SingleAsync();

        // El Owner queda anclado, permanente y sin expiracion...
        var asignacion = await read.UsuariosRoles.AsNoTracking()
            .SingleAsync(ur => ur.TenantUserId == ownerTenantUserId);
        Assert.Equal(superAdminId, asignacion.RolId);
        Assert.Null(asignacion.VigenteDesde);
        Assert.Null(asignacion.VigenteHasta);

        // ...y el Advisor NO: el anclaje es solo para quien gobierna el tenant.
        Assert.False(await read.UsuariosRoles.AnyAsync(ur => ur.TenantUserId == advisorTenantUserId));
    }

    [Fact]
    public async Task Siembra_NoPisaLosRolesDeUnUsuarioQueYaTiene()
    {
        var tenantId = await NuevoTenantConNivelesAsync("Siembra NoPisa");
        long ownerTenantUserId, rolPropioId;

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var nivelId = await RolesTestHelpers.NivelIdAsync(ctx, tenantId, RolesTestHelpers.NivelPublico);
            var propio = new Rol
            {
                TenantId = tenantId,
                Name = "Rol propio",
                NivelAccesoMaximoId = nivelId,
                Estado = RolEstado.Activo,
                AllowRename = true
            };
            ctx.Roles.Add(propio);

            var pu = new PlatformUser { Email = "own2@siembra.local", DisplayName = "Owner" };
            ctx.PlatformUsers.Add(pu);
            var owner = new TenantUser { TenantId = tenantId, PlatformUser = pu, Email = "own2@siembra.local", TenantRole = TenantRole.Owner };
            ctx.TenantUsers.Add(owner);
            await ctx.SaveChangesAsync();

            ownerTenantUserId = owner.Id;
            rolPropioId = propio.Id;
            ctx.UsuariosRoles.Add(new UsuarioRol { TenantId = tenantId, TenantUserId = owner.Id, RolId = propio.Id });
            await ctx.SaveChangesAsync();

            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        // El Owner ya tenia una asignacion: la siembra no le agrega Super Administrador encima.
        await using var read = _fixture.CreateContext(tenantId);
        var asignacion = await read.UsuariosRoles.AsNoTracking()
            .SingleAsync(ur => ur.TenantUserId == ownerTenantUserId);
        Assert.Equal(rolPropioId, asignacion.RolId);
    }

    [Fact]
    public async Task SinNivelesDeClasificacion_NoSiembraNada()
    {
        // nivel_acceso_maximo es FK obligatorio: sin niveles, sembrar a medias seria peor.
        var tenantId = TestIds.Next();
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "Sin Niveles" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(tenantId);
        }

        await using var read = _fixture.CreateContext(tenantId);
        Assert.Equal(0, await read.Roles.CountAsync(r => r.TenantId == tenantId));
    }

    [Fact]
    public async Task Siembra_EsPorTenant_NoSeFiltraEntreTenants()
    {
        var a = await NuevoTenantConNivelesAsync("Siembra A");
        var b = await NuevoTenantConNivelesAsync("Siembra B");

        await using (var ctx = _fixture.CreateContext(a))
        {
            await new RolProvisioningService(ctx).EnsureRolesPredeterminadosAsync(a);
        }

        await using var read = _fixture.CreateContext(tenantId: null);
        Assert.Equal(7, await read.Roles.IgnoreQueryFilters().CountAsync(r => r.TenantId == a));
        Assert.Equal(0, await read.Roles.IgnoreQueryFilters().CountAsync(r => r.TenantId == b));
    }

    // ---- Helpers ----

    private async Task<long> NuevoTenantConNivelesAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await RolesTestHelpers.SeedNivelesYObtenerIdAsync(ctx, tenantId);
        }
        return tenantId;
    }

    /// <summary>Menu minimo con 2 modulos Ready, para la matriz completa del Super Administrador.</summary>
    private static async Task SembrarMenuAsync(TronoxDbContext ctx, long tenantId)
    {
        var view = new MenuView { TenantId = tenantId, Name = "Completo", IsDefault = true, SortOrder = 0 };
        ctx.MenuViews.Add(view);
        var sec = new MenuNode { TenantId = tenantId, MenuView = view, Kind = MenuNodeKind.Section, Name = "Gestion", Route = "gest", SortOrder = 0 };
        ctx.MenuNodes.Add(sec);
        ctx.MenuNodes.Add(new MenuNode { TenantId = tenantId, MenuView = view, Parent = sec, Kind = MenuNodeKind.Item, Name = "Expedientes", Route = "expedientes", State = MenuNodeState.Ready, SortOrder = 0 });
        ctx.MenuNodes.Add(new MenuNode { TenantId = tenantId, MenuView = view, Parent = sec, Kind = MenuNodeKind.Item, Name = "Radicacion", Route = "radicacion", State = MenuNodeState.Ready, SortOrder = 1 });
        // En desarrollo: NO entra en la matriz.
        ctx.MenuNodes.Add(new MenuNode { TenantId = tenantId, MenuView = view, Parent = sec, Kind = MenuNodeKind.Item, Name = "Stub", Route = "stub", State = MenuNodeState.InDevelopment, SortOrder = 2 });
        await ctx.SaveChangesAsync();
    }
}
