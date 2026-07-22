using Tronox.Application.MenuConfig;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS del arbol canonico del menu (MenuCatalogo), sin base de datos.
///
/// Cubren el criterio de RF09 5.9.5.3 (validacion de anidamiento) sobre la SEMILLA misma: el arbol
/// que se siembra en cada alta de tenant no puede contener una combinacion invalida (una Seccion
/// dentro de un Item, un Subgrupo colgando de un Item...). Y cubren las exclusiones que fija el
/// MAPA del vault: RQ16 sin menu propio, RQ14 y los portales externos fuera del menu interno.
/// </summary>
public sealed class MenuCatalogoTests
{
    [Fact]
    public void Catalogo_TieneLasSieteSeccionesDelMapa()
    {
        Assert.Equal(
            new[]
            {
                "configuracion", "gestion-documental", "correspondencia",
                "ciudadano-terceros", "procesos", "analitica", "sistema"
            },
            MenuCatalogo.Secciones.Select(s => s.Slug));
    }

    /// <summary>
    /// EL test de RF09 5.9.5.3 sobre la semilla: cada nodo del arbol canonico se valida contra las
    /// reglas de anidamiento con el Kind de su padre real.
    /// </summary>
    [Fact]
    public void Catalogo_CumpleLasReglasDeAnidamientoDeRF09()
    {
        // El enlace suelto de primer nivel no tiene padre.
        Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.QuickLink, null));

        foreach (var seccion in MenuCatalogo.Secciones)
        {
            Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.Section, null));

            foreach (var item in seccion.Items)
            {
                Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.Item, MenuNodeKind.Section));
                Assert.False(string.IsNullOrWhiteSpace(item.Ruta), $"Item sin ruta: {item.Nombre}");
            }

            foreach (var grupo in seccion.Grupos)
            {
                Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.Subgroup, MenuNodeKind.Section));
                Assert.NotEmpty(grupo.Items);

                foreach (var item in grupo.Items)
                {
                    Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.Item, MenuNodeKind.Subgroup));
                    Assert.False(string.IsNullOrWhiteSpace(item.Ruta), $"Item sin ruta: {item.Nombre}");
                }
            }
        }
    }

    /// <summary>
    /// La ruta es la LLAVE DE PERMISOS (RF09 5.9.3): dos items con la misma ruta serian el mismo
    /// modulo de la matriz con dos nombres, y el conteo modulos = items dejaria de cuadrar.
    /// </summary>
    [Fact]
    public void Catalogo_NoRepiteRutasDeItem()
    {
        var repetidas = MenuCatalogo.RutasDeItem
            .GroupBy(r => r, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(repetidas);
    }

    [Fact]
    public void Catalogo_NoRepiteSlugsDeSeccionNiDeGrupo()
    {
        var slugs = MenuCatalogo.Secciones
            .Select(s => s.Slug)
            .Concat(MenuCatalogo.Secciones.SelectMany(s => s.Grupos).Select(g => g.Slug))
            .ToList();

        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// El MAPA es explicito: la Capa IA "no tiene menu propio, se inserta en otros modulos". Su
    /// unica presencia es el item de configuracion dentro de RQ01.
    /// </summary>
    [Fact]
    public void RQ16_NoTieneSeccionNiGrupoPropio_SoloElItemDeConfiguracionEnRQ01()
    {
        Assert.DoesNotContain(MenuCatalogo.Secciones.SelectMany(s => s.Grupos), g => g.CodigoRf == "RQ16");

        var itemsIa = MenuCatalogo.Secciones
            .SelectMany(s => s.Grupos)
            .SelectMany(g => g.Items.Select(i => (Grupo: g, Item: i)))
            .Where(x => x.Item.CodigoRf == "RQ16")
            .ToList();

        var unico = Assert.Single(itemsIa);
        Assert.Equal("configuracion-general", unico.Grupo.Slug);
    }

    /// <summary>
    /// RQ14 (TRONOX Console) es una aplicacion de plataforma con identidad propia, no un modulo del
    /// tenant; los portales externos viven fuera del SGDEA interno. Ninguno entra en este arbol.
    /// </summary>
    [Fact]
    public void Catalogo_NoIncluyeConsoleNiPortalesExternos()
    {
        var codigos = MenuCatalogo.Secciones
            .SelectMany(s => s.Grupos)
            .SelectMany(g => new[] { g.CodigoRf }.Concat(g.Items.Select(i => i.CodigoRf)))
            .Concat(MenuCatalogo.Secciones.SelectMany(s => s.Items).Select(i => i.CodigoRf))
            .ToList();

        Assert.DoesNotContain("RQ14", codigos);

        foreach (var ruta in MenuCatalogo.RutasDeItem)
        {
            Assert.DoesNotContain("console", ruta, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verificador", ruta, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sede-electronica", ruta, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// El arbol tiene que traer modulos de verdad: es la precondicion de la matriz de permisos.
    /// Un catalogo con secciones y sin items reproduce el defecto que este entregable corrige
    /// (matriz vacia + fail-closed = tenant inusable).
    /// </summary>
    [Fact]
    public void Catalogo_TraeModulosSuficientesParaLaMatriz()
    {
        Assert.True(MenuCatalogo.RutasDeItem.Count > 50,
            $"El arbol canonico solo trae {MenuCatalogo.RutasDeItem.Count} items.");

        // TotalNodos = 1 quick-link + secciones + grupos + items.
        Assert.Equal(
            1 + MenuCatalogo.Secciones.Count
              + MenuCatalogo.Secciones.Sum(s => s.Grupos.Count)
              + MenuCatalogo.RutasDeItem.Count,
            MenuCatalogo.TotalNodos);
    }

    /// <summary>Las rutas de pantalla aun no construida resuelven en la pagina generica /modulo/{slug}.</summary>
    [Fact]
    public void Catalogo_LasRutasSonSlugsRelativos_SinBarraInicialNiAbsolutas()
    {
        foreach (var ruta in MenuCatalogo.RutasDeItem)
        {
            Assert.False(ruta.StartsWith('/'), $"Ruta absoluta: {ruta}");
            Assert.DoesNotContain("://", ruta, StringComparison.Ordinal);
            Assert.Equal(ruta.Trim(), ruta);
        }
    }

    /// <summary>
    /// NINGUN nodo del arbol canonico puede quedarse sin clave de icono.
    ///
    /// POR QUE ESTE TEST: los 93 items del catalogo nacieron sin icono y en el sidebar todas las
    /// pantallas pintaban el mismo cuadrado generico, mientras el prototipo tiene un icono por
    /// pantalla. Era el mayor delator visual del sistema. El compilador ya obliga a pasar el icono
    /// (ItemSemilla.Icono no es opcional), pero no impide pasar "" ni un valor que no sea una clase
    /// Bootstrap Icons. Esto ultimo es lo que se blinda aqui, para que la proxima pantalla que
    /// alguien anada no vuelva a nacer sin icono.
    /// </summary>
    [Fact]
    public void Catalogo_NingunNodoSeQuedaSinIcono()
    {
        var sinIcono = new List<string>();

        void Revisar(string que, string ruta, string? icono)
        {
            // ADR-001: la clave de icono es una clase Bootstrap Icons (bi-*), nunca un SVG.
            if (string.IsNullOrWhiteSpace(icono)
                || !icono.StartsWith("bi-", StringComparison.Ordinal)
                || icono.Length <= 3
                || icono.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            {
                sinIcono.Add($"{que} '{ruta}' -> '{icono}'");
            }
        }

        Revisar("QuickLink", MenuCatalogo.Inicio.Ruta, MenuCatalogo.IconoInicio);

        foreach (var seccion in MenuCatalogo.Secciones)
        {
            Revisar("Seccion", seccion.Slug, seccion.Icono);

            foreach (var grupo in seccion.Grupos)
            {
                Revisar("Grupo", grupo.Slug, grupo.Icono);
            }
        }

        foreach (var item in MenuCatalogo.TodosLosItems())
        {
            Revisar("Item", item.Ruta, item.Icono);
        }

        Assert.True(sinIcono.Count == 0,
            "Nodos del catalogo canonico sin clave de icono valida:" + Environment.NewLine
            + string.Join(Environment.NewLine, sinIcono));
    }

    /// <summary>
    /// El mapa Ruta -> icono tiene que cubrir el arbol ENTERO: es lo que usa
    /// MenuProvisioningService.BackfillIconKeysAsync para rellenar el icono de los tenants que ya
    /// nacieron sin el. Si una ruta no estuviera en el mapa, ese nodo se quedaria con el cuadrado
    /// generico para siempre.
    /// </summary>
    [Fact]
    public void Catalogo_ElMapaDeIconosCubreTodoElArbol()
    {
        Assert.Equal(MenuCatalogo.TotalNodos, MenuCatalogo.IconosPorRuta.Count);

        foreach (var ruta in MenuCatalogo.RutasDeItem)
        {
            Assert.True(MenuCatalogo.IconosPorRuta.ContainsKey(ruta), $"Ruta sin icono en el mapa: {ruta}");
        }
    }
}
