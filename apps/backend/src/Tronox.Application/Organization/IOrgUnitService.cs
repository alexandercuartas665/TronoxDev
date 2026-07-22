namespace Tronox.Application.Organization;

/// <summary>
/// Estructura organizacional del tenant (RQ01 - RF03/RF04, ADR-003): UN SOLO arbol de nodos
/// clasificados (Dependencia / Cargo / Funcionario) con jerarquia ilimitada, validacion de
/// ciclos fail-closed, archivado en vez de borrado, y el resolver de dependencia del Addendum.
/// Todo tenant-scoped por el filtro global.
/// </summary>
public interface IOrgUnitService
{
    // ---- Arbol / consulta ----

    /// <summary>
    /// Arbol completo (raices con hijos anidados, ordenados por SortOrder y nombre). Se tratan
    /// como RAIZ los nodos sin padre O con padre fuera del conjunto visible (ej. padre
    /// archivado), para que ningun nodo visible quede invisible.
    /// </summary>
    Task<IReadOnlyList<OrgUnitNodeDto>> GetTreeAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>Lista plana (para combos de padre / administracion).</summary>
    Task<IReadOnlyList<OrgUnitDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<OrgUnitDto?> GetAsync(long unitId, CancellationToken cancellationToken = default);

    /// <summary>KPIs de cabecera: nodos activos, usuarios asignados y nodos Dependencia.</summary>
    Task<OrgKpisDto> GetKpisAsync(CancellationToken cancellationToken = default);

    // ---- CRUD ----

    Task<OrgResult<OrgUnitDto>> CreateAsync(SaveOrgUnitRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza el nodo. Invalid si el nuevo padre crea un ciclo en el arbol.</summary>
    Task<OrgResult<OrgUnitDto>> UpdateAsync(long unitId, SaveOrgUnitRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archiva/restaura el nodo (soft-delete; NUNCA hay DELETE fisico). Archivar exige no
    /// tener descendientes ACTIVOS.
    /// </summary>
    Task<OrgResult<bool>> SetArchivedAsync(long unitId, bool archived, long actorUserId, string? motivo = null, CancellationToken cancellationToken = default);

    // ---- Resolver de dependencia (ADR-003, Addendum) ----

    /// <summary>
    /// Dependencia derivada de un nodo: sube por la cadena de padres hasta el primer nodo con
    /// clasificador Dependencia. FAIL-CLOSED: null = SIN dependencia (sin visibilidad
    /// documental), nunca visibilidad total. La caminata en si es pura
    /// (<see cref="OrgUnitTree.ResolveDependenciaId"/>); aqui solo se carga el arbol.
    /// </summary>
    Task<long?> ResolveDependenciaAsync(long orgUnitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dependencia derivada de un USUARIO: se resuelve desde su Cargo (TenantUser.CargoOrgUnitId).
    /// FAIL-CLOSED: usuario sin Cargo, o Cargo sin Dependencia por encima, devuelve null.
    /// </summary>
    Task<long?> ResolveDependenciaForUserAsync(long tenantUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reubica un nodo CARGO bajo otro padre. Cambia la visibilidad documental de todos sus
    /// ocupantes sin editarlos, asi que queda en la pista de auditoria e informa cuantos
    /// usuarios quedan afectados (ver <see cref="MoveCargoResultDto"/>).
    /// </summary>
    Task<OrgResult<MoveCargoResultDto>> MoveCargoAsync(long unitId, long? newParentId, long actorUserId, string? motivo = null, CancellationToken cancellationToken = default);

    /// <summary>Cuenta los usuarios que quedarian afectados por mover el nodo (sin moverlo).</summary>
    Task<int> CountAffectedUsersAsync(long unitId, CancellationToken cancellationToken = default);

    // ---- Miembros ----

    Task<IReadOnlyList<OrgUnitMemberDto>> ListMembersAsync(long unitId, CancellationToken cancellationToken = default);
    Task<OrgResult<OrgUnitMemberDto>> AddMemberAsync(long unitId, long tenantUserId, string? role = null, CancellationToken cancellationToken = default);
    Task<OrgResult<bool>> RemoveMemberAsync(long memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca/desmarca a un miembro como jefe/responsable de su nodo (a lo sumo uno por nodo).
    /// Al marcar, sincroniza <c>OrgUnit.ResponsibleTenantUserId</c>; al desmarcar, lo limpia si
    /// apuntaba a ese usuario. Operacion multi-tabla en una sola transaccion.
    /// </summary>
    Task<OrgResult<bool>> SetMemberResponsibleAsync(long memberId, bool isResponsible, CancellationToken cancellationToken = default);
}
