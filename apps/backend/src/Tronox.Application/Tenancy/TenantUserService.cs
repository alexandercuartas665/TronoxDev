using Tronox.Application.Common;
using Tronox.Application.Common.Auth;
using Tronox.Application.Organization;
using Tronox.Application.Roles;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Tenancy;

public sealed class TenantUserService : ITenantUserService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditWriter _audit;

    public TenantUserService(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IPasswordHasher passwordHasher,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _audit = audit;
    }

    public async Task<IReadOnlyList<TenantUserDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        // El filtro global del DbContext limita por el tenant del contexto.
        // DisplayName viene del PlatformUser (join aditivo, ola 3): los dropdowns de
        // asignado muestran el nombre legible en vez del email.
        return await _db.TenantUsers
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Join(_db.PlatformUsers.AsNoTracking(),
                tu => tu.PlatformUserId, pu => pu.Id,
                (tu, pu) => new TenantUserDto(tu.Id, tu.PlatformUserId, tu.Email, tu.TenantRole, tu.Status, pu.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantUserDto?> InviteAsync(InviteTenantUserRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return null;
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var platformUser = await _db.PlatformUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        var esUsuarioNuevo = platformUser is null;
        if (platformUser is null)
        {
            platformUser = new PlatformUser
            {
                Email = email,
                DisplayName = request.DisplayName?.Trim(),
                EmailVerified = false,
                AuthProvider = "local",
                Status = string.IsNullOrEmpty(request.Password) ? PlatformUserStatus.Invited : PlatformUserStatus.Active,
                PasswordHash = string.IsNullOrEmpty(request.Password) ? null : _passwordHasher.Hash(request.Password)
            };
            _db.PlatformUsers.Add(platformUser);
        }

        // Filtro global: solo ve miembros del tenant activo. Un usuario recien creado no puede
        // ser miembro de nada todavia, y ademas su Id vale 0 hasta que la base lo genere: la
        // consulta no tendria sentido y siempre daria falso.
        if (!esUsuarioNuevo)
        {
            var alreadyMember = await _db.TenantUsers.AnyAsync(tu => tu.PlatformUserId == platformUser.Id, cancellationToken);
            if (alreadyMember)
            {
                return null;
            }
        }

        var tenantUser = new TenantUser
        {
            TenantId = tenantId,
            // Por navegacion, NO por id: con ids de identidad el Id del padre vale 0 hasta que
            // se guarda. EF resuelve la FK y el orden de insercion.
            PlatformUser = platformUser,
            Email = email,
            TenantRole = request.Role,
            Status = PlatformUserStatus.Active
        };
        _db.TenantUsers.Add(tenantUser);

        _audit.Write(actorUserId, "tenant-user.invite", nameof(TenantUser), tenantUser,
            previousValue: null,
            newValue: new { email, request.Role },
            tenantId: tenantId);

        await _db.SaveChangesAsync(cancellationToken);
        return Map(tenantUser);
    }

    public async Task<TenantUserDto?> ChangeRoleAsync(long tenantUserId, TenantRole role, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tenantUser = await _db.TenantUsers.FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken);
        if (tenantUser is null)
        {
            return null;
        }

        var previous = tenantUser.TenantRole;
        if (previous != role)
        {
            tenantUser.TenantRole = role;
            _audit.Write(actorUserId, "tenant-user.change-role", nameof(TenantUser), tenantUser,
                previousValue: new { Role = previous },
                newValue: new { Role = role },
                tenantId: tenantUser.TenantId);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Map(tenantUser);
    }

    public async Task<TenantUserDto?> SetStatusAsync(long tenantUserId, PlatformUserStatus status, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tenantUser = await _db.TenantUsers.FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken);
        if (tenantUser is null)
        {
            return null;
        }

        var previous = tenantUser.Status;
        if (previous != status)
        {
            tenantUser.Status = status;
            _audit.Write(actorUserId, "tenant-user.set-status", nameof(TenantUser), tenantUser,
                previousValue: new { Status = previous },
                newValue: new { Status = status },
                tenantId: tenantUser.TenantId);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Map(tenantUser);
    }

    public async Task<TenantUserDto?> ResetPasswordAsync(long tenantUserId, string newPassword, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new ArgumentException("La clave debe tener al menos 6 caracteres.", nameof(newPassword));
        }

        // Filtro global: solo alcanza usuarios del tenant activo.
        var tenantUser = await _db.TenantUsers.FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken);
        if (tenantUser is null)
        {
            return null;
        }

        var platformUser = await _db.PlatformUsers.FirstOrDefaultAsync(pu => pu.Id == tenantUser.PlatformUserId, cancellationToken);
        if (platformUser is null)
        {
            return null;
        }

        platformUser.PasswordHash = _passwordHasher.Hash(newPassword);
        // Un usuario invitado que ya recibe clave del admin queda activo (puede iniciar sesion),
        // PERO solo si cumple el criterio 2 de RF06 5.6.3: dependencia + cargo + al menos un rol.
        // Antes la clave activaba por si sola, que era una puerta lateral a la misma regla que la
        // pantalla de usuarios hace cumplir.
        var reactivated = false;
        if (tenantUser.Status == PlatformUserStatus.Invited
            && await MotivoNoActivableAsync(tenantUser, cancellationToken) is null)
        {
            platformUser.Status = PlatformUserStatus.Active;
            tenantUser.Status = PlatformUserStatus.Active;
            reactivated = true;
        }
        else if (platformUser.Status == PlatformUserStatus.Invited
            && tenantUser.Status == PlatformUserStatus.Active)
        {
            // La cuenta del tenant ya estaba activa: la de plataforma solo tenia pendiente la clave.
            platformUser.Status = PlatformUserStatus.Active;
            reactivated = true;
        }

        // Auditoria SIN la clave (solo el hecho y si reactivo la cuenta).
        _audit.Write(actorUserId, "tenant-user.reset-password", nameof(TenantUser), tenantUser,
            previousValue: null,
            newValue: new { Reactivated = reactivated },
            tenantId: tenantUser.TenantId);

        await _db.SaveChangesAsync(cancellationToken);
        return Map(tenantUser, platformUser.DisplayName);
    }

    public async Task<TenantUserDto?> UpdateProfileAsync(long tenantUserId, string? displayName, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tenantUser = await _db.TenantUsers.FirstOrDefaultAsync(tu => tu.Id == tenantUserId, cancellationToken);
        if (tenantUser is null)
        {
            return null;
        }

        var platformUser = await _db.PlatformUsers.FirstOrDefaultAsync(pu => pu.Id == tenantUser.PlatformUserId, cancellationToken);
        if (platformUser is null)
        {
            return null;
        }

        var normalized = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        var previous = platformUser.DisplayName;
        if (previous != normalized)
        {
            platformUser.DisplayName = normalized;
            _audit.Write(actorUserId, "tenant-user.update-profile", nameof(TenantUser), tenantUser,
                previousValue: new { DisplayName = previous },
                newValue: new { DisplayName = normalized },
                tenantId: tenantUser.TenantId);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Map(tenantUser, normalized);
    }

    private static TenantUserDto Map(TenantUser u, string? displayName = null) =>
        new(u.Id, u.PlatformUserId, u.Email, u.TenantRole, u.Status, displayName);

    // =====================================================================================
    // RQ01 - RF06: Gestion de Usuarios (Funcionarios)
    // =====================================================================================

    public async Task<IReadOnlyList<FuncionarioDto>> ListFuncionariosAsync(
        CancellationToken cancellationToken = default)
    {
        // El filtro global acota al tenant activo en las cuatro consultas.
        var usuarios = await _db.TenantUsers.AsNoTracking()
            .OrderBy(u => u.Apellidos).ThenBy(u => u.Nombres).ThenBy(u => u.Email)
            .ToListAsync(cancellationToken);
        if (usuarios.Count == 0)
        {
            return [];
        }

        var contexto = await CargarContextoAsync(cancellationToken);
        var rolesPorUsuario = await CargarRolesAsync(usuarios.Select(u => u.Id).ToList(), cancellationToken);

        return usuarios.Select(u => ToFuncionario(u, contexto, rolesPorUsuario)).ToList();
    }

    public async Task<FuncionarioDto?> GetFuncionarioAsync(
        long tenantUserId, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.TenantUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == tenantUserId, cancellationToken);
        if (usuario is null)
        {
            return null;
        }
        var contexto = await CargarContextoAsync(cancellationToken);
        var roles = await CargarRolesAsync([tenantUserId], cancellationToken);
        return ToFuncionario(usuario, contexto, roles);
    }

    public async Task<TenancyResult<FuncionarioDto>> SaveFuncionarioAsync(
        SaveFuncionarioRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return TenancyResult<FuncionarioDto>.Invalid("No hay tenant activo.");
        }

        // 1. Reglas PURAS (sin base de datos).
        var error = FuncionarioRules.ValidateDatos(
            request.TipoDocumento, request.NumeroDocumento, request.Nombres, request.Apellidos,
            request.CorreoElectronico, request.Telefono);
        if (error is not null)
        {
            return TenancyResult<FuncionarioDto>.Invalid(error);
        }

        var correo = FuncionarioRules.NormalizarCorreo(request.CorreoElectronico);
        var documento = request.NumeroDocumento!.Trim();

        // 2. Unicidad DENTRO del tenant (el filtro global la acota; no hace falta comparar
        //    TenantId a mano). El correo es el LOGIN (criterio 1 de 5.6.3).
        if (await _db.TenantUsers.AnyAsync(
                u => u.Email == correo && (request.Id == null || u.Id != request.Id), cancellationToken))
        {
            return TenancyResult<FuncionarioDto>.Conflict(
                $"Ya existe un funcionario con el correo '{correo}' en esta entidad.");
        }
        if (await _db.TenantUsers.AnyAsync(
                u => u.NumeroDocumento == documento && (request.Id == null || u.Id != request.Id), cancellationToken))
        {
            return TenancyResult<FuncionarioDto>.Conflict(
                $"Ya existe un funcionario con el documento '{documento}' en esta entidad.");
        }

        // 3. Referencias que deben existir dentro del tenant.
        if (request.CargoOrgUnitId is long cargoId)
        {
            var clasificador = await _db.OrgUnits.AsNoTracking()
                .Where(o => o.Id == cargoId)
                .Select(o => (OrgUnitClassifier?)o.Classifier)
                .FirstOrDefaultAsync(cancellationToken);
            if (clasificador is null)
            {
                return TenancyResult<FuncionarioDto>.NotFound("El cargo seleccionado no existe.");
            }
            if (clasificador != OrgUnitClassifier.Cargo)
            {
                return TenancyResult<FuncionarioDto>.Invalid(
                    "El funcionario se ancla a un nodo CARGO del organigrama, no a otro tipo de nodo.");
            }
        }
        if (request.SedeId is long sedeId
            && !await _db.Sedes.AsNoTracking().AnyAsync(s => s.Id == sedeId, cancellationToken))
        {
            return TenancyResult<FuncionarioDto>.NotFound("La sede seleccionada no existe.");
        }

        TenantUser usuario;
        object? previo = null;
        if (request.Id is long id)
        {
            var existente = await _db.TenantUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            if (existente is null)
            {
                return TenancyResult<FuncionarioDto>.NotFound("El funcionario no existe en esta entidad.");
            }
            usuario = existente;
            previo = Snapshot(usuario);
        }
        else
        {
            // Alta: el PlatformUser es la identidad global; si el correo ya existe se reutiliza.
            var platformUser = await _db.PlatformUsers.FirstOrDefaultAsync(p => p.Email == correo, cancellationToken);
            if (platformUser is null)
            {
                platformUser = new PlatformUser
                {
                    Email = correo,
                    EmailVerified = false,
                    AuthProvider = "local",
                    Status = PlatformUserStatus.Invited,
                    PasswordHash = string.IsNullOrEmpty(request.Password)
                        ? null
                        : _passwordHasher.Hash(request.Password)
                };
                _db.PlatformUsers.Add(platformUser);
            }
            else if (!string.IsNullOrEmpty(request.Password))
            {
                platformUser.PasswordHash = _passwordHasher.Hash(request.Password);
            }

            usuario = new TenantUser
            {
                TenantId = tenantId,
                // Por navegacion, NO por id: en un alta el Id del PlatformUser vale 0 hasta guardar.
                PlatformUser = platformUser,
                // Un funcionario recien creado NUNCA nace Activo: se activa cuando cumple el
                // criterio 2 de 5.6.3 (dependencia + cargo + rol), por SetFuncionarioEstadoAsync.
                Status = PlatformUserStatus.Invited
            };
            _db.TenantUsers.Add(usuario);
        }

        usuario.Email = correo;
        usuario.TenantRole = request.TenantRole;
        usuario.TipoDocumento = request.TipoDocumento;
        usuario.NumeroDocumento = documento;
        usuario.Nombres = request.Nombres!.Trim();
        usuario.Apellidos = request.Apellidos!.Trim();
        usuario.Phone = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
        usuario.CargoOrgUnitId = request.CargoOrgUnitId;
        usuario.SedeId = request.SedeId;
        usuario.FechaVinculacion = request.FechaVinculacion;

        // El DisplayName del PlatformUser se mantiene alineado con los datos de RF06 para que el
        // resto del sistema (menu, asignaciones, auditoria) muestre el nombre y no el correo.
        var platform = await _db.PlatformUsers.FirstOrDefaultAsync(
            p => p.Id == usuario.PlatformUserId || p.Email == correo, cancellationToken);
        if (platform is not null)
        {
            platform.DisplayName = $"{usuario.Nombres} {usuario.Apellidos}".Trim();
        }

        // Auditoria con la ENTIDAD (criterio 6 de 5.6.3): en un alta el Id vale 0 hasta SaveChanges.
        _audit.Write(actorUserId, request.Id is null ? "funcionario.crear" : "funcionario.editar",
            nameof(TenantUser), usuario,
            previousValue: previo,
            newValue: Snapshot(usuario),
            tenantId: tenantId);

        await _db.SaveChangesAsync(cancellationToken);

        var dto = await GetFuncionarioAsync(usuario.Id, cancellationToken);
        return dto is null
            ? TenancyResult<FuncionarioDto>.NotFound()
            : TenancyResult<FuncionarioDto>.Ok(dto);
    }

    public async Task<TenancyResult<FuncionarioDto>> SetFuncionarioEstadoAsync(
        long tenantUserId, PlatformUserStatus estado, string? motivo, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _db.TenantUsers.FirstOrDefaultAsync(u => u.Id == tenantUserId, cancellationToken);
        if (usuario is null)
        {
            return TenancyResult<FuncionarioDto>.NotFound("El funcionario no existe en esta entidad.");
        }

        if (estado == PlatformUserStatus.Active)
        {
            var motivoNoActivable = await MotivoNoActivableAsync(usuario, cancellationToken);
            if (motivoNoActivable is not null)
            {
                return TenancyResult<FuncionarioDto>.Invalid(motivoNoActivable);
            }
        }

        var previo = usuario.Status;
        if (previo != estado)
        {
            usuario.Status = estado;
            // Inactivar CONSERVA todo (criterio 4): no se borra ni se desliga nada, solo cambia
            // el estado de la cuenta. Nunca hay eliminacion real (invariante 8).
            _audit.Write(actorUserId, "funcionario.cambiar-estado", nameof(TenantUser), usuario,
                previousValue: new { Status = previo },
                newValue: new { Status = estado },
                tenantId: usuario.TenantId,
                reason: motivo);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var dto = await GetFuncionarioAsync(tenantUserId, cancellationToken);
        return dto is null
            ? TenancyResult<FuncionarioDto>.NotFound()
            : TenancyResult<FuncionarioDto>.Ok(dto);
    }

    public async Task<TenancyResult<FuncionarioDto>> SetFirmaImagenAsync(
        long tenantUserId, string? rutaSegura, long actorUserId, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.TenantUsers.FirstOrDefaultAsync(u => u.Id == tenantUserId, cancellationToken);
        if (usuario is null)
        {
            return TenancyResult<FuncionarioDto>.NotFound("El funcionario no existe en esta entidad.");
        }
        if (rutaSegura is { Length: > 500 })
        {
            return TenancyResult<FuncionarioDto>.Invalid("La ruta de la firma no puede superar 500 caracteres.");
        }

        var previo = usuario.FirmaImagenPath;
        // Solo la RUTA: los bytes viven en el almacen de objetos (invariante 9).
        usuario.FirmaImagenPath = string.IsNullOrWhiteSpace(rutaSegura) ? null : rutaSegura.Trim();
        _audit.Write(actorUserId, "funcionario.firma-imagen", nameof(TenantUser), usuario,
            previousValue: new { FirmaImagenPath = previo },
            newValue: new { usuario.FirmaImagenPath },
            tenantId: usuario.TenantId);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await GetFuncionarioAsync(tenantUserId, cancellationToken);
        return dto is null
            ? TenancyResult<FuncionarioDto>.NotFound()
            : TenancyResult<FuncionarioDto>.Ok(dto);
    }

    // ---- Internos de RF06 ----

    /// <summary>
    /// Contexto de lectura comun: arbol organizacional (para DERIVAR la dependencia), nombres de
    /// nodos y nombres de sedes. Se carga UNA vez por listado en vez de una consulta por fila.
    /// </summary>
    private sealed record ContextoFuncionarios(
        Dictionary<long, OrgUnitTree.NodeRef> Nodos,
        Dictionary<long, string> NombrePorNodo,
        Dictionary<long, NivelJerarquico?> NivelPorNodo,
        Dictionary<long, string> NombrePorSede);

    private async Task<ContextoFuncionarios> CargarContextoAsync(CancellationToken cancellationToken)
    {
        // Incluye los archivados a proposito: la cadena de ancestros de un cargo activo puede
        // pasar por un nodo archivado, y cortarla ahi daria una dependencia equivocada.
        var nodos = await _db.OrgUnits.AsNoTracking()
            .Select(o => new { o.Id, o.ParentId, o.Classifier, o.Name, o.NivelJerarquico })
            .ToListAsync(cancellationToken);
        var sedes = await _db.Sedes.AsNoTracking()
            .Select(s => new { s.Id, s.NombreSede })
            .ToListAsync(cancellationToken);

        return new ContextoFuncionarios(
            nodos.ToDictionary(n => n.Id, n => new OrgUnitTree.NodeRef(n.Id, n.ParentId, n.Classifier)),
            nodos.ToDictionary(n => n.Id, n => n.Name),
            nodos.ToDictionary(n => n.Id, n => n.NivelJerarquico),
            sedes.ToDictionary(s => s.Id, s => s.NombreSede));
    }

    private async Task<Dictionary<long, List<RolAsignacionDto>>> CargarRolesAsync(
        IReadOnlyList<long> tenantUserIds, CancellationToken cancellationToken)
    {
        var filas = await _db.UsuariosRoles.AsNoTracking()
            .Where(ur => tenantUserIds.Contains(ur.TenantUserId))
            .Select(ur => new
            {
                ur.TenantUserId,
                ur.RolId,
                RolNombre = ur.Rol!.Name,
                ur.VigenteDesde,
                ur.VigenteHasta
            })
            .ToListAsync(cancellationToken);

        return filas
            .GroupBy(f => f.TenantUserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => new RolAsignacionDto(f.RolId, f.RolNombre, f.VigenteDesde, f.VigenteHasta))
                      .OrderBy(r => r.RolNombre)
                      .ToList());
    }

    private static FuncionarioDto ToFuncionario(
        TenantUser u,
        ContextoFuncionarios ctx,
        IReadOnlyDictionary<long, List<RolAsignacionDto>> rolesPorUsuario)
    {
        // La DEPENDENCIA se DERIVA del cargo (ADR-003, Addendum): nunca se almacena en el usuario.
        // FAIL-CLOSED: sin cargo, o con un cargo colgado de la raiz, resuelve a null = sin area
        // documental. Jamas a "todas".
        var dependenciaId = u.CargoOrgUnitId is long cargo
            ? OrgUnitTree.ResolveDependenciaId(cargo, ctx.Nodos)
            : null;

        return new FuncionarioDto(
            u.Id,
            u.PlatformUserId,
            u.Email,
            u.TenantRole,
            u.Status,
            u.TipoDocumento,
            u.NumeroDocumento,
            u.Nombres,
            u.Apellidos,
            u.Phone,
            u.CargoOrgUnitId,
            u.CargoOrgUnitId is long c && ctx.NombrePorNodo.TryGetValue(c, out var cn) ? cn : null,
            u.CargoOrgUnitId is long c2 && ctx.NivelPorNodo.TryGetValue(c2, out var nivel) ? nivel : null,
            dependenciaId,
            dependenciaId is long d && ctx.NombrePorNodo.TryGetValue(d, out var dn) ? dn : null,
            u.SedeId,
            u.SedeId is long s && ctx.NombrePorSede.TryGetValue(s, out var sn) ? sn : null,
            u.FechaVinculacion,
            u.FirmaImagenPath,
            u.MenuViewId,
            rolesPorUsuario.TryGetValue(u.Id, out var roles) ? roles : []);
    }

    /// <summary>
    /// Criterio 2 de 5.6.3 aplicado sobre datos reales: null = se puede activar. La caminata del
    /// arbol es la funcion pura de ADR-003; aqui solo se cargan los nodos.
    /// </summary>
    private async Task<string?> MotivoNoActivableAsync(TenantUser usuario, CancellationToken cancellationToken)
    {
        var nodos = await _db.OrgUnits.AsNoTracking()
            .Select(o => new OrgUnitTree.NodeRef(o.Id, o.ParentId, o.Classifier))
            .ToDictionaryAsync(n => n.Id, cancellationToken);
        var dependenciaId = usuario.CargoOrgUnitId is long cargo
            ? OrgUnitTree.ResolveDependenciaId(cargo, nodos)
            : null;
        var roles = await _db.UsuariosRoles.CountAsync(ur => ur.TenantUserId == usuario.Id, cancellationToken);
        return FuncionarioRules.ValidatePuedeActivar(usuario.CargoOrgUnitId, dependenciaId, roles);
    }

    private static object Snapshot(TenantUser u) => new
    {
        u.Email, u.TenantRole, u.Status, u.TipoDocumento, u.NumeroDocumento, u.Nombres, u.Apellidos,
        u.Phone, u.CargoOrgUnitId, u.SedeId, u.FechaVinculacion
    };
}
