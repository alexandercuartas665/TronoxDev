using Tronox.Application.Archivistica;
using Tronox.Application.MenuConfig;

namespace Tronox.Integration.Tests;

/// <summary>
/// Doble no-op de <see cref="IMenuProvisioningService"/> para los tests que construyen
/// <c>TenantAdminService</c> a mano y NO ejercitan el aprovisionamiento del menu (perfil de
/// empresa, cambio de estado, listado de usuarios).
///
/// OJO: no usar en un test que verifique que un tenant NUEVO nace con su vista de menu; ahi hay
/// que inyectar la implementacion real (<c>DatabaseSeeder</c>), o el test pasaria en falso.
/// </summary>
public sealed class NoOpMenuProvisioning : IMenuProvisioningService
{
    public Task EnsureDefaultMenuAsync(long tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Doble no-op de <see cref="IClasificacionProvisioningService"/>, mismo criterio que
/// <see cref="NoOpMenuProvisioning"/>: para tests que construyen los servicios de alta a mano y
/// NO ejercitan la siembra de los niveles de clasificacion.
///
/// OJO: no usar en un test que verifique que un tenant NUEVO nace con sus 4 niveles; ahi hay que
/// inyectar la implementacion real (<c>ClasificacionProvisioningService</c>) o el test pasaria
/// en falso. Ver ConfiguracionArchivisticaTests.
/// </summary>
public sealed class NoOpClasificacionProvisioning : IClasificacionProvisioningService
{
    public Task EnsureNivelesClasificacionAsync(long tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
