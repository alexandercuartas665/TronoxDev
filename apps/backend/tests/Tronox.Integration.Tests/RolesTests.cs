using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.MenuConfig;
using Tronox.Application.Roles;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Persistence;

namespace Tronox.Integration.Tests;

/// <summary>
/// Tests de integracion de roles y permisos (RQ01 - RF05) sobre PostgreSQL real (Testcontainers).
///
/// Cubre lo que no se puede probar sin base de datos: la matriz persistida UNA FILA POR
/// (modulo, accion), el multi-rol por la puente usuarios_roles con vigencia, las reglas de los
/// roles de sistema, el aislamiento cross-tenant y el catalogo derivado del menu.
///
/// El invariante 10 (FAIL-CLOSED) se verifica aqui de punta a punta: un usuario sin roles, o con
/// roles vencidos, resuelve a SIN PERMISOS contra la base de datos real.
/// </summary>
public abstract class RolesTestsBase
{
    private readonly TenantIsolationDbFixture _fixture;

    protected RolesTestsBase(TenantIsolationDbFixture fixture) => _fixture = fixture;

    // ---- Matriz: una fila por (modulo, accion) ----

    [Fact]
    public async Task CreateRole_SavePermisos_RoundTrips()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles RoundTrip");

        var created = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(null, "QA", "rol de prueba", nivelId, RolEstado.Activo), TestIds.Next()));
        Assert.True(created.IsOk, created.Error);
        var rolId = created.Value!.Id;

        var permisos = new List<ModulePermissionDto>
        {
            new("inventario-items", true, true, false, false),
            new("actividades", true, false, false, false),
            new("vacio", false, false, false, false) // no debe persistir
        };
        var saved = await RunAsync(tenantId, s => s.SavePermisosAsync(rolId, permisos, TestIds.Next()));
        Assert.True(saved.IsOk, saved.Error);

        var detail = await RunAsync(tenantId, s => s.GetAsync(rolId));
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Permisos.Count); // la fila vacia no se guardo
        var inv = detail.Permisos.Single(p => p.ModuleKey == "inventario-items");
        Assert.True(inv.CanView);
        Assert.True(inv.CanCreate);
        Assert.False(inv.CanDelete);
    }

    [Fact]
    public async Task LasSeisAcciones_SePersistenYSeResuelven()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles SeisAcciones");
        var rolId = await NuevoRolAsync(tenantId, "Completo", nivelId);

        // Un modulo con las 6 acciones y otro solo con exportar/imprimir.
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
        [
            new ModulePermissionDto("expedientes", true, true, true, true, true, true),
            new ModulePermissionDto("reportes", false, false, false, false, true, true)
        ], TestIds.Next()));

        // En BD debe haber UNA FILA POR (modulo, accion): 6 + 2 = 8, y ninguna con Permitido=false.
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var filas = await ctx.RolPermisos.AsNoTracking()
                .Where(p => p.RolId == rolId).ToListAsync();

            Assert.Equal(8, filas.Count);
            Assert.All(filas, f => Assert.True(f.Permitido));
            Assert.Equal(6, filas.Count(f => f.Modulo == "expedientes"));
            Assert.Equal(
                [PermissionAction.Export, PermissionAction.Print],
                filas.Where(f => f.Modulo == "reportes").Select(f => f.Accion).OrderBy(a => a).ToList());
        }

        // Y al releer, la proyeccion por modulo devuelve las 6 marcadas.
        var detail = await RunAsync(tenantId, s => s.GetAsync(rolId));
        var exp = detail!.Permisos.Single(p => p.ModuleKey == "expedientes");
        foreach (var accion in PermissionActions.All)
        {
            Assert.True(exp.Can(accion), $"expedientes deberia conceder {accion}");
        }
        var rep = detail.Permisos.Single(p => p.ModuleKey == "reportes");
        Assert.True(rep.CanExport);
        Assert.True(rep.CanPrint);
        Assert.False(rep.CanView);
    }

    [Fact]
    public async Task SavePermisos_IsReplacedOnResave()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles Resave");
        var rolId = await NuevoRolAsync(tenantId, "Reasigna", nivelId);

        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("a", true, false, false, false),
             new ModulePermissionDto("b", true, false, false, false)], TestIds.Next()));
        // Reguardar con un set distinto: borra e reinserta (no acumula).
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("c", true, true, false, false)], TestIds.Next()));

        var detail = await RunAsync(tenantId, s => s.GetAsync(rolId));
        Assert.Single(detail!.Permisos);
        Assert.Equal("c", detail.Permisos[0].ModuleKey);
    }

    // ---- FAIL-CLOSED de punta a punta ----

    [Fact]
    public async Task UsuarioSinRol_NoTienePermisos()
    {
        // El invariante 10 contra base de datos real: sin asignaciones, SIN PERMISOS.
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles SinRol");
        var (platformUserId, _) = await NuevoUsuarioAsync(tenantId, "sinrol@roles.local", TenantRole.Advisor);

        // Existe un rol con permisos en el tenant, pero este usuario no lo tiene asignado.
        var rolId = await NuevoRolAsync(tenantId, "Operativo", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("inventario-items", true, true, false, false)], TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));

        Assert.True(eff.IsEmpty);
        Assert.Empty(eff.RolIds);
        Assert.Null(eff.NivelAccesoMaximoOrden);
        Assert.False(eff.Can("inventario-items", PermissionAction.View));
    }

    [Fact]
    public async Task UsuarioOwnerSinRol_TampocoTienePermisos()
    {
        // El Owner NO obtiene acceso total por su TenantRole: se elimino ese bypass. Si debe
        // poder todo, es porque tiene asignado un rol cuya matriz lo dice.
        var tenantId = await NewTenantAsync("Roles OwnerSinRol");
        var (platformUserId, _) = await NuevoUsuarioAsync(tenantId, "owner@roles.local", TenantRole.Owner);

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));

        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("cualquier-cosa", PermissionAction.Delete));
        Assert.False(eff.Can("cualquier-cosa", PermissionAction.View));
    }

    [Fact]
    public async Task UsuarioInexistenteEnElTenant_NoTienePermisos()
    {
        var tenantId = await NewTenantAsync("Roles Desconocido");

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(TestIds.Next()));

        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("lo-que-sea", PermissionAction.View));
    }

    // ---- Multi-rol: union y nivel maximo ----

    [Fact]
    public async Task DosRoles_LosPermisosSeUnenPorOr()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles Union");
        var (platformUserId, tenantUserId) = await NuevoUsuarioAsync(tenantId, "multi@roles.local", TenantRole.Advisor);

        var rolA = await NuevoRolAsync(tenantId, "Consulta", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolA,
            [new ModulePermissionDto("expedientes", true, false, false, false)], TestIds.Next()));

        var rolB = await NuevoRolAsync(tenantId, "Edicion", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolB,
            [new ModulePermissionDto("expedientes", false, false, true, false),
             new ModulePermissionDto("radicacion", true, false, false, false)], TestIds.Next()));

        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, rolA, null, null, TestIds.Next()));
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, rolB, null, null, TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));

        Assert.Equal(2, eff.RolIds.Count);
        Assert.True(eff.Can("expedientes", PermissionAction.View));   // de A
        Assert.True(eff.Can("expedientes", PermissionAction.Edit));   // de B
        Assert.True(eff.Can("radicacion", PermissionAction.View));    // solo de B
        Assert.False(eff.Can("expedientes", PermissionAction.Delete)); // de ninguno
    }

    [Fact]
    public async Task DosRolesConNivelesDistintos_GanaElMasAlto()
    {
        var tenantId = await NewTenantAsync("Roles NivelMaximo");
        long nivelPublico, nivelReservado;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await RolesTestHelpers.SeedNivelesYObtenerIdAsync(ctx, tenantId);
            nivelPublico = await RolesTestHelpers.NivelIdAsync(ctx, tenantId, RolesTestHelpers.NivelPublico);
            nivelReservado = await RolesTestHelpers.NivelIdAsync(ctx, tenantId, RolesTestHelpers.NivelReservado);
        }

        var (platformUserId, tenantUserId) = await NuevoUsuarioAsync(tenantId, "niveles@roles.local", TenantRole.Advisor);
        var bajo = await NuevoRolAsync(tenantId, "Consulta General", nivelPublico);
        var alto = await NuevoRolAsync(tenantId, "Archivista", nivelReservado);

        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, bajo, null, null, TestIds.Next()));
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, alto, null, null, TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));

        // Reservado = orden 3; el usuario alcanza hasta ahi, no hasta Clasificado (4).
        Assert.Equal(3, eff.NivelAccesoMaximoOrden);
        Assert.True(eff.AlcanzaNivel(3));
        Assert.False(eff.AlcanzaNivel(4));
    }

    [Fact]
    public async Task RolConVigenteHastaPasado_NoCuenta()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles Vencido");
        var (platformUserId, tenantUserId) = await NuevoUsuarioAsync(tenantId, "vencido@roles.local", TenantRole.Advisor);

        var rolId = await NuevoRolAsync(tenantId, "Encargo temporal", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("expedientes", true, true, true, true)], TestIds.Next()));

        // Asignacion que expiro ayer: queda revocada automaticamente, sin borrar la fila.
        var ayer = DateTimeOffset.UtcNow.AddDays(-1);
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(
            tenantUserId, rolId, ayer.AddDays(-30), ayer, TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));

        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("expedientes", PermissionAction.View));

        // La fila SIGUE existiendo (es historia auditable), simplemente no concede.
        await using var ctx = _fixture.CreateContext(tenantId);
        Assert.True(await ctx.UsuariosRoles.AnyAsync(ur => ur.TenantUserId == tenantUserId && ur.RolId == rolId));
    }

    [Fact]
    public async Task RolVigenteConvive_ConUnoVencido()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles VigenteYVencido");
        var (platformUserId, tenantUserId) = await NuevoUsuarioAsync(tenantId, "mixto@roles.local", TenantRole.Advisor);

        var vencido = await NuevoRolAsync(tenantId, "Vencido", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(vencido,
            [new ModulePermissionDto("expedientes", true, false, false, true)], TestIds.Next()));
        var vigente = await NuevoRolAsync(tenantId, "Vigente", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(vigente,
            [new ModulePermissionDto("expedientes", true, false, false, false)], TestIds.Next()));

        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(
            tenantUserId, vencido, null, DateTimeOffset.UtcNow.AddDays(-1), TestIds.Next()));
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, vigente, null, null, TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));

        Assert.Equal([vigente], eff.RolIds);
        Assert.True(eff.Can("expedientes", PermissionAction.View));
        // Eliminar venia SOLO del rol vencido: no debe sobrevivir.
        Assert.False(eff.Can("expedientes", PermissionAction.Delete));
    }

    [Fact]
    public async Task RolInactivo_NoConcede()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles Inactivo");
        var (platformUserId, tenantUserId) = await NuevoUsuarioAsync(tenantId, "inact@roles.local", TenantRole.Advisor);

        var rolId = await NuevoRolAsync(tenantId, "Suspendido", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("expedientes", true, false, false, false)], TestIds.Next()));
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, rolId, null, null, TestIds.Next()));

        // Con el rol Activo concede...
        Assert.True((await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId)))
            .Can("expedientes", PermissionAction.View));

        // ...y al pasarlo a Inactivo deja de conceder, sin tocar asignaciones ni matriz.
        await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(rolId, "Suspendido", null, nivelId, RolEstado.Inactivo), TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));
        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("expedientes", PermissionAction.View));
    }

    [Fact]
    public async Task SetUserRoles_ReemplazaElConjunto()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles SetConjunto");
        var (_, tenantUserId) = await NuevoUsuarioAsync(tenantId, "set@roles.local", TenantRole.Advisor);
        var a = await NuevoRolAsync(tenantId, "A", nivelId);
        var b = await NuevoRolAsync(tenantId, "B", nivelId);
        var c = await NuevoRolAsync(tenantId, "C", nivelId);

        await RunAsync(tenantId, s => s.SetUserRolesAsync(tenantUserId,
            [new RolAsignacionDto(a, null, null, null), new RolAsignacionDto(b, null, null, null)], TestIds.Next()));
        Assert.Equal(2, (await RunAsync(tenantId, s => s.GetUserRolesAsync(tenantUserId))).Count);

        // Reemplaza el conjunto: se va A y B, entra C.
        await RunAsync(tenantId, s => s.SetUserRolesAsync(tenantUserId,
            [new RolAsignacionDto(c, null, null, null)], TestIds.Next()));

        var roles = await RunAsync(tenantId, s => s.GetUserRolesAsync(tenantUserId));
        var unico = Assert.Single(roles);
        Assert.Equal(c, unico.RolId);
    }

    // ---- Reglas de los roles ----

    [Fact]
    public async Task RoleName_IsUniquePerTenant()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles Unicidad");
        var first = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(null, "Duplicado", null, nivelId, RolEstado.Activo), TestIds.Next()));
        Assert.True(first.IsOk);

        var second = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(null, "Duplicado", null, nivelId, RolEstado.Activo), TestIds.Next()));
        Assert.False(second.IsOk);
        Assert.Equal(RolServiceStatus.Conflict, second.Status);
    }

    [Fact]
    public async Task NivelAccesoMaximo_EsObligatorioYDebeExistir()
    {
        var (tenantId, _) = await NewTenantConNivelesAsync("Roles NivelObligatorio");

        var sinNivel = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(null, "Sin nivel", null, 0, RolEstado.Activo), TestIds.Next()));
        Assert.False(sinNivel.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, sinNivel.Status);

        var nivelInexistente = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(null, "Nivel raro", null, TestIds.Next(), RolEstado.Activo), TestIds.Next()));
        Assert.False(nivelInexistente.IsOk);
    }

    [Fact]
    public async Task CrossTenant_Roles_AreIsolated()
    {
        var (a, nivelA) = await NewTenantConNivelesAsync("Roles Tenant A");
        var (b, _) = await NewTenantConNivelesAsync("Roles Tenant B");

        var inA = await RunAsync(a, s => s.SaveAsync(
            new SaveRolRequest(null, "Solo A", null, nivelA, RolEstado.Activo), TestIds.Next()));
        Assert.True(inA.IsOk);

        var bList = await RunAsync(b, s => s.ListAsync());
        Assert.DoesNotContain(bList, r => r.Id == inA.Value!.Id);

        // Leer el rol de A desde B no lo devuelve (filtro global).
        var fromB = await RunAsync(b, s => s.GetAsync(inA.Value!.Id));
        Assert.Null(fromB);
    }

    [Fact]
    public async Task RolDeSistema_NoSeElimina()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles DeleteSystem");
        var systemRolId = await NuevoRolSistemaAsync(
            tenantId, "Administrador", nivelId, RolCatalogo.CodigoAdministrador, allowRename: false);

        var res = await RunAsync(tenantId, s => s.DeleteAsync(systemRolId, TestIds.Next()));

        Assert.False(res.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, res.Status);
    }

    [Fact]
    public async Task RolDeSistema_NoSeRenombra_NiSeLeCambiaElNivel()
    {
        var tenantId = await NewTenantAsync("Roles SistemaInmutable");
        long nivelClasificado, nivelPublico;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await RolesTestHelpers.SeedNivelesYObtenerIdAsync(ctx, tenantId);
            nivelClasificado = await RolesTestHelpers.NivelIdAsync(ctx, tenantId, RolesTestHelpers.NivelClasificado);
            nivelPublico = await RolesTestHelpers.NivelIdAsync(ctx, tenantId, RolesTestHelpers.NivelPublico);
        }
        var rolId = await NuevoRolSistemaAsync(
            tenantId, "Super Administrador", nivelClasificado, RolCatalogo.CodigoSuperAdministrador, allowRename: false);

        // Renombrarlo: prohibido.
        var renombrar = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(rolId, "Otro nombre", null, nivelClasificado, RolEstado.Activo), TestIds.Next()));
        Assert.False(renombrar.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, renombrar.Status);

        // Bajarle el nivel de acceso: prohibido (seria degradar su alcance sobre lo Clasificado).
        var bajarNivel = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(rolId, "Super Administrador", null, nivelPublico, RolEstado.Activo), TestIds.Next()));
        Assert.False(bajarNivel.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, bajarNivel.Status);

        // Pero editar su descripcion (sin tocar nombre ni nivel) si se permite.
        var describir = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(rolId, "Super Administrador", "Nueva descripcion", nivelClasificado, RolEstado.Activo),
            TestIds.Next()));
        Assert.True(describir.IsOk, describir.Error);
    }

    [Fact]
    public async Task LiderDeDependencia_SI_SeRenombra_PeroNoSeElimina()
    {
        // DAT-05: es el identificador del responsable jerarquico en los workflows de RQ11, asi que
        // no se puede borrar; pero cada entidad lo llama distinto, asi que si se puede renombrar.
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles Lider");
        var rolId = await NuevoRolSistemaAsync(
            tenantId, "Lider de Dependencia", nivelId, RolCatalogo.CodigoLiderDependencia, allowRename: true);

        var renombrado = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(rolId, "Jefe de Area", null, nivelId, RolEstado.Activo), TestIds.Next()));
        Assert.True(renombrado.IsOk, renombrado.Error);
        Assert.Equal("Jefe de Area", renombrado.Value!.Name);

        // Sigue siendo de sistema y sigue sin poder eliminarse.
        var borrado = await RunAsync(tenantId, s => s.DeleteAsync(rolId, TestIds.Next()));
        Assert.False(borrado.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, borrado.Status);

        // Y aunque sea renombrable, su nivel de acceso sigue siendo inmutable.
        await using var ctx = _fixture.CreateContext(tenantId);
        var otroNivel = await RolesTestHelpers.NivelIdAsync(ctx, tenantId, RolesTestHelpers.NivelClasificado);
        var subirNivel = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(rolId, "Jefe de Area", null, otroNivel, RolEstado.Activo), TestIds.Next()));
        Assert.False(subirNivel.IsOk);
    }

    [Fact]
    public async Task NoSeBorraUnRolConUsuariosAsignados()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles DeleteWithUsers");
        var rolId = await NuevoRolAsync(tenantId, "ConUsuarios", nivelId);
        var (_, tenantUserId) = await NuevoUsuarioAsync(tenantId, "u2@roles.local", TenantRole.Advisor);
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, rolId, null, null, TestIds.Next()));

        var res = await RunAsync(tenantId, s => s.DeleteAsync(rolId, TestIds.Next()));

        Assert.False(res.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, res.Status);
        Assert.Contains("usuario", res.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoSeBorraUnRol_ConAsignacionVENCIDA()
    {
        // Una asignacion caducada sigue siendo historia: borrar el rol la dejaria huerfana.
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles DeleteVencido");
        var rolId = await NuevoRolAsync(tenantId, "ConHistoria", nivelId);
        var (_, tenantUserId) = await NuevoUsuarioAsync(tenantId, "hist@roles.local", TenantRole.Advisor);
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(
            tenantUserId, rolId, null, DateTimeOffset.UtcNow.AddDays(-1), TestIds.Next()));

        var res = await RunAsync(tenantId, s => s.DeleteAsync(rolId, TestIds.Next()));

        Assert.False(res.IsOk);
        Assert.Equal(RolServiceStatus.Invalid, res.Status);
    }

    [Fact]
    public async Task Delete_RemovesRoleAndPermisos()
    {
        var (tenantId, nivelId) = await NewTenantConNivelesAsync("Roles DeleteOk");
        var rolId = await NuevoRolAsync(tenantId, "Borrable", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("actividades", true, false, false, false)], TestIds.Next()));

        var res = await RunAsync(tenantId, s => s.DeleteAsync(rolId, TestIds.Next()));
        Assert.True(res.IsOk, res.Error);

        await using var ctx = _fixture.CreateContext(tenantId);
        Assert.Equal(0, await ctx.Roles.CountAsync(r => r.Id == rolId));
        Assert.Equal(0, await ctx.RolPermisos.CountAsync(p => p.RolId == rolId));
    }

    // ---- Catalogo derivado del menu ----

    [Fact]
    public async Task ModuleCatalog_DerivesFromReadyMenuItems()
    {
        var tenantId = await NewTenantAsync("Roles Catalogo");

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var view = new MenuView { TenantId = tenantId, Name = "Completo", IsDefault = true, SortOrder = 0 };
            ctx.MenuViews.Add(view);
            var section = new MenuNode
            {
                TenantId = tenantId,
                MenuView = view,
                Kind = MenuNodeKind.Section,
                Name = "Sistema General",
                Route = "gen",
                SortOrder = 0
            };
            ctx.MenuNodes.Add(section);
            // Item Ready -> entra al catalogo.
            ctx.MenuNodes.Add(new MenuNode
            {
                TenantId = tenantId,
                MenuView = view,
                Parent = section,
                Kind = MenuNodeKind.Item,
                Name = "Administracion de usuarios",
                Route = "admin-usuarios",
                State = MenuNodeState.Ready,
                SortOrder = 0
            });
            // Item InDevelopment -> NO entra.
            ctx.MenuNodes.Add(new MenuNode
            {
                TenantId = tenantId,
                MenuView = view,
                Parent = section,
                Kind = MenuNodeKind.Item,
                Name = "Stub",
                Route = "modulo/stub",
                State = MenuNodeState.InDevelopment,
                SortOrder = 1
            });
            await ctx.SaveChangesAsync();
        }

        var catalog = await RunAsync(tenantId, s => s.GetModuleCatalogAsync());
        Assert.Contains(catalog, m => m.Key == "admin-usuarios" && m.Grupo == "Sistema General");
        Assert.DoesNotContain(catalog, m => m.Key == "modulo/stub");
    }

    // ---- Menu filtrado por "Ver" ----

    [Fact]
    public async Task MenuFilter_LimitedRole_ExcludesModulesWithoutView()
    {
        var tenantId = await NewTenantAsync("Menu Filtrado");
        long viewId, nivelId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            nivelId = await RolesTestHelpers.SeedNivelesYObtenerIdAsync(ctx, tenantId);
            viewId = await SeedMenuAsync(ctx, tenantId);
        }

        var (platformUserId, tenantUserId) = await NuevoUsuarioAsync(tenantId, "lim@roles.local", TenantRole.Advisor);

        // Rol: Ver en inventario-items, NADA de la seccion Desarrollo, sin inventario-bodegas.
        var rolId = await NuevoRolAsync(tenantId, "Limitado", nivelId);
        await RunAsync(tenantId, s => s.SavePermisosAsync(rolId,
            [new ModulePermissionDto("inventario-items", true, false, false, false)], TestIds.Next()));
        await RunAsync(tenantId, s => s.AssignRoleToUserAsync(tenantUserId, rolId, null, null, TestIds.Next()));

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));
        Assert.True(eff.Can("inventario-items", PermissionAction.View));
        Assert.False(eff.Can("reglas", PermissionAction.View));

        var menu = await ResolveMenuAsync(tenantId, viewId);
        var filtered = MenuPermissionFilter.Filter(menu, eff);

        Assert.NotNull(filtered);
        // Solo queda la seccion Inventarios con SU unico item visible (inventario-items).
        var section = Assert.Single(filtered!.Roots, n => n.Kind == MenuNodeKind.Section);
        Assert.Equal("inv", section.Route);
        var leaf = Assert.Single(section.Children);
        Assert.Equal("inventario-items", leaf.Route);
        // La seccion Desarrollo (sin ningun item con Ver) desaparece del arbol.
        Assert.DoesNotContain(filtered.Roots, n => n.Route == "dev");
    }

    [Fact]
    public async Task MenuFilter_UsuarioSinRol_NoVeNINGUNModulo()
    {
        // Antes este usuario veia el menu COMPLETO (Unrestricted). Ahora no ve nada.
        var tenantId = await NewTenantAsync("Menu SinRol");
        long viewId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            viewId = await SeedMenuAsync(ctx, tenantId);
        }
        var (platformUserId, _) = await NuevoUsuarioAsync(tenantId, "sr@roles.local", TenantRole.Advisor);

        var eff = await RunAsync(tenantId, s => s.ResolveEffectivePermissionsAsync(platformUserId));
        Assert.True(eff.IsEmpty);

        var menu = await ResolveMenuAsync(tenantId, viewId);
        var filtered = MenuPermissionFilter.Filter(menu, eff);

        Assert.NotNull(filtered);
        Assert.Empty(filtered!.Roots);
    }

    // ---- Helpers ----

    /// <summary>Menu de prueba: seccion Inventarios (2 items) + seccion Desarrollo (1 item).</summary>
    private static async Task<long> SeedMenuAsync(TronoxDbContext ctx, long tenantId)
    {
        var view = new MenuView { TenantId = tenantId, Name = "Completo", IsDefault = true, SortOrder = 0 };
        ctx.MenuViews.Add(view);

        var inv = new MenuNode { TenantId = tenantId, MenuView = view, Kind = MenuNodeKind.Section, Name = "Inventarios", Route = "inv", SortOrder = 0 };
        var dev = new MenuNode { TenantId = tenantId, MenuView = view, Kind = MenuNodeKind.Section, Name = "Desarrollo", Route = "dev", SortOrder = 1 };
        ctx.MenuNodes.AddRange(inv, dev);
        ctx.MenuNodes.Add(new MenuNode { TenantId = tenantId, MenuView = view, Parent = inv, Kind = MenuNodeKind.Item, Name = "Items", Route = "inventario-items", State = MenuNodeState.Ready, SortOrder = 0 });
        ctx.MenuNodes.Add(new MenuNode { TenantId = tenantId, MenuView = view, Parent = inv, Kind = MenuNodeKind.Item, Name = "Bodegas", Route = "inventario-bodegas", State = MenuNodeState.Ready, SortOrder = 1 });
        ctx.MenuNodes.Add(new MenuNode { TenantId = tenantId, MenuView = view, Parent = dev, Kind = MenuNodeKind.Item, Name = "Reglas", Route = "reglas", State = MenuNodeState.Ready, SortOrder = 0 });

        await ctx.SaveChangesAsync();
        return view.Id;
    }

    private async Task<ResolvedMenuDto?> ResolveMenuAsync(long tenantId, long viewId)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var svc = new MenuConfigService(ctx, new TestTenantContext(tenantId));
        return await svc.GetMenuForTenantUserAsync(tenantId, viewId);
    }

    private async Task<long> NewTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    /// <summary>Tenant nuevo + sus 4 niveles sembrados; devuelve el id del nivel Publico.</summary>
    private async Task<(long TenantId, long NivelId)> NewTenantConNivelesAsync(string name)
    {
        var tenantId = await NewTenantAsync(name);
        await using var ctx = _fixture.CreateContext(tenantId);
        var nivelId = await RolesTestHelpers.SeedNivelesYObtenerIdAsync(ctx, tenantId);
        return (tenantId, nivelId);
    }

    private async Task<long> NuevoRolAsync(long tenantId, string nombre, long nivelId)
    {
        var created = await RunAsync(tenantId, s => s.SaveAsync(
            new SaveRolRequest(null, nombre, null, nivelId, RolEstado.Activo), TestIds.Next()));
        Assert.True(created.IsOk, created.Error);
        return created.Value!.Id;
    }

    /// <summary>Rol de SISTEMA creado directamente (el servicio no permite crearlos: los siembra el alta).</summary>
    private async Task<long> NuevoRolSistemaAsync(
        long tenantId, string nombre, long nivelId, string codigoSistema, bool allowRename)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var rol = new Rol
        {
            TenantId = tenantId,
            Name = nombre,
            NivelAccesoMaximoId = nivelId,
            IsSystem = true,
            AllowRename = allowRename,
            CodigoSistema = codigoSistema,
            Estado = RolEstado.Activo
        };
        ctx.Roles.Add(rol);
        await ctx.SaveChangesAsync();
        return rol.Id;
    }

    private async Task<(long PlatformUserId, long TenantUserId)> NuevoUsuarioAsync(
        long tenantId, string email, TenantRole rol)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var pu = new PlatformUser { Email = email, DisplayName = email };
        ctx.PlatformUsers.Add(pu);
        var tu = new TenantUser { TenantId = tenantId, PlatformUser = pu, Email = email, TenantRole = rol };
        ctx.TenantUsers.Add(tu);
        await ctx.SaveChangesAsync();
        return (pu.Id, tu.Id);
    }

    private async Task<T> RunAsync<T>(long tenantId, Func<IRolService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var service = new RolService(ctx, new TestTenantContext(tenantId), new NoOpAuditWriter());
        return await action(service);
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
            AuditActorType actorType = AuditActorType.Human)
        {
            // Los tests no persisten auditoria; el interceptor ya estampa tenant/fechas.
        }

        public void Write(long actorUserId, string actionName, string entityName,
            Tronox.Domain.Common.BaseEntity entity,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human)
        {
            // Idem: la forma diferida se verifica en AuditEntityIdTests con el AuditWriter real.
        }
    }
}

/// <summary>Matriz dual, motor PostgreSQL (contenedor efimero postgres:16-alpine).</summary>
public sealed class RolesTests_Postgres
    : RolesTestsBase, IClassFixture<PostgresTenantIsolationFixture>
{
    public RolesTests_Postgres(PostgresTenantIsolationFixture fixture) : base(fixture) { }
}
