using Tronox.Application.MenuConfig;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Infrastructure.Persistence;

/// <summary>
/// Duenio del arbol canonico del menu de TRONOX y unico punto de siembra (RF09 5.9.4).
///
/// Se registra como IMenuProvisioningService y lo invocan LAS DOS rutas de alta de tenant
/// (TenantAdminService y OnboardingService), no un seeder de demo. Esa es la correccion al
/// riesgo documentado en ADR-001: en el sistema hermano el menu solo se sembraba para el
/// tenant demo, asi que los clientes creados desde el panel de plataforma nacian sin ninguna
/// vista y sus usuarios no veian nada.
///
/// La operacion es IDEMPOTENTE: si el tenant ya tiene al menos una vista, no hace nada.
///
/// ALCANCE ACTUAL (Fase 0): siembra la vista predeterminada con el acceso Inicio y las 7
/// secciones canonicas de MAPA_MENU_SISTEMA_TRONOX. Los 17 modulos y sus pantallas se agregan
/// en el entregable 1.5, cuando exista la matriz de permisos que filtra el arbol.
/// </summary>
public sealed class MenuProvisioningService : IMenuProvisioningService
{
    private readonly TronoxDbContext _db;

    public MenuProvisioningService(TronoxDbContext db) => _db = db;

    /// <summary>Secciones canonicas de primer nivel: (slug, etiqueta, icono Bootstrap Icons).</summary>
    private static readonly (string Route, string Name, string Icon)[] Secciones =
    [
        ("configuracion",     "CONFIGURACION",              "bi-sliders"),
        ("gestion-documental", "GESTION DOCUMENTAL",        "bi-folder"),
        ("correspondencia",   "CORRESPONDENCIA Y TRAMITE",  "bi-envelope"),
        ("ciudadano-terceros", "CIUDADANO Y TERCEROS",      "bi-people"),
        ("procesos",          "PROCESOS ESPECIALIZADOS",    "bi-briefcase"),
        ("analitica",         "INTELIGENCIA Y ANALITICA",   "bi-graph-up"),
        ("sistema",           "SISTEMA",                    "bi-shield-lock")
    ];

    public async Task EnsureDefaultMenuAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Se consulta IGNORANDO el filtro global: el alta corre bajo el contexto de la plataforma,
        // no bajo el del tenant que se esta creando, asi que el filtro no aplicaria aqui.
        var yaTieneVista = await _db.MenuViews
            .IgnoreQueryFilters()
            .AnyAsync(v => v.TenantId == tenantId, cancellationToken);
        if (yaTieneVista)
        {
            return;
        }

        var vista = new MenuView
        {
            TenantId = tenantId,
            Name = "Completa",
            Description = "Vista predeterminada con el arbol canonico de TRONOX.",
            IsDefault = true,
            SortOrder = 0
        };
        _db.MenuViews.Add(vista);

        _db.MenuNodes.Add(new MenuNode
        {
            TenantId = tenantId,
            MenuViewId = vista.Id,
            Kind = MenuNodeKind.QuickLink,
            Name = "Inicio",
            IconKey = "bi-house",
            Route = "inicio",
            State = MenuNodeState.Ready,
            IsVisible = true,
            SortOrder = 0
        });

        var orden = 1;
        foreach (var (route, name, icon) in Secciones)
        {
            _db.MenuNodes.Add(new MenuNode
            {
                TenantId = tenantId,
                MenuViewId = vista.Id,
                Kind = MenuNodeKind.Section,
                Name = name,
                IconKey = icon,
                Route = route,
                State = MenuNodeState.Ready,
                IsVisible = true,
                SortOrder = orden++
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
