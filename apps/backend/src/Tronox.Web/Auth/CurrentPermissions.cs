using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Tronox.Application.Roles;
using Tronox.Domain.Enums;

namespace Tronox.Web.Auth;

/// <summary>
/// Permisos efectivos del usuario ACTUAL, resueltos una sola vez por scope/circuito y cacheados.
/// Los consumen las paginas (para ocultar botones), el filtrado del menu y el handler de
/// autorizacion.
///
/// FAIL-CLOSED (invariante 10). No hay ninguna via por la que este servicio conceda acceso que la
/// matriz del usuario no conceda:
/// <list type="bullet">
/// <item>Sin usuario autenticado -> SIN PERMISOS.</item>
/// <item>Sin claim de tenant -> SIN PERMISOS (no es un usuario de tenant; no tiene modulos).</item>
/// <item>Usuario sin ningun rol vigente -> SIN PERMISOS. NO acceso total.</item>
/// <item>La resolucion falla o lanza -> SIN PERMISOS. Nada de "por si acaso, dejamos pasar".</item>
/// </list>
/// El backbone (ECOREX) era fail-OPEN a proposito y resolvia "Unrestricted" en todos esos casos.
/// TRONOX no puede serlo: maneja niveles Reservado y Clasificado, y ahi un fallo de resolucion que
/// abre la puerta no es un bug de usabilidad, es una fuga de informacion reservada.
/// </summary>
public interface ICurrentPermissions
{
    /// <summary>Permisos efectivos del usuario actual (resueltos y cacheados en el scope).</summary>
    Task<EffectivePermissions> GetAsync(CancellationToken cancellationToken = default);

    Task<bool> CanAsync(string moduleKey, PermissionAction action, CancellationToken cancellationToken = default);
    Task<bool> CanViewAsync(string moduleKey, CancellationToken cancellationToken = default);
    Task<bool> CanCreateAsync(string moduleKey, CancellationToken cancellationToken = default);
    Task<bool> CanEditAsync(string moduleKey, CancellationToken cancellationToken = default);
    Task<bool> CanDeleteAsync(string moduleKey, CancellationToken cancellationToken = default);
    Task<bool> CanExportAsync(string moduleKey, CancellationToken cancellationToken = default);
    Task<bool> CanPrintAsync(string moduleKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementacion scoped de <see cref="ICurrentPermissions"/>. Resuelve en un scope PROPIO (como
/// NavMenu con la marca y el menu) para no compartir el DbContext del circuito, y memoiza el
/// resultado para todas las consultas del mismo scope.
/// </summary>
public sealed class CurrentPermissions : ICurrentPermissions
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private EffectivePermissions? _cached;

    public CurrentPermissions(IServiceScopeFactory scopeFactory, IServiceProvider services)
    {
        _scopeFactory = scopeFactory;
        _services = services;
    }

    public async Task<EffectivePermissions> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _cached ??= await ResolveAsync(cancellationToken);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<EffectivePermissions> ResolveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (tenantId, platformUserId) = await ResolveIdentityAsync();

            // Sin usuario, o sin tenant, no hay matriz que resolver: SIN PERMISOS.
            // (Aqui el backbone devolvia acceso total. Ese era el agujero.)
            if (platformUserId is not long userId || tenantId is not long tid)
            {
                return EffectivePermissions.None;
            }

            // Scope propio: DbContext aislado del circuito Blazor (patron de NavMenu).
            await using var scope = _scopeFactory.CreateAsyncScope();

            // El TenantUser es tenant-scoped: sin tenant en el contexto el filtro global no lo
            // encuentra. En una peticion HTTP el tenant saldria del HttpContext, pero en un
            // circuito Blazor no hay HttpContext, asi que se fija de forma ambiental a partir del
            // claim del AuthenticationState (unica fuente de identidad aqui, ver abajo).
            using var ambient = AmbientTenantContext.Begin(tid, userId);

            var roles = scope.ServiceProvider.GetRequiredService<IRolService>();
            return await roles.ResolveEffectivePermissionsAsync(userId, cancellationToken);
        }
        catch
        {
            // FAIL-CLOSED: si la resolucion falla, NO se concede nada. Una resolucion de permisos
            // que revienta es justo el momento en el que no se puede asumir buena fe.
            return EffectivePermissions.None;
        }
    }

    /// <summary>
    /// Tenant y usuario actuales, SIEMPRE desde el <see cref="AuthenticationState"/>.
    ///
    /// OJO - trampa heredada del backbone, no reintroducir: aqui NO se lee el
    /// IHttpContextAccessor. En un circuito Blazor interactivo (paginas con prerender:false) NO
    /// hay HttpContext: los claims salian nulos, la resolucion caia en su rama de fallo y -cuando
    /// esa rama era fail-open- el gateado en pagina (ocultar botones, negar acceso) no restringia
    /// a NADIE, aunque su rol lo prohibiera. Solo se salvaban las paginas con
    /// [Authorize(Policy="Perm:...")], que se evalua durante la peticion.
    ///
    /// El AuthenticationState es la fuente correcta en las DOS realidades: en el circuito lo
    /// mantiene el propio Blazor, y en el render del servidor lo alimenta el framework a partir
    /// del usuario autenticado de la peticion. Una sola fuente, sin ramas que diverjan.
    ///
    /// El proveedor se pide por IServiceProvider (no por constructor) para no exigirlo en scopes
    /// que no lo tengan (p. ej. trabajo de fondo); si no esta, no hay identidad -> sin permisos.
    /// </summary>
    private async Task<(long? TenantId, long? UserId)> ResolveIdentityAsync()
    {
        var authProvider = _services.GetService<AuthenticationStateProvider>();
        if (authProvider is null)
        {
            return (null, null);
        }

        var state = await authProvider.GetAuthenticationStateAsync();
        var user = state.User;
        if (!long.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return (null, null);
        }

        return long.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId) && tenantId != 0
            ? (tenantId, userId)
            : (null, userId);
    }

    public async Task<bool> CanAsync(
        string moduleKey, PermissionAction action, CancellationToken cancellationToken = default)
        => (await GetAsync(cancellationToken)).Can(moduleKey, action);

    public Task<bool> CanViewAsync(string moduleKey, CancellationToken cancellationToken = default)
        => CanAsync(moduleKey, PermissionAction.View, cancellationToken);

    public Task<bool> CanCreateAsync(string moduleKey, CancellationToken cancellationToken = default)
        => CanAsync(moduleKey, PermissionAction.Create, cancellationToken);

    public Task<bool> CanEditAsync(string moduleKey, CancellationToken cancellationToken = default)
        => CanAsync(moduleKey, PermissionAction.Edit, cancellationToken);

    public Task<bool> CanDeleteAsync(string moduleKey, CancellationToken cancellationToken = default)
        => CanAsync(moduleKey, PermissionAction.Delete, cancellationToken);

    public Task<bool> CanExportAsync(string moduleKey, CancellationToken cancellationToken = default)
        => CanAsync(moduleKey, PermissionAction.Export, cancellationToken);

    public Task<bool> CanPrintAsync(string moduleKey, CancellationToken cancellationToken = default)
        => CanAsync(moduleKey, PermissionAction.Print, cancellationToken);
}
