namespace Tronox.Application.Roles;

/// <summary>
/// Gestion de roles de permisos del tenant activo (RQ01 - RF05). Tenant-scoped (filtro global +
/// estampado en alta), auditado y transaccional en las operaciones multi-tabla.
/// </summary>
public interface IRolService
{
    Task<IReadOnlyList<RolDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<RolDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea (Id null) o edita un rol. Reglas RF05: nombre unico por tenant; un rol de SISTEMA no
    /// se renombra (salvo "Lider de Dependencia") y NUNCA se le cambia el nivel de acceso maximo.
    /// </summary>
    Task<RolResult<RolDto>> SaveAsync(
        SaveRolRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un rol. Bloquea (Invalid) si es de sistema o si tiene usuarios asignados.
    /// </summary>
    Task<RolResult<bool>> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda la matriz del rol. Borra e reinserta EN TRANSACCION, persistiendo UNA FILA POR
    /// (modulo, accion) concedida (la spec prohibe bitmask y JSON).
    /// </summary>
    Task<RolResult<bool>> SavePermisosAsync(
        long rolId, IReadOnlyList<ModulePermissionDto> permisos, long actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Catalogo de modulos de la matriz, DERIVADO de los MenuNode Item con State=Ready de la
    /// vista IsDefault del tenant (Key=Route, Label=Name, Grupo=Section padre). Sin listas
    /// paralelas que se desincronicen.
    /// </summary>
    Task<IReadOnlyList<ModuloInfo>> GetModuleCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Permisos efectivos del usuario: UNION de sus roles VIGENTES y nivel de acceso MAS ALTO.
    /// FAIL-CLOSED: sin roles vigentes devuelve <see cref="EffectivePermissions.None"/>.
    /// platformUserId = PlatformUser.Id (resuelve el TenantUser del tenant activo).
    /// </summary>
    Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
        long platformUserId, CancellationToken cancellationToken = default);

    /// <summary>Asignaciones de rol (con vigencia) de un usuario del tenant.</summary>
    Task<IReadOnlyList<RolAsignacionDto>> GetUserRolesAsync(
        long tenantUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna un rol a un usuario con vigencia opcional. Idempotente sobre (usuario, rol): si ya
    /// existe la asignacion, actualiza su vigencia. Auditado.
    /// </summary>
    Task<RolResult<bool>> AssignRoleToUserAsync(
        long tenantUserId, long rolId, DateTimeOffset? vigenteDesde, DateTimeOffset? vigenteHasta,
        long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Revoca (elimina) la asignacion de un rol a un usuario. Auditado.</summary>
    Task<RolResult<bool>> RevokeRoleFromUserAsync(
        long tenantUserId, long rolId, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplaza el CONJUNTO de roles de un usuario por el indicado (lo que usa la pantalla de
    /// asignacion). Transaccional y auditado.
    /// </summary>
    Task<RolResult<bool>> SetUserRolesAsync(
        long tenantUserId, IReadOnlyList<RolAsignacionDto> roles, long actorUserId,
        CancellationToken cancellationToken = default);
}
