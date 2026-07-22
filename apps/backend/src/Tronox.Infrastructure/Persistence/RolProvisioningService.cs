using Microsoft.EntityFrameworkCore;
using Tronox.Application.Roles;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Infrastructure.Persistence;

/// <summary>
/// Siembra de los ROLES PREDETERMINADOS de un tenant (RQ01 - RF05).
///
/// Misma mecanica que MenuProvisioningService y ClasificacionProvisioningService, y por la misma
/// razon: cuelga del camino de ALTA DEL TENANT (TenantAdminService y OnboardingService), NO de un
/// seeder de demo. Si se sembrara desde un seeder, los clientes creados desde el panel de
/// plataforma nacerian SIN roles, y como el sistema es FAIL-CLOSED (invariante 10) sus usuarios
/// no podrian hacer absolutamente nada.
///
/// La definicion canonica de los 7 roles vive en Application (RolCatalogo), no aqui, para que el
/// aprovisionamiento y los tests no puedan derivar.
///
/// IDEMPOTENTE y resistente al renombrado: la identidad de un rol predeterminado es su
/// CodigoSistema, no su nombre. Se siembra solo lo que falte; nunca se pisa lo que el tenant haya
/// personalizado (nombre, descripcion, estado o matriz).
/// </summary>
public sealed class RolProvisioningService : IRolProvisioningService
{
    private readonly TronoxDbContext _db;

    public RolProvisioningService(TronoxDbContext db) => _db = db;

    public async Task EnsureRolesPredeterminadosAsync(
        long tenantId, CancellationToken cancellationToken = default)
    {
        // Se consulta IGNORANDO el filtro global: el alta corre bajo el contexto de la plataforma,
        // no bajo el del tenant que se esta creando, asi que el filtro no aplicaria aqui.
        var nivelesPorCodigo = await _db.NivelesClasificacion
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId)
            .ToDictionaryAsync(n => n.Codigo, n => n.Id, StringComparer.Ordinal, cancellationToken);

        if (nivelesPorCodigo.Count == 0)
        {
            // Sin niveles no se puede sembrar: nivel_acceso_maximo es un FK obligatorio. El alta
            // del tenant llama primero a EnsureNivelesClasificacionAsync; si no hay niveles es que
            // el orden del alta se rompio, y es mejor no dejar roles a medias.
            return;
        }

        var yaSembrados = await _db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.CodigoSistema != null)
            .Select(r => r.CodigoSistema!)
            .ToListAsync(cancellationToken);
        var existentes = yaSembrados.ToHashSet(StringComparer.Ordinal);

        var nuevos = new List<(Rol Rol, RolCatalogo.RolSemilla Semilla)>();
        foreach (var semilla in RolCatalogo.Roles)
        {
            if (existentes.Contains(semilla.CodigoSistema))
            {
                continue;
            }
            if (!nivelesPorCodigo.TryGetValue(semilla.NivelCodigo, out var nivelId))
            {
                continue;
            }

            var rol = new Rol
            {
                TenantId = tenantId,
                Name = semilla.Nombre,
                Description = semilla.Descripcion,
                NivelAccesoMaximoId = nivelId,
                IsSystem = true,
                AllowRename = semilla.AllowRename,
                CodigoSistema = semilla.CodigoSistema,
                Estado = RolEstado.Activo
            };
            _db.Roles.Add(rol);
            nuevos.Add((rol, semilla));
        }

        if (nuevos.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            await SembrarMatrizCompletaAsync(tenantId, nuevos, cancellationToken);
        }

        // Siempre, aunque no se haya sembrado nada nuevo: es el ancla de arranque fail-closed.
        await EnsureOwnerTieneSuperAdminAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// Matriz completa (6 acciones x todos los modulos del menu) para los roles que gobiernan el
    /// tenant. Esto es lo que SUSTITUYE al bypass "AllowAll" del backbone: el Super Administrador
    /// lo puede todo porque su MATRIZ lo dice y queda auditada fila a fila, no porque el resolver
    /// de permisos lo deje pasar por una excepcion en el codigo.
    /// </summary>
    private async Task SembrarMatrizCompletaAsync(
        long tenantId,
        IReadOnlyList<(Rol Rol, RolCatalogo.RolSemilla Semilla)> nuevos,
        CancellationToken cancellationToken)
    {
        var conMatrizCompleta = nuevos.Where(n => n.Semilla.MatrizCompleta).ToList();
        if (conMatrizCompleta.Count == 0)
        {
            return;
        }

        var modulos = await ModulosDelMenuAsync(tenantId, cancellationToken);
        if (modulos.Count == 0)
        {
            return;
        }

        foreach (var (rol, _) in conMatrizCompleta)
        {
            foreach (var modulo in modulos)
            {
                foreach (var accion in PermissionActions.All)
                {
                    _db.RolPermisos.Add(new RolPermiso
                    {
                        TenantId = tenantId,
                        RolId = rol.Id,
                        Modulo = modulo,
                        Accion = accion,
                        Permitido = true
                    });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ancla del arranque FAIL-CLOSED: da "Super Administrador" a los usuarios Owner/Admin del
    /// tenant que no tengan NINGUN rol asignado.
    ///
    /// Hace falta precisamente porque se elimino el bypass del backbone. Antes, un Owner sin rol
    /// resolvia a acceso total por codigo; ahora resuelve a SIN PERMISOS, que es lo correcto pero
    /// dejaria al tenant recien creado sin nadie capaz de entrar a repartir permisos. El acceso
    /// del Owner pasa a estar donde debe estar: en una asignacion de rol explicita y auditable,
    /// no en una excepcion del resolver.
    ///
    /// Idempotente: solo toca usuarios con CERO asignaciones, asi que nunca revierte una
    /// decision del tenant (si le quitaron el rol al Owner a proposito, no se lo devuelve, porque
    /// para entonces ya tendra otra asignacion... y si lo dejaron sin ninguna, se asume arranque).
    /// </summary>
    private async Task EnsureOwnerTieneSuperAdminAsync(long tenantId, CancellationToken cancellationToken)
    {
        var superAdminId = await _db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && r.CodigoSistema == RolCatalogo.CodigoSuperAdministrador)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (superAdminId is not long rolId)
        {
            return;
        }

        var sinRoles = await _db.TenantUsers
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId
                && (u.TenantRole == TenantRole.Owner || u.TenantRole == TenantRole.Admin)
                && !_db.UsuariosRoles.IgnoreQueryFilters().Any(ur => ur.TenantUserId == u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (sinRoles.Count == 0)
        {
            return;
        }

        foreach (var tenantUserId in sinRoles)
        {
            _db.UsuariosRoles.Add(new UsuarioRol
            {
                TenantId = tenantId,
                TenantUserId = tenantUserId,
                RolId = rolId,
                VigenteDesde = null,
                VigenteHasta = null
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Modulos del tenant DERIVADOS del menu (misma regla que RolService.GetModuleCatalogAsync):
    /// nodos Item, en estado Ready y con Route no vacio, de la vista predeterminada. Sin listas
    /// paralelas que se desincronicen.
    /// </summary>
    private async Task<IReadOnlyList<string>> ModulosDelMenuAsync(
        long tenantId, CancellationToken cancellationToken)
    {
        var vista = await _db.MenuViews
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId && v.IsDefault)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Name)
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.MenuViews
                .IgnoreQueryFilters()
                .Where(v => v.TenantId == tenantId)
                .OrderBy(v => v.SortOrder).ThenBy(v => v.Name)
                .Select(v => (long?)v.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (vista is not long vistaId)
        {
            return [];
        }

        return await _db.MenuNodes
            .IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId
                && n.MenuViewId == vistaId
                && n.Kind == MenuNodeKind.Item
                && n.State == MenuNodeState.Ready
                && n.Route != null && n.Route != "")
            .Select(n => n.Route!)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
