using Tronox.Application.MenuConfig;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS del arbol canonico del menu (MenuCatalogo), sin base de datos.
///
/// Cubren el criterio de RF09 5.9.5.3 (validacion de anidamiento) sobre la SEMILLA misma y la
/// fidelidad con el PROTOTIPO unificado (Prototipo/assets/js/tronox-shell.js), que es la fuente de
/// verdad de la navegacion del tenant: mismos grupos, mismos modulos (incluidas Capa IA y Console) y
/// mismas pantallas.
/// </summary>
public sealed class MenuCatalogoTests
{
    [Fact]
    public void Catalogo_TieneLosSieteGruposDelPrototipo()
    {
        Assert.Equal(
            new[]
            {
                "configuracion", "gestion-documental", "gestion-tramite",
                "ciudadano-terceros", "procesos-especializados", "inteligencia-analitica", "plataforma"
            },
            MenuCatalogo.Secciones.Select(s => s.Slug));
    }

    /// <summary>
    /// EL test de RF09 5.9.5.3 sobre la semilla: cada nodo del arbol canonico se valida contra las
    /// reglas de anidamiento con el Kind de su padre real. Incluye las sub-secciones anidadas
    /// (Subgroup dentro de Subgroup) que introdujo la alineacion con el prototipo.
    /// </summary>
    [Fact]
    public void Catalogo_CumpleLasReglasDeAnidamientoDeRF09()
    {
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
                ValidarGrupo(grupo, MenuNodeKind.Section);
            }
        }
    }

    private static void ValidarGrupo(MenuCatalogo.GrupoSemilla grupo, MenuNodeKind padre)
    {
        Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.Subgroup, padre));
        // Un grupo debe traer algo: items propios o sub-grupos.
        Assert.True(grupo.Items.Count > 0 || (grupo.Subgrupos?.Count ?? 0) > 0,
            $"Grupo vacio: {grupo.Nombre}");

        foreach (var item in grupo.Items)
        {
            Assert.Null(MenuNodeKindRules.Validate(MenuNodeKind.Item, MenuNodeKind.Subgroup));
            Assert.False(string.IsNullOrWhiteSpace(item.Ruta), $"Item sin ruta: {item.Nombre}");
        }

        foreach (var sub in grupo.Subgrupos ?? [])
        {
            ValidarGrupo(sub, MenuNodeKind.Subgroup);
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
            .Concat(MenuCatalogo.TodosLosGrupos().Select(g => g.Slug))
            .ToList();

        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// El slug de un grupo/seccion no puede colisionar con la ruta de un item: comparten el espacio
    /// de claves de IconosPorRuta/NombresPorRuta y del reconciliador de vistas.
    /// </summary>
    [Fact]
    public void Catalogo_LasClavesDeGrupoYDeItemNoColisionan()
    {
        var slugs = MenuCatalogo.Secciones.Select(s => s.Slug)
            .Concat(MenuCatalogo.TodosLosGrupos().Select(g => g.Slug))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var ruta in MenuCatalogo.RutasDeItem)
        {
            Assert.DoesNotContain(ruta, slugs);
        }
    }

    /// <summary>
    /// El prototipo trae la Capa IA Transversal (RQ16) como MODULO propio bajo Inteligencia y
    /// Analitica, no como un item suelto de configuracion. Se reproduce tal cual.
    /// </summary>
    [Fact]
    public void RQ16_EsUnModuloPropioDeInteligenciaYAnalitica()
    {
        var seccion = MenuCatalogo.Secciones.Single(s => s.Slug == "inteligencia-analitica");
        var capaIa = Assert.Single(seccion.Grupos, g => g.CodigoRf == "RQ16");
        Assert.Equal("req016", capaIa.Slug);
        Assert.NotEmpty(capaIa.Items);
    }

    /// <summary>
    /// El prototipo incluye la TRONOX Console (RQ14) bajo el grupo Plataforma: se reproduce (decision
    /// del cliente de parquedad literal con el prototipo).
    /// </summary>
    [Fact]
    public void Catalogo_IncluyeConsoleBajoPlataforma()
    {
        var plataforma = MenuCatalogo.Secciones.Single(s => s.Slug == "plataforma");
        var console = Assert.Single(plataforma.Grupos, g => g.CodigoRf == "RQ14");
        Assert.Equal("req014", console.Slug);
        Assert.NotEmpty(console.Items);
    }

    /// <summary>REQ001 es un unico modulo con tres sub-secciones anidadas, como en el prototipo.</summary>
    [Fact]
    public void REQ001_EsUnModuloConTresSubSecciones()
    {
        var config = MenuCatalogo.Secciones.Single(s => s.Slug == "configuracion");
        var req001 = Assert.Single(config.Grupos, g => g.Slug == "req001");

        Assert.Empty(req001.Items); // no cuelga items directos: todo va en sub-secciones.
        Assert.Equal(
            new[] { "General", "Organizacional", "Sistema" },
            (req001.Subgrupos ?? []).Select(g => g.Nombre));
    }

    /// <summary>
    /// El estado del modulo (punto de color del sidebar) mapea el `estado` del prototipo, y NO todos
    /// los modulos quedan en Ready: hay listos, en desarrollo y especificados.
    /// </summary>
    [Fact]
    public void Catalogo_LosModulosTienenEstadosVariados()
    {
        var modulos = MenuCatalogo.Secciones.SelectMany(s => s.Grupos).ToList();
        Assert.Contains(modulos, g => g.Estado == MenuNodeState.Ready);          // listo
        Assert.Contains(modulos, g => g.Estado == MenuNodeState.InDevelopment);  // prototipo (req005)
        Assert.Contains(modulos, g => g.Estado == MenuNodeState.Disabled);       // spec
    }

    /// <summary>El arbol trae modulos de verdad: es la precondicion de la matriz de permisos.</summary>
    [Fact]
    public void Catalogo_TraeModulosSuficientesParaLaMatriz()
    {
        Assert.True(MenuCatalogo.RutasDeItem.Count > 50,
            $"El arbol canonico solo trae {MenuCatalogo.RutasDeItem.Count} items.");

        Assert.Equal(
            1 + MenuCatalogo.Secciones.Count
              + MenuCatalogo.TotalSubgrupos
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

    /// <summary>NINGUN nodo del arbol canonico puede quedarse sin clave de icono valida (bi-*).</summary>
    [Fact]
    public void Catalogo_NingunNodoSeQuedaSinIcono()
    {
        var sinIcono = new List<string>();

        void Revisar(string que, string ruta, string? icono)
        {
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
        }

        foreach (var grupo in MenuCatalogo.TodosLosGrupos())
        {
            Revisar("Grupo", grupo.Slug, grupo.Icono);
        }

        foreach (var item in MenuCatalogo.TodosLosItems())
        {
            Revisar("Item", item.Ruta, item.Icono);
        }

        Assert.True(sinIcono.Count == 0,
            "Nodos del catalogo canonico sin clave de icono valida:" + Environment.NewLine
            + string.Join(Environment.NewLine, sinIcono));
    }

    /// <summary>El mapa Ruta -> icono cubre el arbol ENTERO (lo usa BackfillIconKeysAsync).</summary>
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
