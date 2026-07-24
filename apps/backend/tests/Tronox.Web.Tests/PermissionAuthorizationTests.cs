using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Tronox.Application.Roles;
using Tronox.Domain.Enums;
using Tronox.Web.Auth;

namespace Tronox.Web.Tests;

/// <summary>
/// Tests unitarios del enforcement dinamico de permisos: parseo de nombres de policy
/// "Perm:{modulo}:{accion}" (simples y COMPUESTAS con '+'), y decision del
/// PermissionAuthorizationHandler.
///
/// FAIL-CLOSED (invariante 10): el handler concede UNICAMENTE si la matriz lo dice. Ya no existe
/// la rama "si es Unrestricted, concede", que era por donde pasaban Owner/Admin y los usuarios
/// sin rol para obtener acceso total.
/// </summary>
public class PermissionAuthorizationTests
{
    // ---- PermissionPolicy.TryParse ----

    [Theory]
    [InlineData("Perm:inventario-items:View", "inventario-items", PermissionAction.View)]
    [InlineData("Perm:admin-usuarios:Create", "admin-usuarios", PermissionAction.Create)]
    [InlineData("Perm:roles-permisos:Edit", "roles-permisos", PermissionAction.Edit)]
    [InlineData("Perm:modulo/estados:Delete", "modulo/estados", PermissionAction.Delete)]
    // Las dos acciones nuevas de la spec (6 acciones, no 4).
    [InlineData("Perm:expedientes:Export", "expedientes", PermissionAction.Export)]
    [InlineData("Perm:expedientes:Print", "expedientes", PermissionAction.Print)]
    public void TryParse_ValidNames_Parses(string name, string expectedModule, PermissionAction expectedAction)
    {
        Assert.True(PermissionPolicy.TryParse(name, out var module, out var action));
        Assert.Equal(expectedModule, module);
        Assert.Equal(expectedAction, action);
    }

    [Theory]
    [InlineData("Inventario.Ver")]          // policy clasica, sin prefijo Perm:
    [InlineData("TenantMember")]
    [InlineData("Perm:solo-modulo")]        // falta la accion
    [InlineData("Perm:mod:Desconocida")]    // accion no valida
    [InlineData("Perm::View")]              // modulo vacio
    [InlineData("")]
    public void TryParse_InvalidNames_ReturnsFalse(string name)
    {
        Assert.False(PermissionPolicy.TryParse(name, out _, out _));
    }

    [Fact]
    public void For_BuildsRoundTrippableName()
    {
        var name = PermissionPolicy.For("inventario-items", PermissionAction.Create);
        Assert.Equal("Perm:inventario-items:Create", name);
        Assert.True(PermissionPolicy.TryParse(name, out var module, out var action));
        Assert.Equal("inventario-items", module);
        Assert.Equal(PermissionAction.Create, action);
    }

    // ---- La accion se toma tras el ULTIMO ':' (una ruta puede contener ':') ----

    [Fact]
    public void TryParse_RutaConDosPuntos_LaAccionEsElSegmentoTrasElULTIMO()
    {
        // El detalle que evita el bug: partir por el PRIMER ':' daria modulo="modulo" y
        // accion="sub:View", que no parsea, y la policy se caeria al provider por defecto...
        // que no la conoce. Resultado: una pagina sin gate efectivo.
        Assert.True(PermissionPolicy.TryParse("Perm:modulo:sub:View", out var module, out var action));
        Assert.Equal("modulo:sub", module);
        Assert.Equal(PermissionAction.View, action);
    }

    [Fact]
    public void TryParseMany_CompuestaConRutaQueContieneDosPuntos_ParseaCadaSegmento()
    {
        // Policy COMPUESTA (AND) donde AMBOS modulos llevan ':' en la ruta.
        var name = "Perm:modulo:sub:View+otro:ruta:Export";

        Assert.True(PermissionPolicy.TryParseMany(name, out var parts));

        Assert.Equal(2, parts.Count);
        Assert.Equal(("modulo:sub", PermissionAction.View), parts[0]);
        Assert.Equal(("otro:ruta", PermissionAction.Export), parts[1]);
    }

    [Fact]
    public void ForAll_ConRutasQueContienenDosPuntos_HaceRoundTrip()
    {
        var name = PermissionPolicy.ForAll(
            ("modulo:sub", PermissionAction.View),
            ("modulo:sub", PermissionAction.Print));

        Assert.Equal("Perm:modulo:sub:View+modulo:sub:Print", name);
        Assert.True(PermissionPolicy.TryParseMany(name, out var parts));
        Assert.Equal(("modulo:sub", PermissionAction.View), parts[0]);
        Assert.Equal(("modulo:sub", PermissionAction.Print), parts[1]);
    }

    // ---- Policies COMPUESTAS (AND) ----

    [Fact]
    public void TryParseMany_SingleSegment_ReturnsOne()
    {
        Assert.True(PermissionPolicy.TryParseMany("Perm:actividades:View", out var parts));
        Assert.Single(parts);
        Assert.Equal(("actividades", PermissionAction.View), parts[0]);
    }

    [Fact]
    public void TryParseMany_CompositeAnd_ReturnsAllSegmentsInOrder()
    {
        Assert.True(PermissionPolicy.TryParseMany("Perm:formularios:View+formularios:Edit", out var parts));
        Assert.Equal(2, parts.Count);
        Assert.Equal(("formularios", PermissionAction.View), parts[0]);
        Assert.Equal(("formularios", PermissionAction.Edit), parts[1]);
    }

    [Theory]
    [InlineData("Perm:actividades:View+")]        // segmento vacio al final -> se ignora, queda 1 valido
    [InlineData("Perm:actividades:View+ +")]      // espacios/vacios -> se ignoran
    public void TryParseMany_TrailingSeparators_AreIgnored(string name)
    {
        Assert.True(PermissionPolicy.TryParseMany(name, out var parts));
        Assert.Single(parts);
        Assert.Equal(("actividades", PermissionAction.View), parts[0]);
    }

    [Theory]
    [InlineData("Perm:a:View+b:Desconocida")]     // un segmento con accion invalida -> todo falla
    [InlineData("Perm:a:View+:Edit")]             // un segmento con modulo vacio -> todo falla
    [InlineData("TenantMember")]                  // sin prefijo
    public void TryParseMany_AnyInvalidSegment_ReturnsFalse(string name)
    {
        Assert.False(PermissionPolicy.TryParseMany(name, out _));
    }

    [Fact]
    public void TryParse_Single_RejectsComposite()
    {
        Assert.False(PermissionPolicy.TryParse("Perm:a:View+b:Edit", out _, out _));
    }

    [Fact]
    public void ForAll_BuildsRoundTrippableCompositeName()
    {
        var name = PermissionPolicy.ForAll(("formularios", PermissionAction.View), ("formularios", PermissionAction.Edit));
        Assert.Equal("Perm:formularios:View+formularios:Edit", name);
        Assert.True(PermissionPolicy.TryParseMany(name, out var parts));
        Assert.Equal(2, parts.Count);
    }

    [Fact]
    public async Task Composite_AllRequirementsMet_Succeeds()
    {
        var eff = EffectivePermissions.FromPermissions(TestIds.Next(),
            [new ModulePermissionDto("formularios", true, false, true, false)]);
        var handler = new PermissionAuthorizationHandler(new FakeCurrentPermissions(eff));
        var reqs = new[]
        {
            new PermissionRequirement("formularios", PermissionAction.View),
            new PermissionRequirement("formularios", PermissionAction.Edit)
        };
        var ctx = new AuthorizationHandlerContext(reqs, UserWithTenant(), resource: null);

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Composite_OneRequirementMissing_Denies()
    {
        var eff = EffectivePermissions.FromPermissions(TestIds.Next(),
            [new ModulePermissionDto("formularios", true, false, false, false)]);
        var handler = new PermissionAuthorizationHandler(new FakeCurrentPermissions(eff));
        var reqs = new[]
        {
            new PermissionRequirement("formularios", PermissionAction.View),
            new PermissionRequirement("formularios", PermissionAction.Edit)
        };
        var ctx = new AuthorizationHandlerContext(reqs, UserWithTenant(), resource: null);

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    private static ClaimsPrincipal UserWithTenant()
        => new(new ClaimsIdentity([new Claim("tenant_id", TestIds.Next().ToString())], "test"));

    // ---- PermissionAuthorizationHandler: FAIL-CLOSED ----

    private static AuthorizationHandlerContext ContextFor(PermissionRequirement requirement)
        => new([requirement], UserWithTenant(), resource: null);

    [Fact]
    public async Task Handler_SinPermisos_NoConcede()
    {
        // El caso que antes concedia por la puerta "Unrestricted": usuario sin roles vigentes.
        var handler = new PermissionAuthorizationHandler(
            new FakeCurrentPermissions(EffectivePermissions.None));
        var ctx = ContextFor(new PermissionRequirement("inventario-items", PermissionAction.Delete));

        await handler.HandleAsync(ctx);

        Assert.False(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Handler_SinPermisos_NoConcedeNingunaDeLasSeisAcciones()
    {
        var handler = new PermissionAuthorizationHandler(
            new FakeCurrentPermissions(EffectivePermissions.None));

        foreach (var accion in PermissionActions.All)
        {
            var ctx = ContextFor(new PermissionRequirement("cualquier-modulo", accion));
            await handler.HandleAsync(ctx);
            Assert.False(ctx.HasSucceeded);
        }
    }

    [Fact]
    public async Task Handler_ConRol_Concede_CuandoLaMatrizLoPermite()
    {
        var eff = EffectivePermissions.FromPermissions(TestIds.Next(),
            [new ModulePermissionDto("inventario-items", true, false, false, false)]);
        var handler = new PermissionAuthorizationHandler(new FakeCurrentPermissions(eff));
        var ctx = ContextFor(new PermissionRequirement("inventario-items", PermissionAction.View));

        await handler.HandleAsync(ctx);

        Assert.True(ctx.HasSucceeded);
    }

    [Fact]
    public async Task Handler_ConRol_Niega_CuandoLaMatrizNoLoPermite()
    {
        var eff = EffectivePermissions.FromPermissions(TestIds.Next(),
            [new ModulePermissionDto("inventario-items", true, false, false, false)]);
        var handler = new PermissionAuthorizationHandler(new FakeCurrentPermissions(eff));

        var denyCreate = ContextFor(new PermissionRequirement("inventario-items", PermissionAction.Create));
        var denyOther = ContextFor(new PermissionRequirement("modulo-desconocido", PermissionAction.View));

        await handler.HandleAsync(denyCreate);
        await handler.HandleAsync(denyOther);

        Assert.False(denyCreate.HasSucceeded);
        Assert.False(denyOther.HasSucceeded);
    }

    private sealed class FakeCurrentPermissions(EffectivePermissions eff) : ICurrentPermissions
    {
        public Task<EffectivePermissions> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(eff);
        public Task<EffectivePermissions> GetForAsync(System.Security.Claims.ClaimsPrincipal? user, CancellationToken cancellationToken = default) => Task.FromResult(eff);
        public Task<bool> CanAsync(string moduleKey, PermissionAction action, CancellationToken cancellationToken = default) => Task.FromResult(eff.Can(moduleKey, action));
        public Task<bool> CanViewAsync(string moduleKey, CancellationToken cancellationToken = default) => CanAsync(moduleKey, PermissionAction.View, cancellationToken);
        public Task<bool> CanCreateAsync(string moduleKey, CancellationToken cancellationToken = default) => CanAsync(moduleKey, PermissionAction.Create, cancellationToken);
        public Task<bool> CanEditAsync(string moduleKey, CancellationToken cancellationToken = default) => CanAsync(moduleKey, PermissionAction.Edit, cancellationToken);
        public Task<bool> CanDeleteAsync(string moduleKey, CancellationToken cancellationToken = default) => CanAsync(moduleKey, PermissionAction.Delete, cancellationToken);
        public Task<bool> CanExportAsync(string moduleKey, CancellationToken cancellationToken = default) => CanAsync(moduleKey, PermissionAction.Export, cancellationToken);
        public Task<bool> CanPrintAsync(string moduleKey, CancellationToken cancellationToken = default) => CanAsync(moduleKey, PermissionAction.Print, cancellationToken);
    }
}
