using Tronox.Application.MenuConfig;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Infrastructure.Persistence;

/// <summary>
/// Unico punto de siembra del arbol canonico del menu de TRONOX (RF09 5.9.4). La DEFINICION del
/// arbol no vive aqui sino en Application (MenuCatalogo), para que el alta real, la demo y los
/// tests no puedan derivar (RF09 5.9.4 punto 4).
///
/// Se registra como IMenuProvisioningService y lo invocan LAS DOS rutas de alta de tenant
/// (TenantAdminService y OnboardingService), no un seeder de demo. Esa es la correccion al
/// riesgo documentado en ADR-001: en el sistema hermano el menu solo se sembraba para el
/// tenant demo, asi que los clientes creados desde el panel de plataforma nacian sin ninguna
/// vista y sus usuarios no veian nada.
///
/// POR QUE EL ARBOL DEBE ESTAR COMPLETO: el catalogo de modulos del tenant se DERIVA de este
/// arbol (nodos Item, Ready, con Route). Un arbol con secciones pero sin items produce CERO
/// modulos, la matriz del Super Administrador nace vacia y, con el enforcement fail-closed de
/// ADR-004, el tenant nace inusable. Por eso los items canonicos se siembran en estado Ready:
/// todas sus rutas resuelven (pagina real, o la pagina generica /modulo/{slug} para lo que aun
/// no esta construido), y marcarlos InDevelopment los dejaria fuera de la matriz, que es
/// exactamente el defecto que este servicio evita.
///
/// IDEMPOTENTE en dos niveles:
/// 1. Si el tenant ya tiene una vista CON items, no se toca nada: se respeta cualquier
///    personalizacion posterior del tenant (misma politica que RolProvisioningService).
/// 2. Si la vista existe pero quedo sin items (tenant sembrado por una version incompleta de este
///    servicio), se COMPLETA reutilizando los nodos ya presentes por su Route: no se duplica nada.
/// </summary>
public sealed class MenuProvisioningService : IMenuProvisioningService
{
    private readonly TronoxDbContext _db;

    public MenuProvisioningService(TronoxDbContext db) => _db = db;

    public async Task EnsureDefaultMenuAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        // Se consulta IGNORANDO el filtro global: el alta corre bajo el contexto de la plataforma,
        // no bajo el del tenant que se esta creando, asi que el filtro no aplicaria aqui.
        var vista = await _db.MenuViews
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == tenantId)
            .OrderByDescending(v => v.IsDefault).ThenBy(v => v.SortOrder).ThenBy(v => v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (vista is not null)
        {
            var yaTieneItems = await _db.MenuNodes
                .IgnoreQueryFilters()
                .AnyAsync(n => n.MenuViewId == vista.Id && n.Kind == MenuNodeKind.Item, cancellationToken);
            if (yaTieneItems)
            {
                return;
            }
        }
        else
        {
            vista = new MenuView
            {
                TenantId = tenantId,
                Name = MenuCatalogo.NombreVistaPredeterminada,
                Description = "Vista predeterminada con el arbol canonico de TRONOX.",
                IsDefault = true,
                SortOrder = 0
            };
            _db.MenuViews.Add(vista);
        }

        // Nodos de primer nivel que ya existan (tenant sembrado por la version incompleta): se
        // reutilizan por Route para no duplicar la seccion ni perder su Id.
        var existentes = new Dictionary<string, MenuNode>(StringComparer.Ordinal);
        if (vista.Id != 0)
        {
            var raices = await _db.MenuNodes
                .IgnoreQueryFilters()
                .Where(n => n.MenuViewId == vista.Id && n.ParentId == null && n.Route != null)
                .ToListAsync(cancellationToken);
            foreach (var raiz in raices)
            {
                existentes[raiz.Route!] = raiz;
            }
        }

        MenuNode Nodo(MenuNodeKind kind, string nombre, string? icono, string? ruta, string? codigoRf, MenuNode? padre, int orden)
        {
            var nodo = new MenuNode
            {
                TenantId = tenantId,
                MenuView = vista,
                Parent = padre,
                Kind = kind,
                Name = nombre,
                IconKey = icono,
                LegacyCode = codigoRf,
                Route = ruta,
                State = MenuNodeState.Ready,
                IsVisible = true,
                SortOrder = orden
            };
            _db.MenuNodes.Add(nodo);
            return nodo;
        }

        // El Id de la vista lo asigna la base al insertar (BIGINT identidad), asi que aqui todavia
        // vale 0: se enlaza por la propiedad de navegacion y EF resuelve la FK y el orden de insercion.
        if (!existentes.ContainsKey(MenuCatalogo.Inicio.Ruta))
        {
            Nodo(MenuNodeKind.QuickLink, MenuCatalogo.Inicio.Nombre, MenuCatalogo.IconoInicio,
                MenuCatalogo.Inicio.Ruta, null, null, 0);
        }

        var ordenSeccion = 1;
        foreach (var seccion in MenuCatalogo.Secciones)
        {
            if (!existentes.TryGetValue(seccion.Slug, out var nodoSeccion))
            {
                nodoSeccion = Nodo(MenuNodeKind.Section, seccion.Nombre, seccion.Icono,
                    seccion.Slug, null, null, ordenSeccion);
            }
            ordenSeccion++;

            var ordenHijo = 0;
            foreach (var grupo in seccion.Grupos)
            {
                var nodoGrupo = Nodo(MenuNodeKind.Subgroup, grupo.Nombre, grupo.Icono,
                    grupo.Slug, grupo.CodigoRf, nodoSeccion, ordenHijo++);

                var ordenItem = 0;
                foreach (var item in grupo.Items)
                {
                    Nodo(MenuNodeKind.Item, item.Nombre, null, item.Ruta, item.CodigoRf, nodoGrupo, ordenItem++);
                }
            }

            // Items sueltos colgados directamente de la seccion (ej. SISTEMA).
            foreach (var item in seccion.Items)
            {
                Nodo(MenuNodeKind.Item, item.Nombre, null, item.Ruta, item.CodigoRf, nodoSeccion, ordenHijo++);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
