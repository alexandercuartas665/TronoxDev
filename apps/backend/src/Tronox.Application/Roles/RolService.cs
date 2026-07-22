using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Roles;

/// <summary>
/// Implementacion de IRolService (RQ01 - RF05). Aislamiento por tenant via filtro global.
/// Auditoria en cada mutacion, SIEMPRE con la sobrecarga que recibe la ENTIDAD (la de long? id
/// pierde el id en las altas, porque el Id es identity y vale 0 antes de SaveChanges).
/// La matriz se guarda borrando e reinsertando en transaccion, UNA FILA POR (modulo, accion).
/// </summary>
public sealed class RolService : IRolService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;
    private readonly TimeProvider _clock;

    public RolService(
        IApplicationDbContext db, ITenantContext tenant, IAuditWriter audit, TimeProvider? clock = null)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTimeOffset Now => _clock.GetUtcNow();

    // ---- Consulta ----

    public async Task<IReadOnlyList<RolDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ahora = Now;
        return await _db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new RolDto(
                r.Id, r.Name, r.Description, r.Estado, r.IsSystem, r.AllowRename, r.CodigoSistema,
                r.NivelAccesoMaximoId,
                r.NivelAccesoMaximo != null ? r.NivelAccesoMaximo.Nombre : null,
                r.NivelAccesoMaximo != null ? r.NivelAccesoMaximo.NivelOrden : 0,
                // Solo cuentan las asignaciones VIGENTES: una caducada no "ocupa" el rol.
                _db.UsuariosRoles.Count(ur => ur.RolId == r.Id
                    && (ur.VigenteDesde == null || ur.VigenteDesde <= ahora)
                    && (ur.VigenteHasta == null || ahora < ur.VigenteHasta))))
            .ToListAsync(cancellationToken);
    }

    public async Task<RolDetailDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null) { return null; }

        var permisos = await LoadPermisosAsync([id], cancellationToken);
        permisos.TryGetValue(id, out var matriz);

        return new RolDetailDto(
            r.Id, r.Name, r.Description, r.Estado, r.IsSystem, r.AllowRename, r.CodigoSistema,
            r.NivelAccesoMaximoId, matriz ?? []);
    }

    /// <summary>
    /// Lee las filas (modulo, accion) de varios roles y las reagrupa en la proyeccion por modulo
    /// que consume la UI. Una sola consulta para todos los roles pedidos.
    /// </summary>
    private async Task<Dictionary<long, IReadOnlyList<ModulePermissionDto>>> LoadPermisosAsync(
        IReadOnlyCollection<long> rolIds, CancellationToken cancellationToken)
    {
        if (rolIds.Count == 0) { return []; }

        var filas = await _db.RolPermisos.AsNoTracking()
            .Where(p => rolIds.Contains(p.RolId) && p.Permitido)
            .Select(p => new { p.RolId, p.Modulo, p.Accion })
            .ToListAsync(cancellationToken);

        return filas
            .GroupBy(f => f.RolId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ModulePermissionDto>)g
                    .GroupBy(f => f.Modulo, StringComparer.Ordinal)
                    .Select(m => ModulePermissionDto.FromActions(m.Key, m.Select(x => x.Accion)))
                    .ToList());
    }

    // ---- Alta / edicion ----

    public async Task<RolResult<RolDto>> SaveAsync(
        SaveRolRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var trimmed = (request.Name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return RolResult<RolDto>.Invalid("El nombre del rol es obligatorio.");
        }
        if (trimmed.Length > 100)
        {
            return RolResult<RolDto>.Invalid("El nombre no puede superar 100 caracteres.");
        }
        var descripcion = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (descripcion is { Length: > 300 })
        {
            return RolResult<RolDto>.Invalid("La descripcion no puede superar 300 caracteres.");
        }

        // nivel_acceso_maximo es OBLIGATORIO y debe existir en el tenant: sin el, el rol no sabe
        // hasta que nivel documental alcanza y no se puede evaluar el acceso a lo Reservado.
        var nivel = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.NivelAccesoMaximoId, cancellationToken);
        if (nivel is null)
        {
            return RolResult<RolDto>.Invalid("El nivel de acceso maximo es obligatorio y debe existir en el tenant.");
        }

        Rol entity;
        if (request.Id is long gid)
        {
            var found = await _db.Roles.FirstOrDefaultAsync(x => x.Id == gid, cancellationToken);
            if (found is null)
            {
                return RolResult<RolDto>.NotFound("El rol no existe.");
            }
            entity = found;

            var renombra = !string.Equals(entity.Name, trimmed, StringComparison.Ordinal);
            // Rol de sistema: no se renombra... salvo la excepcion explicita (DAT-05).
            if (entity.IsSystem && renombra && !entity.AllowRename)
            {
                return RolResult<RolDto>.Invalid("El rol de sistema no se puede renombrar.");
            }
            // ...y su nivel de acceso maximo NUNCA se cambia, ni con la excepcion de renombrado:
            // es lo que define su alcance sobre lo Reservado/Clasificado.
            if (entity.IsSystem && entity.NivelAccesoMaximoId != request.NivelAccesoMaximoId)
            {
                return RolResult<RolDto>.Invalid(
                    "No se puede cambiar el nivel de acceso maximo de un rol de sistema.");
            }
            if (await _db.Roles.AnyAsync(x => x.Name == trimmed && x.Id != gid, cancellationToken))
            {
                return RolResult<RolDto>.Conflict($"Ya existe un rol con el nombre '{trimmed}'.");
            }

            var prev = new { entity.Name, entity.Description, entity.Estado, entity.NivelAccesoMaximoId };
            entity.Name = trimmed;
            entity.Description = descripcion;
            entity.Estado = request.Estado;
            entity.NivelAccesoMaximoId = request.NivelAccesoMaximoId;
            _audit.Write(actorUserId, "rol.update", nameof(Rol), entity,
                previousValue: prev,
                newValue: new { entity.Name, entity.Description, entity.Estado, entity.NivelAccesoMaximoId },
                tenantId: entity.TenantId);
        }
        else
        {
            if (_tenant.TenantId is not long tenantId)
            {
                return RolResult<RolDto>.Invalid("No hay tenant activo.");
            }
            if (await _db.Roles.AnyAsync(x => x.Name == trimmed, cancellationToken))
            {
                return RolResult<RolDto>.Conflict($"Ya existe un rol con el nombre '{trimmed}'.");
            }
            entity = new Rol
            {
                TenantId = tenantId,
                Name = trimmed,
                Description = descripcion,
                NivelAccesoMaximoId = request.NivelAccesoMaximoId,
                Estado = request.Estado,
                IsSystem = false,
                AllowRename = true,
                CodigoSistema = null
            };
            _db.Roles.Add(entity);
            // Auditoria con la ENTIDAD: en un alta el Id todavia vale 0 (identity de la base), y
            // la sobrecarga por id dejaria el asiento sin identificar (incumple RNF-04).
            _audit.Write(actorUserId, "rol.create", nameof(Rol), entity,
                previousValue: null,
                newValue: new { entity.Name, entity.Description, entity.Estado, entity.NivelAccesoMaximoId },
                tenantId: entity.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ahora = Now;
        var userCount = await _db.UsuariosRoles.CountAsync(
            ur => ur.RolId == entity.Id
                && (ur.VigenteDesde == null || ur.VigenteDesde <= ahora)
                && (ur.VigenteHasta == null || ahora < ur.VigenteHasta),
            cancellationToken);

        return RolResult<RolDto>.Ok(new RolDto(
            entity.Id, entity.Name, entity.Description, entity.Estado, entity.IsSystem,
            entity.AllowRename, entity.CodigoSistema, entity.NivelAccesoMaximoId,
            nivel.Nombre, nivel.NivelOrden, userCount));
    }

    public async Task<RolResult<bool>> DeleteAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Roles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return RolResult<bool>.NotFound("El rol no existe.");
        }
        // Un rol predeterminado no se elimina JAMAS, ni siquiera el renombrable: el motor de
        // workflows (RQ11) referencia a "Lider de Dependencia" por su codigo de sistema.
        if (entity.IsSystem)
        {
            return RolResult<bool>.Invalid("No se puede eliminar un rol predeterminado del sistema.");
        }
        // Se cuentan TODAS las asignaciones, vigentes o no: borrar el rol dejaria filas huerfanas
        // y borraria historia de quien lo tuvo.
        var users = await _db.UsuariosRoles.CountAsync(ur => ur.RolId == id, cancellationToken);
        if (users > 0)
        {
            return RolResult<bool>.Invalid(
                $"No se puede eliminar: el rol tiene {users} usuario(s) asignado(s). Reasigna esos usuarios primero.");
        }

        var joinTx = _db.HasActiveTransaction;
        var tx = joinTx ? null : await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            var permisos = await _db.RolPermisos.Where(p => p.RolId == id).ToListAsync(cancellationToken);
            _db.RolPermisos.RemoveRange(permisos);
            _db.Roles.Remove(entity);
            _audit.Write(actorUserId, "rol.delete", nameof(Rol), entity,
                previousValue: new { entity.Name, entity.CodigoSistema },
                newValue: null,
                tenantId: entity.TenantId);
            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) { await tx.CommitAsync(cancellationToken); }
        }
        catch
        {
            if (tx is not null) { await tx.RollbackAsync(cancellationToken); }
            throw;
        }
        finally
        {
            if (tx is not null) { await tx.DisposeAsync(); }
        }

        return RolResult<bool>.Ok(true);
    }

    // ---- Matriz de permisos ----

    public async Task<RolResult<bool>> SavePermisosAsync(
        long rolId, IReadOnlyList<ModulePermissionDto> permisos, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rol = await _db.Roles.FirstOrDefaultAsync(x => x.Id == rolId, cancellationToken);
        if (rol is null)
        {
            return RolResult<bool>.NotFound("El rol no existe.");
        }

        // Solo se persisten los modulos que conceden algo (logica pura, testeable).
        var toPersist = PermissionResolver.FilterPersistable(permisos);

        var joinTx = _db.HasActiveTransaction;
        var tx = joinTx ? null : await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await _db.RolPermisos.Where(p => p.RolId == rolId).ToListAsync(cancellationToken);
            _db.RolPermisos.RemoveRange(existing);

            var filas = 0;
            foreach (var p in toPersist)
            {
                // UNA FILA POR (modulo, accion) concedida: la spec prohibe bitmask y JSON.
                foreach (var accion in p.GrantedActions())
                {
                    _db.RolPermisos.Add(new RolPermiso
                    {
                        TenantId = rol.TenantId,
                        RolId = rolId,
                        Modulo = p.ModuleKey,
                        Accion = accion,
                        Permitido = true
                    });
                    filas++;
                }
            }

            _audit.Write(actorUserId, "rol.save-permisos", nameof(Rol), rol,
                previousValue: new { Filas = existing.Count },
                newValue: new { Filas = filas, Modulos = toPersist.Count },
                tenantId: rol.TenantId);
            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) { await tx.CommitAsync(cancellationToken); }
        }
        catch
        {
            if (tx is not null) { await tx.RollbackAsync(cancellationToken); }
            throw;
        }
        finally
        {
            if (tx is not null) { await tx.DisposeAsync(); }
        }

        return RolResult<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<ModuloInfo>> GetModuleCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        // Vista IsDefault del tenant (o la primera si ninguna esta marcada).
        var defaultView = await _db.MenuViews.AsNoTracking()
            .Where(v => v.IsDefault)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Name)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.MenuViews.AsNoTracking()
                .OrderBy(v => v.SortOrder).ThenBy(v => v.Name)
                .FirstOrDefaultAsync(cancellationToken);

        if (defaultView is null)
        {
            return ModuleCatalogFallback.Modules;
        }

        var nodes = await _db.MenuNodes.AsNoTracking()
            .Where(n => n.MenuViewId == defaultView.Id)
            .Select(n => new { n.Id, n.ParentId, n.Kind, n.Name, n.Route, n.State, n.SortOrder })
            .ToListAsync(cancellationToken);

        var byId = nodes.ToDictionary(n => n.Id);

        // Nombre del grupo = Section ancestro del Item (subiendo por ParentId a traves de Subgroups).
        string GroupNameFor(long? parentId)
        {
            var guard = 0;
            var current = parentId;
            while (current is long pid && byId.TryGetValue(pid, out var node) && guard++ < 100)
            {
                if (node.Kind == MenuNodeKind.Section)
                {
                    return node.Name;
                }
                current = node.ParentId;
            }
            return "Sin grupo";
        }

        var catalog = nodes
            .Where(n => n.Kind == MenuNodeKind.Item
                && n.State == MenuNodeState.Ready
                && !string.IsNullOrWhiteSpace(n.Route))
            // Un modulo real = un Route unico (varios items pueden reusar la misma pagina;
            // gana el primero por orden de aparicion en el menu).
            .GroupBy(n => n.Route!, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.OrderBy(x => x.SortOrder).First();
                return new ModuloInfo(first.Route!, first.Name, GroupNameFor(first.ParentId));
            })
            .OrderBy(m => m.Grupo, StringComparer.Ordinal).ThenBy(m => m.Label, StringComparer.Ordinal)
            .ToList();

        return catalog.Count > 0 ? catalog : ModuleCatalogFallback.Modules;
    }

    // ---- Resolucion de permisos efectivos (FAIL-CLOSED) ----

    public async Task<EffectivePermissions> ResolveEffectivePermissionsAsync(
        long platformUserId, CancellationToken cancellationToken = default)
    {
        var tenantUserId = await _db.TenantUsers.AsNoTracking()
            .Where(u => u.PlatformUserId == platformUserId)
            .Select(u => (long?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantUserId is not long tuId)
        {
            // Usuario no resoluble en este tenant: SIN PERMISOS (invariante 10). El backbone
            // devolvia acceso total aqui; eso es exactamente la fuga que TRONOX no puede permitir.
            return EffectivePermissions.None;
        }

        // Asignaciones del usuario con el nivel y el estado de cada rol. El filtro de VIGENCIA
        // no se hace en SQL: vive en PermissionResolver (logica pura y testeable sin BD).
        var asignaciones = await _db.UsuariosRoles.AsNoTracking()
            .Where(ur => ur.TenantUserId == tuId)
            .Select(ur => new
            {
                ur.RolId,
                ur.VigenteDesde,
                ur.VigenteHasta,
                Estado = ur.Rol!.Estado,
                NivelOrden = ur.Rol!.NivelAccesoMaximo!.NivelOrden
            })
            .ToListAsync(cancellationToken);

        if (asignaciones.Count == 0)
        {
            return EffectivePermissions.None;
        }

        var permisos = await LoadPermisosAsync(
            asignaciones.Select(a => a.RolId).Distinct().ToList(), cancellationToken);

        var grants = asignaciones.Select(a => new RolGrant(
            a.RolId,
            a.Estado,
            a.NivelOrden,
            a.VigenteDesde,
            a.VigenteHasta,
            permisos.TryGetValue(a.RolId, out var m) ? m : []));

        return PermissionResolver.Resolve(grants, Now);
    }

    // ---- Asignacion de roles a usuarios (multi-rol con vigencia) ----

    public async Task<IReadOnlyList<RolAsignacionDto>> GetUserRolesAsync(
        long tenantUserId, CancellationToken cancellationToken = default)
        => await _db.UsuariosRoles.AsNoTracking()
            .Where(ur => ur.TenantUserId == tenantUserId)
            .OrderBy(ur => ur.Rol!.Name)
            .Select(ur => new RolAsignacionDto(
                ur.RolId, ur.Rol!.Name, ur.VigenteDesde, ur.VigenteHasta))
            .ToListAsync(cancellationToken);

    public async Task<RolResult<bool>> AssignRoleToUserAsync(
        long tenantUserId, long rolId, DateTimeOffset? vigenteDesde, DateTimeOffset? vigenteHasta,
        long actorUserId, CancellationToken cancellationToken = default)
    {
        if (vigenteDesde is DateTimeOffset d && vigenteHasta is DateTimeOffset h && h <= d)
        {
            return RolResult<bool>.Invalid("La fecha de expiracion debe ser posterior al inicio de vigencia.");
        }

        var user = await _db.TenantUsers.FirstOrDefaultAsync(u => u.Id == tenantUserId, cancellationToken);
        if (user is null)
        {
            return RolResult<bool>.NotFound("El usuario no existe.");
        }
        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Id == rolId, cancellationToken);
        if (rol is null)
        {
            return RolResult<bool>.NotFound("El rol no existe.");
        }

        var existing = await _db.UsuariosRoles
            .FirstOrDefaultAsync(ur => ur.TenantUserId == tenantUserId && ur.RolId == rolId, cancellationToken);

        if (existing is null)
        {
            var asignacion = new UsuarioRol
            {
                TenantId = user.TenantId,
                TenantUserId = tenantUserId,
                RolId = rolId,
                VigenteDesde = vigenteDesde,
                VigenteHasta = vigenteHasta
            };
            _db.UsuariosRoles.Add(asignacion);
            _audit.Write(actorUserId, "usuario-rol.assign", nameof(UsuarioRol), asignacion,
                previousValue: null,
                newValue: new { TenantUserId = tenantUserId, RolId = rolId, vigenteDesde, vigenteHasta },
                tenantId: user.TenantId);
        }
        else
        {
            var prev = new { existing.VigenteDesde, existing.VigenteHasta };
            existing.VigenteDesde = vigenteDesde;
            existing.VigenteHasta = vigenteHasta;
            _audit.Write(actorUserId, "usuario-rol.update-vigencia", nameof(UsuarioRol), existing,
                previousValue: prev,
                newValue: new { vigenteDesde, vigenteHasta },
                tenantId: user.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return RolResult<bool>.Ok(true);
    }

    public async Task<RolResult<bool>> RevokeRoleFromUserAsync(
        long tenantUserId, long rolId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.UsuariosRoles
            .FirstOrDefaultAsync(ur => ur.TenantUserId == tenantUserId && ur.RolId == rolId, cancellationToken);
        if (existing is null)
        {
            return RolResult<bool>.NotFound("El usuario no tiene asignado ese rol.");
        }

        _db.UsuariosRoles.Remove(existing);
        _audit.Write(actorUserId, "usuario-rol.revoke", nameof(UsuarioRol), existing,
            previousValue: new { existing.TenantUserId, existing.RolId, existing.VigenteHasta },
            newValue: null,
            tenantId: existing.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return RolResult<bool>.Ok(true);
    }

    public async Task<RolResult<bool>> SetUserRolesAsync(
        long tenantUserId, IReadOnlyList<RolAsignacionDto> roles, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.TenantUsers.FirstOrDefaultAsync(u => u.Id == tenantUserId, cancellationToken);
        if (user is null)
        {
            return RolResult<bool>.NotFound("El usuario no existe.");
        }

        var deseados = roles
            .GroupBy(r => r.RolId)
            .ToDictionary(g => g.Key, g => g.Last());

        if (deseados.Count > 0)
        {
            var ids = deseados.Keys.ToList();
            var existentes = await _db.Roles.CountAsync(r => ids.Contains(r.Id), cancellationToken);
            if (existentes != ids.Count)
            {
                return RolResult<bool>.NotFound("Alguno de los roles indicados no existe.");
            }
        }

        var joinTx = _db.HasActiveTransaction;
        var tx = joinTx ? null : await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            var actuales = await _db.UsuariosRoles
                .Where(ur => ur.TenantUserId == tenantUserId)
                .ToListAsync(cancellationToken);

            foreach (var sobrante in actuales.Where(a => !deseados.ContainsKey(a.RolId)))
            {
                _db.UsuariosRoles.Remove(sobrante);
            }

            foreach (var (rolId, deseado) in deseados)
            {
                var actual = actuales.FirstOrDefault(a => a.RolId == rolId);
                if (actual is null)
                {
                    _db.UsuariosRoles.Add(new UsuarioRol
                    {
                        TenantId = user.TenantId,
                        TenantUserId = tenantUserId,
                        RolId = rolId,
                        VigenteDesde = deseado.VigenteDesde,
                        VigenteHasta = deseado.VigenteHasta
                    });
                }
                else
                {
                    actual.VigenteDesde = deseado.VigenteDesde;
                    actual.VigenteHasta = deseado.VigenteHasta;
                }
            }

            _audit.Write(actorUserId, "usuario-rol.set", nameof(TenantUser), user,
                previousValue: new { Roles = actuales.Select(a => a.RolId).ToArray() },
                newValue: new { Roles = deseados.Keys.ToArray() },
                tenantId: user.TenantId);

            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) { await tx.CommitAsync(cancellationToken); }
        }
        catch
        {
            if (tx is not null) { await tx.RollbackAsync(cancellationToken); }
            throw;
        }
        finally
        {
            if (tx is not null) { await tx.DisposeAsync(); }
        }

        return RolResult<bool>.Ok(true);
    }
}

/// <summary>
/// Catalogo minimo de modulos usado solo cuando el tenant aun no tiene menu configurado
/// (defensa: la matriz nunca queda vacia). En operacion normal el catalogo sale del menu real.
/// </summary>
public static class ModuleCatalogFallback
{
    public static readonly IReadOnlyList<ModuloInfo> Modules = new List<ModuloInfo>
    {
        new("admin-usuarios", "Administracion de usuarios", "Sistema - General"),
        new("roles-permisos", "Roles y permisos", "Sistema - General"),
    };
}
