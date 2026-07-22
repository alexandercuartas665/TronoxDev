using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Tronox.Application.Roles;
using Tronox.Domain.Enums;
using Tronox.Web.Auth;

namespace Tronox.Web.Tests;

/// <summary>
/// Tests de <see cref="CurrentPermissions"/>: resuelve el set del usuario actual UNA sola vez por
/// scope (cachea) y es FAIL-CLOSED (invariante 10).
///
/// Este archivo es la red de seguridad del arreglo de seguridad. Si alguien reintroduce el
/// fail-open del backbone (devolver acceso total cuando no hay usuario, no hay tenant o la
/// resolucion revienta), estos tests se ponen en rojo.
///
/// Ademas fija la OTRA trampa heredada: la identidad sale del AuthenticationState, NUNCA del
/// IHttpContextAccessor (en un circuito Blazor no hay HttpContext y los claims salian nulos).
/// </summary>
public class CurrentPermissionsTests
{
    /// <summary>
    /// Contenedor con el IRolService dado y un AuthenticationStateProvider como el de un circuito.
    /// Pasar userId/tenantId en null equivale a "no autenticado" / "sin tenant".
    /// </summary>
    private static (IServiceScopeFactory Scopes, IServiceProvider Services) ProviderWith(
        IRolService rolService, long? userId, long? tenantId)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => rolService);
        services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider(userId, tenantId));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IServiceScopeFactory>(), provider);
    }

    private static CurrentPermissions SutFor(IRolService rolService, long? userId, long? tenantId)
    {
        var (scopes, services) = ProviderWith(rolService, userId, tenantId);
        return new CurrentPermissions(scopes, services);
    }

    private static EffectivePermissions RolConVer(string modulo) =>
        EffectivePermissions.FromPermissions(
            TestIds.Next(), [new ModulePermissionDto(modulo, true, false, false, false)]);

    // ---- FAIL-CLOSED (invariante 10) ----

    [Fact]
    public async Task Usuario_SIN_ROL_NoTienePermisos_NoAccesoTotal()
    {
        // El servicio resuelve "sin roles vigentes" -> None. CurrentPermissions NO puede
        // convertir eso en acceso total, que es justo lo que hacia el backbone.
        var fake = new CountingRolService(EffectivePermissions.None);
        var sut = SutFor(fake, userId: TestIds.Next(), tenantId: TestIds.Next());

        var eff = await sut.GetAsync();

        Assert.True(eff.IsEmpty);
        Assert.Equal(1, fake.Calls);
        Assert.False(await sut.CanViewAsync("expedientes"));
        Assert.False(await sut.CanCreateAsync("expedientes"));
        Assert.False(await sut.CanEditAsync("expedientes"));
        Assert.False(await sut.CanDeleteAsync("expedientes"));
        Assert.False(await sut.CanExportAsync("expedientes"));
        Assert.False(await sut.CanPrintAsync("expedientes"));
    }

    [Fact]
    public async Task ResolucionQueLANZA_NoTienePermisos()
    {
        // Una resolucion de permisos que revienta es justo el momento en el que NO se puede
        // asumir buena fe. Antes esto devolvia Unrestricted "para no bloquear la consola".
        var sut = SutFor(new ThrowingRolService(), userId: TestIds.Next(), tenantId: TestIds.Next());

        var eff = await sut.GetAsync();

        Assert.True(eff.IsEmpty);
        Assert.False(await sut.CanViewAsync("expedientes"));
        Assert.False(await sut.CanAsync("expedientes", PermissionAction.Export));
    }

    [Fact]
    public async Task SinUsuarioAutenticado_NoTienePermisos()
    {
        var fake = new CountingRolService(RolConVer("expedientes"));
        var sut = SutFor(fake, userId: null, tenantId: TestIds.Next());

        var eff = await sut.GetAsync();

        Assert.True(eff.IsEmpty);
        Assert.Equal(0, fake.Calls);   // ni siquiera consulta: no hay a quien resolver.
        Assert.False(await sut.CanViewAsync("expedientes"));
    }

    [Fact]
    public async Task SinClaimDeTenant_NoTienePermisos()
    {
        // Sin tenant no hay matriz de modulos que aplicar. Resolver igual seria peor: el filtro
        // global de EF no acotaria a ningun tenant.
        var fake = new CountingRolService(RolConVer("expedientes"));
        var sut = SutFor(fake, userId: TestIds.Next(), tenantId: null);

        var eff = await sut.GetAsync();

        Assert.True(eff.IsEmpty);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task SinAuthenticationStateProvider_NoTienePermisos()
    {
        // Scope sin proveedor de identidad (p. ej. trabajo de fondo): sin identidad, sin permisos.
        var services = new ServiceCollection();
        services.AddScoped<IRolService>(_ => new CountingRolService(RolConVer("expedientes")));
        var provider = services.BuildServiceProvider();
        var sut = new CurrentPermissions(provider.GetRequiredService<IServiceScopeFactory>(), provider);

        Assert.True((await sut.GetAsync()).IsEmpty);
    }

    // ---- Identidad desde el AuthenticationState (trampa del circuito Blazor) ----

    [Fact]
    public async Task Identidad_SaleDelAuthenticationState_NoDelHttpContext()
    {
        // Regresion del bug de seguridad: en un circuito Blazor NO hay HttpContext. Cuando la
        // identidad se leia de ahi, salia nula, la resolucion caia en su rama de fallo y -siendo
        // fail-open- el gateado en pagina no restringia a NADIE. Aqui no hay HttpContext en
        // absoluto (CurrentPermissions ya ni lo recibe) y aun asi debe resolver de verdad.
        var fake = new CountingRolService(RolConVer("inventario-items"));
        var sut = SutFor(fake, userId: TestIds.Next(), tenantId: TestIds.Next());

        var eff = await sut.GetAsync();

        Assert.False(eff.IsEmpty);                 // lo que fallaba: resolvia a "sin identidad"
        Assert.Equal(1, fake.Calls);               // resolvio de verdad contra el rol
        Assert.True(await sut.CanViewAsync("inventario-items"));
        Assert.False(await sut.CanCreateAsync("inventario-items"));
    }

    // ---- Memoizacion y respeto del set resuelto ----

    [Fact]
    public async Task Resolve_CacheaElResultado_ResuelveUnaSolaVez()
    {
        var fake = new CountingRolService(RolConVer("inventario-items"));
        var sut = SutFor(fake, userId: TestIds.Next(), tenantId: TestIds.Next());

        var a = await sut.GetAsync();
        var b = await sut.GetAsync();
        var canView = await sut.CanViewAsync("inventario-items");
        var canCreate = await sut.CanCreateAsync("inventario-items");

        Assert.Same(a, b);
        Assert.Equal(1, fake.Calls);
        Assert.True(canView);
        Assert.False(canCreate);
    }

    [Fact]
    public async Task ModuloFueraDeLaMatriz_NoSeConcede()
    {
        var sut = SutFor(new CountingRolService(RolConVer("inventario-items")),
            userId: TestIds.Next(), tenantId: TestIds.Next());

        Assert.True(await sut.CanViewAsync("inventario-items"));
        Assert.False(await sut.CanViewAsync("expedientes"));
    }

    [Fact]
    public async Task LasSeisAcciones_SeConsultanIndependientemente()
    {
        var eff = EffectivePermissions.FromPermissions(
            TestIds.Next(),
            [new ModulePermissionDto("expedientes", true, false, false, false, true, false)]);
        var sut = SutFor(new CountingRolService(eff), userId: TestIds.Next(), tenantId: TestIds.Next());

        Assert.True(await sut.CanViewAsync("expedientes"));
        Assert.True(await sut.CanExportAsync("expedientes"));
        Assert.False(await sut.CanPrintAsync("expedientes"));
        Assert.False(await sut.CanCreateAsync("expedientes"));
        Assert.False(await sut.CanEditAsync("expedientes"));
        Assert.False(await sut.CanDeleteAsync("expedientes"));
    }

    /// <summary>AuthenticationStateProvider con usuario/tenant fijos, como el de un circuito.</summary>
    private sealed class FakeAuthStateProvider(long? userId, long? tenantId) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var claims = new List<Claim>();
            if (userId is long uid) { claims.Add(new Claim(ClaimTypes.NameIdentifier, uid.ToString())); }
            if (tenantId is long tid) { claims.Add(new Claim("tenant_id", tid.ToString())); }
            var identity = claims.Count > 0 ? new ClaimsIdentity(claims, "circuit") : new ClaimsIdentity();
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    // ---- Fakes de IRolService (solo se ejercita ResolveEffectivePermissionsAsync) ----

    private sealed class CountingRolService(EffectivePermissions eff) : StubRolService
    {
        public int Calls { get; private set; }

        public override Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
            long platformUserId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(eff);
        }
    }

    private sealed class ThrowingRolService : StubRolService
    {
        public override Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
            long platformUserId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    /// <summary>Base con todos los miembros de IRolService lanzando NotSupported salvo el resolutor.</summary>
    private abstract class StubRolService : IRolService
    {
        public virtual Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
            long platformUserId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<RolDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolResult<RolDto>> SaveAsync(SaveRolRequest request, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolResult<bool>> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolResult<bool>> SavePermisosAsync(long rolId, IReadOnlyList<ModulePermissionDto> permisos, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModuloInfo>> GetModuleCatalogAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RolAsignacionDto>> GetUserRolesAsync(long tenantUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolResult<bool>> AssignRoleToUserAsync(long tenantUserId, long rolId, DateTimeOffset? vigenteDesde, DateTimeOffset? vigenteHasta, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolResult<bool>> RevokeRoleFromUserAsync(long tenantUserId, long rolId, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RolResult<bool>> SetUserRolesAsync(long tenantUserId, IReadOnlyList<RolAsignacionDto> roles, long actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
