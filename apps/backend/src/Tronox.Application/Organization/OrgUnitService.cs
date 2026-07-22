using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Organization;

/// <summary>
/// Estructura organizacional (RQ01 - RF03/RF04, ADR-003). El aislamiento por tenant lo
/// garantiza el filtro global; toda la logica de arbol es PURA (OrgUnitTree) sobre el mapa de
/// nodos del tenant, de modo que se puede testear sin base de datos y cachear.
///
/// Reglas de negocio implementadas aqui:
/// 1. fondo_id OBLIGATORIO en nodos Dependencia; ignorado (persistido null) en los demas.
/// 2. codigo UNICO ENTRE HERMANOS bajo el mismo padre dentro del tenant, NO global.
/// 3. Nunca hay borrado fisico: se archiva, y archivar exige no tener descendientes activos.
/// 4. Validacion de ciclos FAIL-CLOSED (un arbol ya corrupto se reporta como ciclo).
/// 5. La dependencia de un usuario se DERIVA del arbol; no se almacena en el usuario.
/// 6. Toda alta, modificacion, archivado y reubicacion de Cargo queda en la pista de
///    auditoria, auditando la ENTIDAD (no el id: en las altas el id todavia vale 0).
/// </summary>
public sealed class OrgUnitService : IOrgUnitService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public OrgUnitService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Arbol / consulta ----

    public async Task<IReadOnlyList<OrgUnitNodeDto>> GetTreeAsync(
        bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var units = await LoadUnitsWithMetaAsync(includeArchived, cancellationToken);

        var byParent = units
            .GroupBy(u => u.ParentId)
            .ToDictionary(g => g.Key ?? 0, g => g.OrderBy(u => u.SortOrder).ThenBy(u => u.Name).ToList());

        List<OrgUnitNodeDto> BuildChildren(long? parentId)
        {
            if (!byParent.TryGetValue(parentId ?? 0, out var children))
            {
                return [];
            }
            return children.Select(u => ToNode(u, BuildChildren(u.Id))).ToList();
        }

        // Raices: sin padre O con padre fuera del conjunto visible (ej. padre archivado
        // cuando includeArchived = false): asi ningun nodo visible queda huerfano.
        var visibleIds = units.Select(u => u.Id).ToHashSet();
        return units
            .Where(u => u.ParentId is null || !visibleIds.Contains(u.ParentId.Value))
            .OrderBy(u => u.SortOrder).ThenBy(u => u.Name)
            .Select(u => ToNode(u, BuildChildren(u.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<OrgUnitDto>> ListAsync(
        bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var units = await LoadUnitsWithMetaAsync(includeArchived, cancellationToken);
        var nameById = units.ToDictionary(u => u.Id, u => u.Name);
        return units
            .OrderBy(u => u.SortOrder).ThenBy(u => u.Name)
            .Select(u => ToFlat(
                u, u.ParentId is long pid && nameById.TryGetValue(pid, out var pname) ? pname : null))
            .ToList();
    }

    public async Task<OrgUnitDto?> GetAsync(long unitId, CancellationToken cancellationToken = default)
    {
        var unit = await _db.OrgUnits.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        return unit is null ? null : await ToDtoAsync(unit, cancellationToken);
    }

    public async Task<OrgKpisDto> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        var totalUnits = await _db.OrgUnits.CountAsync(u => !u.IsArchived, cancellationToken);
        var dependencias = await _db.OrgUnits.CountAsync(
            u => !u.IsArchived && u.Classifier == OrgUnitClassifier.Dependencia, cancellationToken);

        // Usuarios asignados: miembros + responsables de nodos activos, sin duplicar.
        var memberUsers = await _db.OrgUnitMembers
            .Where(m => !m.OrgUnit!.IsArchived)
            .Select(m => m.TenantUserId)
            .ToListAsync(cancellationToken);
        var responsibleUsers = await _db.OrgUnits
            .Where(u => !u.IsArchived && u.ResponsibleTenantUserId != null)
            .Select(u => u.ResponsibleTenantUserId!.Value)
            .ToListAsync(cancellationToken);
        var assignedUsers = memberUsers.Concat(responsibleUsers).Distinct().Count();

        return new OrgKpisDto(totalUnits, assignedUsers, dependencias);
    }

    // ---- CRUD ----

    public async Task<OrgResult<OrgUnitDto>> CreateAsync(
        SaveOrgUnitRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return OrgResult<OrgUnitDto>.Invalid("No hay tenant activo.");
        }
        var validation = await ValidateAsync(request, unitId: null, cancellationToken);
        if (validation is not null)
        {
            return validation.To<OrgUnitDto>();
        }

        var unit = new OrgUnit { TenantId = tenantId };
        Apply(unit, request);
        _db.OrgUnits.Add(unit);
        // Forma PREFERENTE de auditoria: la ENTIDAD, no el id (en un alta el id vale 0 hasta
        // que EF lo materializa durante SaveChanges).
        _audit.Write(actorUserId, "orgunit.create", nameof(OrgUnit), unit,
            previousValue: null,
            newValue: Snapshot(unit),
            tenantId: unit.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return OrgResult<OrgUnitDto>.Ok(await ToDtoAsync(unit, cancellationToken));
    }

    public async Task<OrgResult<OrgUnitDto>> UpdateAsync(
        long unitId, SaveOrgUnitRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var unit = await _db.OrgUnits.FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit is null)
        {
            return OrgResult<OrgUnitDto>.NotFound("El nodo del organigrama no existe.");
        }
        var validation = await ValidateAsync(request, unitId, cancellationToken);
        if (validation is not null)
        {
            return validation.To<OrgUnitDto>();
        }

        if (request.ParentId != unit.ParentId)
        {
            var cycle = await ValidateParentMoveAsync(unitId, request.ParentId, cancellationToken);
            if (cycle is not null)
            {
                return cycle.To<OrgUnitDto>();
            }
        }
        if (request.SucesoraId == unitId)
        {
            return OrgResult<OrgUnitDto>.Invalid("Una dependencia no puede ser su propia sucesora.");
        }

        var prev = Snapshot(unit);
        Apply(unit, request);
        _audit.Write(actorUserId, "orgunit.update", nameof(OrgUnit), unit,
            previousValue: prev,
            newValue: Snapshot(unit),
            tenantId: unit.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return OrgResult<OrgUnitDto>.Ok(await ToDtoAsync(unit, cancellationToken));
    }

    public async Task<OrgResult<bool>> SetArchivedAsync(
        long unitId, bool archived, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var unit = await _db.OrgUnits.FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit is null)
        {
            return OrgResult<bool>.NotFound("El nodo del organigrama no existe.");
        }
        // Invariante 8: nunca borrado fisico. Y archivar exige no tener descendientes ACTIVOS.
        if (archived && await _db.OrgUnits.AnyAsync(u => u.ParentId == unitId && !u.IsArchived, cancellationToken))
        {
            return OrgResult<bool>.Invalid("El nodo tiene descendientes activos; archivalos primero.");
        }
        var prev = unit.IsArchived;
        unit.IsArchived = archived;
        _audit.Write(actorUserId, archived ? "orgunit.archivar" : "orgunit.restaurar", nameof(OrgUnit), unit,
            previousValue: new { IsArchived = prev },
            newValue: new { unit.IsArchived },
            tenantId: unit.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return OrgResult<bool>.Ok(true);
    }

    // ---- Resolver de dependencia (ADR-003, Addendum) ----

    public async Task<long?> ResolveDependenciaAsync(long orgUnitId, CancellationToken cancellationToken = default)
        => OrgUnitTree.ResolveDependenciaId(orgUnitId, await LoadNodeRefsAsync(cancellationToken));

    public async Task<long?> ResolveDependenciaForUserAsync(
        long tenantUserId, CancellationToken cancellationToken = default)
    {
        var cargoId = await _db.TenantUsers.AsNoTracking()
            .Where(tu => tu.Id == tenantUserId)
            .Select(tu => tu.CargoOrgUnitId)
            .FirstOrDefaultAsync(cancellationToken);
        // FAIL-CLOSED: sin Cargo anclado no hay area documental; nunca "todo".
        return cargoId is long cargo
            ? OrgUnitTree.ResolveDependenciaId(cargo, await LoadNodeRefsAsync(cancellationToken))
            : null;
    }

    public async Task<OrgResult<MoveCargoResultDto>> MoveCargoAsync(
        long unitId, long? newParentId, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var unit = await _db.OrgUnits.FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit is null)
        {
            return OrgResult<MoveCargoResultDto>.NotFound("El nodo del organigrama no existe.");
        }
        if (unit.Classifier != OrgUnitClassifier.Cargo)
        {
            return OrgResult<MoveCargoResultDto>.Invalid(
                "Esta operacion reubica nodos Cargo; usa la edicion normal para los demas nodos.");
        }

        var nodes = await LoadNodeRefsAsync(cancellationToken);
        var parentClassifier = newParentId is long np && nodes.TryGetValue(np, out var parentNode)
            ? (OrgUnitClassifier?)parentNode.Classifier
            : null;
        if (newParentId is not null && parentClassifier is null)
        {
            return OrgResult<MoveCargoResultDto>.NotFound("El nodo padre no existe.");
        }
        var structureError = OrgStructureRules.ValidateParent(OrgUnitClassifier.Cargo, parentClassifier);
        if (structureError is not null)
        {
            return OrgResult<MoveCargoResultDto>.Invalid(structureError);
        }

        var parentByUnit = nodes.ToDictionary(kv => kv.Key, kv => kv.Value.ParentId);
        if (OrgUnitTree.WouldCreateCycle(unitId, newParentId, parentByUnit))
        {
            return OrgResult<MoveCargoResultDto>.Invalid(
                "El padre seleccionado crearia un ciclo: un nodo no puede ser su propio ancestro.");
        }

        var previousParentId = unit.ParentId;
        var previousDependenciaId = OrgUnitTree.ResolveDependenciaId(unitId, nodes);

        // Cuantos usuarios cambian de visibilidad documental SIN que nadie los edite: los
        // anclados a este Cargo o a cualquier nodo del subarbol que se mueve con el.
        var affected = await CountAffectedUsersAsync(unitId, parentByUnit, cancellationToken);

        // Simular el arbol resultante (puro) para reportar la dependencia destino.
        var moved = nodes[unitId] with { ParentId = newParentId };
        var after = new Dictionary<long, OrgUnitTree.NodeRef>(nodes) { [unitId] = moved };
        var newDependenciaId = OrgUnitTree.ResolveDependenciaId(unitId, after);

        unit.ParentId = newParentId;
        _audit.Write(actorUserId, "orgunit.mover_cargo", nameof(OrgUnit), unit,
            previousValue: new { ParentId = previousParentId, DependenciaId = previousDependenciaId },
            newValue: new { ParentId = newParentId, DependenciaId = newDependenciaId, UsuariosAfectados = affected },
            tenantId: unit.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);

        return OrgResult<MoveCargoResultDto>.Ok(new MoveCargoResultDto(
            unitId, previousParentId, newParentId, previousDependenciaId, newDependenciaId, affected));
    }

    public async Task<int> CountAffectedUsersAsync(long unitId, CancellationToken cancellationToken = default)
    {
        var parentByUnit = await LoadParentMapAsync(cancellationToken);
        return await CountAffectedUsersAsync(unitId, parentByUnit, cancellationToken);
    }

    private async Task<int> CountAffectedUsersAsync(
        long unitId, IReadOnlyDictionary<long, long?> parentByUnit, CancellationToken cancellationToken)
    {
        var subtree = OrgUnitTree.DescendantsAndSelf(unitId, parentByUnit).ToList();
        return await _db.TenantUsers
            .CountAsync(tu => tu.CargoOrgUnitId != null && subtree.Contains(tu.CargoOrgUnitId.Value), cancellationToken);
    }

    // ---- Miembros ----

    public async Task<IReadOnlyList<OrgUnitMemberDto>> ListMembersAsync(
        long unitId, CancellationToken cancellationToken = default)
    {
        return await _db.OrgUnitMembers.AsNoTracking()
            .Where(m => m.OrgUnitId == unitId)
            .Join(_db.TenantUsers, m => m.TenantUserId, tu => tu.Id, (m, tu) => new { m, tu })
            .GroupJoin(_db.PlatformUsers, x => x.tu.PlatformUserId, pu => pu.Id, (x, pus) => new { x.m, x.tu, pus })
            .SelectMany(x => x.pus.DefaultIfEmpty(), (x, pu) => new
            {
                x.m.Id,
                x.m.OrgUnitId,
                x.m.TenantUserId,
                x.tu.Email,
                DisplayName = pu != null ? pu.DisplayName : null,
                x.m.Role,
                x.m.IsResponsible
            })
            .OrderByDescending(x => x.IsResponsible).ThenBy(x => x.Email)
            .Select(x => new OrgUnitMemberDto(x.Id, x.OrgUnitId, x.TenantUserId, x.Email, x.DisplayName, x.Role, x.IsResponsible))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrgResult<OrgUnitMemberDto>> AddMemberAsync(
        long unitId, long tenantUserId, string? role = null, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return OrgResult<OrgUnitMemberDto>.Invalid("No hay tenant activo.");
        }
        var unit = await _db.OrgUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);
        if (unit is null)
        {
            return OrgResult<OrgUnitMemberDto>.NotFound("El nodo del organigrama no existe.");
        }
        // El usuario debe ser miembro del MISMO tenant (el filtro global lo garantiza).
        var tenantUser = await _db.TenantUsers.AsNoTracking()
            .FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken);
        if (tenantUser is null)
        {
            return OrgResult<OrgUnitMemberDto>.NotFound("El usuario no pertenece al tenant.");
        }
        if (await _db.OrgUnitMembers.AnyAsync(
                m => m.OrgUnitId == unitId && m.TenantUserId == tenantUserId, cancellationToken))
        {
            return OrgResult<OrgUnitMemberDto>.Conflict("El usuario ya es miembro del nodo.");
        }
        var trimmedRole = Normalize(role);
        if (trimmedRole is { Length: > 100 })
        {
            return OrgResult<OrgUnitMemberDto>.Invalid("El rol no puede superar 100 caracteres.");
        }

        var member = new OrgUnitMember
        {
            TenantId = tenantId,
            OrgUnitId = unitId,
            TenantUserId = tenantUserId,
            Role = trimmedRole
        };
        _db.OrgUnitMembers.Add(member);
        await _db.SaveChangesAsync(cancellationToken);

        var displayName = await _db.PlatformUsers.AsNoTracking()
            .Where(pu => pu.Id == tenantUser.PlatformUserId)
            .Select(pu => pu.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        return OrgResult<OrgUnitMemberDto>.Ok(new OrgUnitMemberDto(
            member.Id, unitId, tenantUserId, tenantUser.Email, displayName, member.Role, member.IsResponsible));
    }

    public async Task<OrgResult<bool>> RemoveMemberAsync(long memberId, CancellationToken cancellationToken = default)
    {
        var member = await _db.OrgUnitMembers.FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return OrgResult<bool>.NotFound("El miembro no existe.");
        }
        // Si el miembro era el jefe/responsable, tambien limpiar el responsable del nodo.
        if (member.IsResponsible)
        {
            var unit = await _db.OrgUnits.FirstOrDefaultAsync(u => u.Id == member.OrgUnitId, cancellationToken);
            if (unit is not null && unit.ResponsibleTenantUserId == member.TenantUserId)
            {
                unit.ResponsibleTenantUserId = null;
            }
        }
        _db.OrgUnitMembers.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);
        return OrgResult<bool>.Ok(true);
    }

    public async Task<OrgResult<bool>> SetMemberResponsibleAsync(
        long memberId, bool isResponsible, CancellationToken cancellationToken = default)
    {
        var member = await _db.OrgUnitMembers.FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return OrgResult<bool>.NotFound("El miembro no existe.");
        }
        var unit = await _db.OrgUnits.FirstOrDefaultAsync(u => u.Id == member.OrgUnitId, cancellationToken);
        if (unit is null)
        {
            return OrgResult<bool>.NotFound("El nodo del organigrama no existe.");
        }

        if (isResponsible)
        {
            // A lo sumo un jefe/responsable por nodo: desmarcar a los demas.
            var others = await _db.OrgUnitMembers
                .Where(m => m.OrgUnitId == member.OrgUnitId && m.Id != member.Id && m.IsResponsible)
                .ToListAsync(cancellationToken);
            foreach (var o in others)
            {
                o.IsResponsible = false;
            }
            member.IsResponsible = true;
            unit.ResponsibleTenantUserId = member.TenantUserId;
        }
        else
        {
            member.IsResponsible = false;
            if (unit.ResponsibleTenantUserId == member.TenantUserId)
            {
                unit.ResponsibleTenantUserId = null;
            }
        }
        // Una sola SaveChanges = una transaccion (miembros + nodo atomicos).
        await _db.SaveChangesAsync(cancellationToken);
        return OrgResult<bool>.Ok(true);
    }

    // ---- Internos ----

    private sealed record UnitMeta(
        long Id, string Name, OrgUnitClassifier Classifier, long? ParentId, long? ResponsibleTenantUserId,
        string? ResponsibleName, string? Description, int SortOrder, bool IsArchived, int MemberCount,
        long? TenantUserId, string? OccupantName, long? FondoId, string? FondoNombre, string? Codigo,
        DateOnly? VigenteDesde, DateOnly? VigenteHasta, long? SucesoraId,
        string? CodigoCargo, string? CodigoDafp, NivelJerarquico? NivelJerarquico);

    private static OrgUnitNodeDto ToNode(UnitMeta u, IReadOnlyList<OrgUnitNodeDto> children) => new(
        u.Id, u.Name, u.Classifier, u.ParentId, u.ResponsibleTenantUserId, u.ResponsibleName,
        u.Description, u.SortOrder, u.IsArchived, u.MemberCount, children,
        u.TenantUserId, u.OccupantName, u.FondoId, u.Codigo, u.VigenteDesde, u.VigenteHasta,
        u.SucesoraId, u.CodigoCargo, u.CodigoDafp, u.NivelJerarquico);

    private static OrgUnitDto ToFlat(UnitMeta u, string? parentName) => new(
        u.Id, u.Name, u.Classifier, u.ParentId, parentName, u.ResponsibleTenantUserId, u.ResponsibleName,
        u.Description, u.SortOrder, u.IsArchived, u.MemberCount, u.TenantUserId, u.OccupantName,
        u.FondoId, u.FondoNombre, u.Codigo, u.VigenteDesde, u.VigenteHasta, u.SucesoraId,
        u.CodigoCargo, u.CodigoDafp, u.NivelJerarquico);

    private async Task<List<UnitMeta>> LoadUnitsWithMetaAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var query = _db.OrgUnits.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(u => !u.IsArchived);
        }
        var rows = await query
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Classifier,
                u.ParentId,
                u.ResponsibleTenantUserId,
                u.Description,
                u.SortOrder,
                u.IsArchived,
                u.TenantUserId,
                u.FondoId,
                u.Codigo,
                u.VigenteDesde,
                u.VigenteHasta,
                u.SucesoraId,
                u.CodigoCargo,
                u.CodigoDafp,
                u.NivelJerarquico,
                FondoNombre = _db.Fondos.Where(f => f.Id == u.FondoId).Select(f => f.NombreFondo).FirstOrDefault(),
                MemberCount = _db.OrgUnitMembers.Count(m => m.OrgUnitId == u.Id),
                ResponsibleName = _db.TenantUsers
                    .Where(tu => tu.Id == u.ResponsibleTenantUserId)
                    .Join(_db.PlatformUsers, tu => tu.PlatformUserId, pu => pu.Id,
                        (tu, pu) => pu.DisplayName ?? tu.Email)
                    .FirstOrDefault(),
                OccupantName = _db.TenantUsers
                    .Where(tu => tu.Id == u.TenantUserId)
                    .Join(_db.PlatformUsers, tu => tu.PlatformUserId, pu => pu.Id,
                        (tu, pu) => pu.DisplayName ?? tu.Email)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new UnitMeta(
            r.Id, r.Name, r.Classifier, r.ParentId, r.ResponsibleTenantUserId, r.ResponsibleName,
            r.Description, r.SortOrder, r.IsArchived, r.MemberCount, r.TenantUserId, r.OccupantName,
            r.FondoId, r.FondoNombre, r.Codigo, r.VigenteDesde, r.VigenteHasta, r.SucesoraId,
            r.CodigoCargo, r.CodigoDafp, r.NivelJerarquico)).ToList();
    }

    /// <summary>
    /// Mapa Id -&gt; nodo de TODO el arbol del tenant (incluidos los archivados: la cadena de
    /// ancestros de un nodo activo puede pasar por uno archivado, y cortarla ahi daria una
    /// dependencia equivocada). Es la entrada del resolver puro.
    /// </summary>
    private async Task<Dictionary<long, OrgUnitTree.NodeRef>> LoadNodeRefsAsync(CancellationToken cancellationToken)
        => await _db.OrgUnits.AsNoTracking()
            .Select(u => new OrgUnitTree.NodeRef(u.Id, u.ParentId, u.Classifier))
            .ToDictionaryAsync(n => n.Id, cancellationToken);

    private async Task<Dictionary<long, long?>> LoadParentMapAsync(CancellationToken cancellationToken)
        => await _db.OrgUnits.AsNoTracking()
            .Select(u => new { u.Id, u.ParentId })
            .ToDictionaryAsync(u => u.Id, u => u.ParentId, cancellationToken);

    private async Task<OrgUnitDto> ToDtoAsync(OrgUnit unit, CancellationToken cancellationToken)
    {
        var parentName = unit.ParentId is long parentId
            ? await _db.OrgUnits.AsNoTracking().Where(u => u.Id == parentId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var fondoNombre = unit.FondoId is long fondoId
            ? await _db.Fondos.AsNoTracking().Where(f => f.Id == fondoId).Select(f => f.NombreFondo).FirstOrDefaultAsync(cancellationToken)
            : null;
        var responsibleName = await DisplayNameAsync(unit.ResponsibleTenantUserId, cancellationToken);
        var occupantName = await DisplayNameAsync(unit.TenantUserId, cancellationToken);
        var memberCount = await _db.OrgUnitMembers.CountAsync(m => m.OrgUnitId == unit.Id, cancellationToken);

        return new OrgUnitDto(
            unit.Id, unit.Name, unit.Classifier, unit.ParentId, parentName,
            unit.ResponsibleTenantUserId, responsibleName, unit.Description,
            unit.SortOrder, unit.IsArchived, memberCount, unit.TenantUserId, occupantName,
            unit.FondoId, fondoNombre, unit.Codigo, unit.VigenteDesde, unit.VigenteHasta,
            unit.SucesoraId, unit.CodigoCargo, unit.CodigoDafp, unit.NivelJerarquico);
    }

    private async Task<string?> DisplayNameAsync(long? tenantUserId, CancellationToken cancellationToken)
        => tenantUserId is long id
            ? await _db.TenantUsers.AsNoTracking()
                .Where(tu => tu.Id == id)
                .Join(_db.PlatformUsers, tu => tu.PlatformUserId, pu => pu.Id, (tu, pu) => pu.DisplayName ?? tu.Email)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

    /// <summary>
    /// Escribe la request sobre la entidad. Los atributos que NO corresponden al clasificador
    /// se persisten en NULL: un Cargo jamas guarda fondo_id, y una Dependencia jamas guarda
    /// nivel jerarquico.
    /// </summary>
    private static void Apply(OrgUnit unit, SaveOrgUnitRequest request)
    {
        var esDependencia = request.Classifier == OrgUnitClassifier.Dependencia;
        var esCargo = request.Classifier == OrgUnitClassifier.Cargo;

        unit.Name = request.Name.Trim();
        unit.Classifier = request.Classifier;
        unit.ParentId = request.ParentId;
        unit.ResponsibleTenantUserId = request.ResponsibleTenantUserId;
        unit.Description = Normalize(request.Description);
        unit.SortOrder = request.SortOrder;
        unit.TenantUserId = request.Classifier == OrgUnitClassifier.Funcionario ? request.TenantUserId : null;

        unit.FondoId = esDependencia ? request.FondoId : null;
        unit.Codigo = esDependencia ? Normalize(request.Codigo)?.ToUpperInvariant() : null;
        unit.VigenteDesde = esDependencia ? request.VigenteDesde : null;
        unit.VigenteHasta = esDependencia ? request.VigenteHasta : null;
        unit.SucesoraId = esDependencia ? request.SucesoraId : null;

        unit.CodigoCargo = esCargo ? Normalize(request.CodigoCargo) : null;
        unit.CodigoDafp = esCargo ? Normalize(request.CodigoDafp) : null;
        unit.NivelJerarquico = esCargo ? request.NivelJerarquico : null;
    }

    private static object Snapshot(OrgUnit u) => new
    {
        u.Name, u.Classifier, u.ParentId, u.FondoId, u.Codigo, u.VigenteDesde, u.VigenteHasta,
        u.SucesoraId, u.CodigoCargo, u.CodigoDafp, u.NivelJerarquico, u.TenantUserId,
        u.ResponsibleTenantUserId, u.IsArchived
    };

    /// <summary>Validacion completa del alta/edicion. Null = valido.</summary>
    private async Task<OrgResult<bool>?> ValidateAsync(
        SaveOrgUnitRequest request, long? unitId, CancellationToken cancellationToken)
    {
        // 1. Reglas PURAS por clasificador (sin base de datos).
        var error = OrgStructureRules.ValidateNode(
            request.Classifier, request.Name, request.Description, request.FondoId, request.Codigo,
            request.VigenteDesde, request.VigenteHasta, request.CodigoCargo, request.CodigoDafp,
            request.NivelJerarquico, request.TenantUserId);
        if (error is not null)
        {
            return OrgResult<bool>.Invalid(error);
        }

        // 2. Referencias que deben existir DENTRO del tenant (el filtro global lo garantiza).
        if (request.ParentId is long parentId
            && !await _db.OrgUnits.AnyAsync(u => u.Id == parentId, cancellationToken))
        {
            return OrgResult<bool>.NotFound("El nodo padre no existe.");
        }
        if (request.Classifier == OrgUnitClassifier.Dependencia
            && request.FondoId is long fondoId
            && !await _db.Fondos.AnyAsync(f => f.Id == fondoId, cancellationToken))
        {
            return OrgResult<bool>.NotFound("El fondo documental no existe.");
        }
        if (request.ResponsibleTenantUserId is long responsibleId
            && !await _db.TenantUsers.AnyAsync(tu => tu.Id == responsibleId, cancellationToken))
        {
            return OrgResult<bool>.NotFound("El responsable no pertenece al tenant.");
        }
        if (request.Classifier == OrgUnitClassifier.Funcionario
            && request.TenantUserId is long occupantId
            && !await _db.TenantUsers.AnyAsync(tu => tu.Id == occupantId, cancellationToken))
        {
            return OrgResult<bool>.NotFound("El usuario ocupante no pertenece al tenant.");
        }
        if (request.Classifier == OrgUnitClassifier.Dependencia && request.SucesoraId is long sucesoraId)
        {
            var sucesora = await _db.OrgUnits.AsNoTracking()
                .Where(u => u.Id == sucesoraId)
                .Select(u => (OrgUnitClassifier?)u.Classifier)
                .FirstOrDefaultAsync(cancellationToken);
            if (sucesora is null)
            {
                return OrgResult<bool>.NotFound("La dependencia sucesora no existe.");
            }
            if (sucesora != OrgUnitClassifier.Dependencia)
            {
                return OrgResult<bool>.Invalid("La sucesora de una dependencia debe ser otra Dependencia.");
            }
        }

        // 3. Coherencia estructural padre -> hijo.
        var parentClassifier = request.ParentId is long pid
            ? await _db.OrgUnits.AsNoTracking()
                .Where(u => u.Id == pid)
                .Select(u => (OrgUnitClassifier?)u.Classifier)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var structureError = OrgStructureRules.ValidateParent(request.Classifier, parentClassifier);
        if (structureError is not null)
        {
            return OrgResult<bool>.Invalid(structureError);
        }

        // 4. Codigo UNICO ENTRE HERMANOS dentro del tenant (NO global): el mismo codigo puede
        //    repetirse bajo padres distintos.
        if (request.Classifier == OrgUnitClassifier.Dependencia && Normalize(request.Codigo) is string codigo)
        {
            var upper = codigo.ToUpperInvariant();
            var duplicated = await _db.OrgUnits.AsNoTracking().AnyAsync(
                u => u.Codigo == upper && u.ParentId == request.ParentId && (unitId == null || u.Id != unitId),
                cancellationToken);
            if (duplicated)
            {
                return OrgResult<bool>.Conflict(
                    $"Ya existe una dependencia con el codigo '{upper}' bajo el mismo padre.");
            }
        }

        return null;
    }

    private async Task<OrgResult<bool>?> ValidateParentMoveAsync(
        long unitId, long? newParentId, CancellationToken cancellationToken)
    {
        if (newParentId is not long newParent)
        {
            return null;
        }
        if (newParent == unitId)
        {
            return OrgResult<bool>.Invalid("Un nodo no puede ser su propio padre.");
        }
        // Validacion de ciclos FAIL-CLOSED: el padre propuesto no puede ser descendiente del
        // nodo, y un arbol ya corrupto se reporta como ciclo en vez de colgar el listado.
        var parentByUnit = await LoadParentMapAsync(cancellationToken);
        return OrgUnitTree.WouldCreateCycle(unitId, newParent, parentByUnit)
            ? OrgResult<bool>.Invalid(
                "El padre seleccionado crearia un ciclo: un nodo no puede ser su propio ancestro.")
            : null;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
