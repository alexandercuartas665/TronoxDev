using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Organization;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Integration.Tests;

/// <summary>
/// Estructura organizacional como ARBOL UNICO con clasificador (RQ01 - RF03/RF04, ADR-003)
/// contra PostgreSQL real (Testcontainers). Cubre lo que necesita base de datos:
///
/// 1. jerarquia de varios niveles (profundidad ilimitada);
/// 2. codigo unico ENTRE HERMANOS pero repetible bajo padres distintos;
/// 3. fondo_id OBLIGATORIO solo en nodos Dependencia;
/// 4. no archivar un nodo con descendientes activos (nunca hay borrado fisico);
/// 5. deteccion de ciclo sobre un arbol YA CORRUPTO, sin colgarse;
/// 6. resolucion de dependencia subiendo desde un Cargo anidado;
/// 7. el caso FAIL-CLOSED del Cargo sin Dependencia por encima;
/// 8. mover un Cargo reporta cuantos usuarios quedan afectados.
///
/// Las reglas puras se testean sin base de datos en Tronox.Application.Tests
/// (OrgStructureRulesTests, OrgDependenciaResolverTests, OrgUnitTreeTests).
/// </summary>
public abstract class EstructuraOrganizacionalTestsBase
{
    private readonly TenantIsolationDbFixture _fixture;

    protected EstructuraOrganizacionalTestsBase(TenantIsolationDbFixture fixture) => _fixture = fixture;

    private static readonly DateOnly Desde = new(2024, 1, 1);
    private static readonly long Actor = TestIds.Next();

    // ================= 1. Jerarquia de varios niveles =================

    [Fact]
    public async Task Arbol_SoportaJerarquiaDeVariosNiveles()
    {
        var seed = await SeedTenantAsync("Org Niveles");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        // Cuatro niveles de Dependencia + un Cargo + un Funcionario: 6 de profundidad.
        var n1 = Ok(await svc.CreateAsync(Dependencia("Ministerio", "MIN", fondo), Actor));
        var n2 = Ok(await svc.CreateAsync(Dependencia("Viceministerio", "VIC", fondo, n1.Id), Actor));
        var n3 = Ok(await svc.CreateAsync(Dependencia("Direccion", "DIR", fondo, n2.Id), Actor));
        var n4 = Ok(await svc.CreateAsync(Dependencia("Oficina", "OFI", fondo, n3.Id), Actor));
        var cargo = Ok(await svc.CreateAsync(Cargo("Jefe de Oficina", n4.Id, NivelJerarquico.Directivo), Actor));
        var funcionario = Ok(await svc.CreateAsync(new SaveOrgUnitRequest(
            "Ana Gomez", OrgUnitClassifier.Funcionario, ParentId: cargo.Id,
            TenantUserId: seed.TenantUserId), Actor));

        var tree = await svc.GetTreeAsync();
        var raiz = Assert.Single(tree);
        Assert.Equal("Ministerio", raiz.Name);
        var nivel2 = Assert.Single(raiz.Children);
        var nivel3 = Assert.Single(nivel2.Children);
        var nivel4 = Assert.Single(nivel3.Children);
        var nivel5 = Assert.Single(nivel4.Children);
        var nivel6 = Assert.Single(nivel5.Children);
        Assert.Equal(OrgUnitClassifier.Cargo, nivel5.Classifier);
        Assert.Equal(NivelJerarquico.Directivo, nivel5.NivelJerarquico);
        Assert.Equal(funcionario.Id, nivel6.Id);
        Assert.Equal(OrgUnitClassifier.Funcionario, nivel6.Classifier);

        // El fondo solo viaja en los nodos Dependencia.
        Assert.Equal(fondo, nivel4.FondoId);
        Assert.Null(nivel5.FondoId);
    }

    // ================= 2. Codigo unico entre hermanos, NO global =================

    [Fact]
    public async Task Codigo_EsUnicoEntreHermanos_PeroSeRepiteBajoPadresDistintos()
    {
        var seed = await SeedTenantAsync("Org Codigos");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var padreA = Ok(await svc.CreateAsync(Dependencia("Secretaria A", "SA", fondo), Actor));
        var padreB = Ok(await svc.CreateAsync(Dependencia("Secretaria B", "SB", fondo), Actor));

        Assert.True((await svc.CreateAsync(Dependencia("Juridica de A", "JUR", fondo, padreA.Id), Actor)).IsOk);

        // MISMO codigo bajo el MISMO padre: conflicto.
        var duplicado = await svc.CreateAsync(Dependencia("Otra de A", "JUR", fondo, padreA.Id), Actor);
        Assert.Equal(OrgServiceStatus.Conflict, duplicado.Status);
        Assert.Contains("JUR", duplicado.Error!);

        // MISMO codigo bajo OTRO padre: permitido, la unicidad es entre hermanos, no global.
        var enOtroPadre = await svc.CreateAsync(Dependencia("Juridica de B", "JUR", fondo, padreB.Id), Actor);
        Assert.True(enOtroPadre.IsOk, enOtroPadre.Error);

        // Y en las raices tambien se comprueba (dos raices no pueden compartir codigo).
        var raizDuplicada = await svc.CreateAsync(Dependencia("Secretaria A bis", "SA", fondo), Actor);
        Assert.Equal(OrgServiceStatus.Conflict, raizDuplicada.Status);
    }

    [Fact]
    public async Task Codigo_NoChocaEntreTenantsDistintos()
    {
        var a = await SeedTenantAsync("Org Codigo A");
        var b = await SeedTenantAsync("Org Codigo B");
        var fondoA = await SeedFondoAsync(a.TenantId, "F01");
        var fondoB = await SeedFondoAsync(b.TenantId, "F01");

        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            Assert.True((await NewService(ctxA, a).CreateAsync(
                Dependencia("Direccion de A", "DG", fondoA), Actor)).IsOk);
        }
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            // Unicidad DENTRO del tenant (DAT-01): el mismo codigo en otro tenant no colisiona.
            var enB = await NewService(ctxB, b).CreateAsync(Dependencia("Direccion de B", "DG", fondoB), Actor);
            Assert.True(enB.IsOk, enB.Error);
        }
    }

    // ================= 3. fondo_id obligatorio SOLO en Dependencia =================

    [Fact]
    public async Task FondoId_EsObligatorioSoloEnDependencia()
    {
        var seed = await SeedTenantAsync("Org Fondo");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        // Dependencia SIN fondo: invalida.
        var sinFondo = await svc.CreateAsync(new SaveOrgUnitRequest(
            "Direccion sin fondo", OrgUnitClassifier.Dependencia, Codigo: "DSF", VigenteDesde: Desde), Actor);
        Assert.Equal(OrgServiceStatus.Invalid, sinFondo.Status);
        Assert.Contains("fondo", sinFondo.Error!, StringComparison.OrdinalIgnoreCase);

        // Dependencia con fondo INEXISTENTE: NotFound (no se acepta una FK colgante).
        var fondoFantasma = await svc.CreateAsync(
            Dependencia("Direccion fantasma", "DFA", TestIds.Next()), Actor);
        Assert.Equal(OrgServiceStatus.NotFound, fondoFantasma.Status);

        var dep = Ok(await svc.CreateAsync(Dependencia("Direccion", "DIR", fondo), Actor));

        // Cargo SIN fondo: perfectamente valido.
        var cargo = await svc.CreateAsync(Cargo("Profesional", dep.Id, NivelJerarquico.Profesional), Actor);
        Assert.True(cargo.IsOk, cargo.Error);
        Assert.Null(cargo.Value!.FondoId);

        // Y si alguien manda fondo en un Cargo, se IGNORA (se persiste null), no se guarda.
        var cargoConFondo = Ok(await svc.CreateAsync(
            Cargo("Tecnico", dep.Id, NivelJerarquico.Tecnico) with { FondoId = fondo }, Actor));
        Assert.Null(cargoConFondo.FondoId);
        Assert.Null(await ctx.OrgUnits.Where(u => u.Id == cargoConFondo.Id).Select(u => u.FondoId).FirstAsync());

        // Cargo sin nivel jerarquico: invalido (la otra cara de la regla por clasificador).
        var cargoSinNivel = await svc.CreateAsync(new SaveOrgUnitRequest(
            "Cargo sin nivel", OrgUnitClassifier.Cargo, ParentId: dep.Id), Actor);
        Assert.Equal(OrgServiceStatus.Invalid, cargoSinNivel.Status);
    }

    // ================= 4. No archivar con descendientes activos =================

    [Fact]
    public async Task NoSeArchivaUnNodoConDescendientesActivos_YNuncaHayBorradoFisico()
    {
        var seed = await SeedTenantAsync("Org Archivar");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var padre = Ok(await svc.CreateAsync(Dependencia("Secretaria", "SEC", fondo), Actor));
        var hija = Ok(await svc.CreateAsync(Dependencia("Grupo interno", "GIT", fondo, padre.Id), Actor));

        var bloqueado = await svc.SetArchivedAsync(padre.Id, archived: true, Actor);
        Assert.Equal(OrgServiceStatus.Invalid, bloqueado.Status);

        // Archivando primero la hoja, el padre ya se puede archivar.
        Assert.True((await svc.SetArchivedAsync(hija.Id, archived: true, Actor)).IsOk);
        var ahoraSi = await svc.SetArchivedAsync(padre.Id, archived: true, Actor);
        Assert.True(ahoraSi.IsOk, ahoraSi.Error);

        // Invariante 8: siguen ambos en base, archivados. Nunca DELETE.
        Assert.Equal(2, await ctx.OrgUnits.CountAsync());
        Assert.Equal(2, await ctx.OrgUnits.CountAsync(u => u.IsArchived));
        Assert.Empty(await svc.GetTreeAsync());
        // Con includeArchived siguen ahi, y con su jerarquia intacta (una raiz con una hija).
        var archivadas = await svc.GetTreeAsync(includeArchived: true);
        var raizArchivada = Assert.Single(archivadas);
        Assert.Equal(padre.Id, raizArchivada.Id);
        Assert.Equal(hija.Id, Assert.Single(raizArchivada.Children).Id);
    }

    [Fact]
    public async Task RaicesTolerantes_UnNodoConPadreArchivadoSigueVisible()
    {
        var seed = await SeedTenantAsync("Org Huerfanos");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var padre = Ok(await svc.CreateAsync(Dependencia("Padre", "PAD", fondo), Actor));
        var hija = Ok(await svc.CreateAsync(Dependencia("Hija", "HIJ", fondo, padre.Id), Actor));

        // Se archiva el padre por la via directa (dato ya existente en produccion), dejando a
        // la hija activa con un padre fuera del conjunto visible.
        var entidad = await ctx.OrgUnits.FirstAsync(u => u.Id == padre.Id);
        entidad.IsArchived = true;
        await ctx.SaveChangesAsync();

        // La hija NO desaparece: se trata como raiz para que ningun nodo visible quede invisible.
        var tree = await svc.GetTreeAsync();
        var raiz = Assert.Single(tree);
        Assert.Equal(hija.Id, raiz.Id);
        Assert.Equal(padre.Id, raiz.ParentId);
    }

    // ================= 5. Ciclo sobre un arbol YA CORRUPTO =================

    [Fact]
    public async Task ArbolYaCorrupto_ReportaCiclo_SinColgarse()
    {
        var seed = await SeedTenantAsync("Org Corrupto");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var a = Ok(await svc.CreateAsync(Dependencia("A", "A", fondo), Actor));
        var b = Ok(await svc.CreateAsync(Dependencia("B", "B", fondo, a.Id), Actor));
        var suelta = Ok(await svc.CreateAsync(Dependencia("Suelta", "S", fondo), Actor));

        // Se corrompe el arbol POR DEBAJO del servicio (como lo haria un dato migrado o un
        // UPDATE manual): A -> B -> A. El listado y la validacion deben sobrevivir a esto.
        var entidadA = await ctx.OrgUnits.FirstAsync(u => u.Id == a.Id);
        entidadA.ParentId = b.Id;
        await ctx.SaveChangesAsync();

        // Mover un nodo sano bajo la rama corrupta se reporta como CICLO (fail-closed), en vez
        // de colgar el proceso caminando ancestros para siempre.
        var mover = await svc.UpdateAsync(suelta.Id, Dependencia("Suelta", "S", fondo, a.Id), Actor);
        Assert.Equal(OrgServiceStatus.Invalid, mover.Status);
        Assert.Contains("ciclo", mover.Error!, StringComparison.OrdinalIgnoreCase);

        // Y el arbol se sigue pudiendo listar: con A y B mutuamente colgados ninguno es raiz
        // por ParentId, pero ambos siguen dentro del conjunto visible y "Suelta" se ve igual.
        var tree = await svc.GetTreeAsync();
        Assert.Contains(tree, n => n.Id == suelta.Id);
    }

    // ================= 6 y 7. Resolver de dependencia (Addendum) =================

    [Fact]
    public async Task ResuelveLaDependenciaSubiendoDesdeUnCargoAnidado()
    {
        var seed = await SeedTenantAsync("Org Resolver");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var ministerio = Ok(await svc.CreateAsync(Dependencia("Ministerio", "MIN", fondo), Actor));
        var direccion = Ok(await svc.CreateAsync(Dependencia("Direccion", "DIR", fondo, ministerio.Id), Actor));
        var oficina = Ok(await svc.CreateAsync(Dependencia("Oficina", "OFI", fondo, direccion.Id), Actor));
        var cargo = Ok(await svc.CreateAsync(Cargo("Profesional", oficina.Id, NivelJerarquico.Profesional), Actor));

        // La dependencia es la MAS CERCANA hacia arriba, no la raiz del arbol.
        Assert.Equal(oficina.Id, await svc.ResolveDependenciaAsync(cargo.Id));

        // Un usuario anclado a ese Cargo hereda esa dependencia SIN que se le almacene.
        await AnclarCargoAsync(seed.TenantId, seed.TenantUserId, cargo.Id);
        Assert.Equal(oficina.Id, await svc.ResolveDependenciaForUserAsync(seed.TenantUserId));
    }

    [Fact]
    public async Task CargoSinDependenciaEncima_EsFailClosed_SinVisibilidad()
    {
        var seed = await SeedTenantAsync("Org FailClosed");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        // Cargo colgado directamente de la RAIZ: no hay ninguna Dependencia por encima.
        var cargoHuerfano = Ok(await svc.CreateAsync(
            Cargo("Asesor sin area", parentId: null, NivelJerarquico.Asesor), Actor));

        // SIN dependencia = SIN visibilidad documental. Nunca visibilidad total.
        Assert.Null(await svc.ResolveDependenciaAsync(cargoHuerfano.Id));

        await AnclarCargoAsync(seed.TenantId, seed.TenantUserId, cargoHuerfano.Id);
        Assert.Null(await svc.ResolveDependenciaForUserAsync(seed.TenantUserId));

        // Un usuario SIN cargo anclado tambien resuelve fail-closed.
        await AnclarCargoAsync(seed.TenantId, seed.TenantUserId, null);
        Assert.Null(await svc.ResolveDependenciaForUserAsync(seed.TenantUserId));
    }

    // ================= 8. Mover un Cargo reporta usuarios afectados =================

    [Fact]
    public async Task MoverUnCargo_ReportaCuantosUsuariosQuedanAfectados()
    {
        var seed = await SeedTenantAsync("Org Mover");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");
        var otroUsuario = await SeedExtraUserAsync(seed.TenantId, "segundo");
        var terceroUsuario = await SeedExtraUserAsync(seed.TenantId, "tercero");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var origen = Ok(await svc.CreateAsync(Dependencia("Origen", "ORI", fondo), Actor));
        var destino = Ok(await svc.CreateAsync(Dependencia("Destino", "DES", fondo), Actor));
        var cargo = Ok(await svc.CreateAsync(Cargo("Profesional", origen.Id, NivelJerarquico.Profesional), Actor));

        // Dos usuarios anclados al cargo que se mueve; el tercero cuelga de otro sitio y no
        // debe contarse.
        await AnclarCargoAsync(seed.TenantId, seed.TenantUserId, cargo.Id);
        await AnclarCargoAsync(seed.TenantId, otroUsuario, cargo.Id);
        var cargoAjeno = Ok(await svc.CreateAsync(Cargo("Tecnico", destino.Id, NivelJerarquico.Tecnico), Actor));
        await AnclarCargoAsync(seed.TenantId, terceroUsuario, cargoAjeno.Id);

        // Antes de mover, la UI puede preguntar a cuantos afecta.
        Assert.Equal(2, await svc.CountAffectedUsersAsync(cargo.Id));

        var movido = await svc.MoveCargoAsync(cargo.Id, destino.Id, Actor, "Reestructuracion 2026");
        Assert.True(movido.IsOk, movido.Error);
        var r = movido.Value!;
        Assert.Equal(2, r.AffectedUserCount);
        Assert.Equal(origen.Id, r.PreviousDependenciaId);
        Assert.Equal(destino.Id, r.NewDependenciaId);
        Assert.Equal(destino.Id, r.NewParentId);

        // La visibilidad documental de los ocupantes cambio SIN editar a esos usuarios.
        Assert.Equal(destino.Id, await svc.ResolveDependenciaForUserAsync(seed.TenantUserId));
        Assert.Equal(destino.Id, await svc.ResolveDependenciaForUserAsync(otroUsuario));
        Assert.Equal(destino.Id, await svc.ResolveDependenciaForUserAsync(terceroUsuario));
    }

    [Fact]
    public async Task MoverUnCargoALaRaiz_DejaASusOcupantesSinDependencia()
    {
        var seed = await SeedTenantAsync("Org Mover Raiz");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var dep = Ok(await svc.CreateAsync(Dependencia("Direccion", "DIR", fondo), Actor));
        var cargo = Ok(await svc.CreateAsync(Cargo("Asesor", dep.Id, NivelJerarquico.Asesor), Actor));
        await AnclarCargoAsync(seed.TenantId, seed.TenantUserId, cargo.Id);

        var movido = await svc.MoveCargoAsync(cargo.Id, newParentId: null, Actor);
        Assert.True(movido.IsOk, movido.Error);
        Assert.Equal(1, movido.Value!.AffectedUserCount);
        Assert.Equal(dep.Id, movido.Value.PreviousDependenciaId);
        // FAIL-CLOSED tras el movimiento: el ocupante se queda SIN area documental.
        Assert.Null(movido.Value.NewDependenciaId);
        Assert.Null(await svc.ResolveDependenciaForUserAsync(seed.TenantUserId));
    }

    [Fact]
    public async Task MoverUnCargo_ArrastraSuSubarbol_YRechazaNodosQueNoSonCargo()
    {
        var seed = await SeedTenantAsync("Org Mover Subarbol");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");
        var ocupante = await SeedExtraUserAsync(seed.TenantId, "ocupante");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var origen = Ok(await svc.CreateAsync(Dependencia("Origen", "ORI", fondo), Actor));
        var destino = Ok(await svc.CreateAsync(Dependencia("Destino", "DES", fondo), Actor));
        var cargo = Ok(await svc.CreateAsync(Cargo("Jefe", origen.Id, NivelJerarquico.Directivo), Actor));
        // Funcionario colgando del cargo: se mueve con el.
        Ok(await svc.CreateAsync(new SaveOrgUnitRequest(
            "Ana Gomez", OrgUnitClassifier.Funcionario, ParentId: cargo.Id,
            TenantUserId: ocupante), Actor));
        await AnclarCargoAsync(seed.TenantId, ocupante, cargo.Id);

        Assert.True((await svc.MoveCargoAsync(cargo.Id, destino.Id, Actor)).IsOk);
        Assert.Equal(destino.Id, await svc.ResolveDependenciaForUserAsync(ocupante));

        // La operacion es SOLO para nodos Cargo: una Dependencia se mueve por la edicion normal.
        var noEsCargo = await svc.MoveCargoAsync(origen.Id, destino.Id, Actor);
        Assert.Equal(OrgServiceStatus.Invalid, noEsCargo.Status);

        // Y sigue rechazando ciclos.
        var cicloDeCargo = await svc.MoveCargoAsync(cargo.Id, cargo.Id, Actor);
        Assert.Equal(OrgServiceStatus.Invalid, cicloDeCargo.Status);
    }

    // ================= Sucesora (fusiones / reestructuraciones, RF03) =================

    [Fact]
    public async Task Sucesora_ApuntaAOtraDependencia_YNoASiMisma()
    {
        var seed = await SeedTenantAsync("Org Sucesora");
        var fondo = await SeedFondoAsync(seed.TenantId, "F01");

        await using var ctx = _fixture.CreateContext(seed.TenantId);
        var svc = NewService(ctx, seed);

        var nueva = Ok(await svc.CreateAsync(Dependencia("Direccion nueva", "NUE", fondo), Actor));
        var vieja = Ok(await svc.CreateAsync(Dependencia("Direccion vieja", "VIE", fondo), Actor));

        // Fusion: la vieja se cierra con fecha y apunta a su sucesora.
        var fusionada = await svc.UpdateAsync(vieja.Id, new SaveOrgUnitRequest(
            "Direccion vieja", OrgUnitClassifier.Dependencia, FondoId: fondo, Codigo: "VIE",
            VigenteDesde: Desde, VigenteHasta: new DateOnly(2026, 6, 30), SucesoraId: nueva.Id), Actor);
        Assert.True(fusionada.IsOk, fusionada.Error);
        Assert.Equal(nueva.Id, fusionada.Value!.SucesoraId);
        Assert.Equal(new DateOnly(2026, 6, 30), fusionada.Value.VigenteHasta);

        // Una dependencia no puede sucederse a si misma.
        var autoSucesion = await svc.UpdateAsync(vieja.Id, new SaveOrgUnitRequest(
            "Direccion vieja", OrgUnitClassifier.Dependencia, FondoId: fondo, Codigo: "VIE",
            VigenteDesde: Desde, SucesoraId: vieja.Id), Actor);
        Assert.Equal(OrgServiceStatus.Invalid, autoSucesion.Status);
    }

    // ================= Aislamiento por tenant =================

    [Fact]
    public async Task ResolverDeDependencia_EsFailClosedCrossTenant()
    {
        var a = await SeedTenantAsync("Org Cross A");
        var b = await SeedTenantAsync("Org Cross B");
        var fondoA = await SeedFondoAsync(a.TenantId, "F01");

        long cargoDeA;
        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            var svcA = NewService(ctxA, a);
            var dep = Ok(await svcA.CreateAsync(Dependencia("Direccion de A", "DG", fondoA), Actor));
            cargoDeA = Ok(await svcA.CreateAsync(Cargo("Jefe", dep.Id, NivelJerarquico.Directivo), Actor)).Id;
            Assert.Equal(dep.Id, await svcA.ResolveDependenciaAsync(cargoDeA));
        }

        // El tenant B no ve el arbol de A: resolver un nodo ajeno da null, no la dependencia.
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            Assert.Null(await NewService(ctxB, b).ResolveDependenciaAsync(cargoDeA));
        }
    }

    // ================= Helpers =================

    private static OrgUnitDto Ok(OrgResult<OrgUnitDto> result)
    {
        Assert.True(result.IsOk, result.Error);
        return result.Value!;
    }

    private static OrgUnitService NewService(IApplicationDbContext ctx, SeedData seed)
        => new(ctx, new TestTenantContext(seed.TenantId, seed.PlatformUserId), new NoOpAuditWriter());

    private static SaveOrgUnitRequest Dependencia(
        string nombre, string codigo, long fondoId, long? parentId = null)
        => new(nombre, OrgUnitClassifier.Dependencia, ParentId: parentId,
            FondoId: fondoId, Codigo: codigo, VigenteDesde: Desde);

    private static SaveOrgUnitRequest Cargo(string nombre, long? parentId, NivelJerarquico nivel)
        => new(nombre, OrgUnitClassifier.Cargo, ParentId: parentId, NivelJerarquico: nivel);

    /// <summary>
    /// Ancla el usuario a UN solo nodo, su Cargo (ADR-003, Addendum). La dependencia NO se
    /// escribe en el usuario: se deriva.
    /// </summary>
    private async Task AnclarCargoAsync(long tenantId, long tenantUserId, long? cargoOrgUnitId)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var user = await ctx.TenantUsers.FirstAsync(tu => tu.Id == tenantUserId);
        user.CargoOrgUnitId = cargoOrgUnitId;
        await ctx.SaveChangesAsync();
    }

    private async Task<long> SeedFondoAsync(long tenantId, string codigo)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var fondo = new Fondo
        {
            TenantId = tenantId,
            CodigoFondo = codigo,
            NombreFondo = $"Fondo {codigo}",
            FechaApertura = new DateOnly(2020, 1, 1)
        };
        ctx.Fondos.Add(fondo);
        await ctx.SaveChangesAsync();
        return fondo.Id;
    }

    private async Task<SeedData> SeedTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var platformUser = new PlatformUser
            {
                Email = $"user-{tenantId}@estructura.test",
                EmailVerified = true,
                Status = PlatformUserStatus.Active
            };
            ctx.PlatformUsers.Add(platformUser);
            var tenantUser = new TenantUser
            {
                TenantId = tenantId,
                PlatformUser = platformUser,
                Email = platformUser.Email
            };
            ctx.TenantUsers.Add(tenantUser);
            await ctx.SaveChangesAsync();
            return new SeedData(tenantId, tenantUser.Id, platformUser.Id);
        }
    }

    private async Task<long> SeedExtraUserAsync(long tenantId, string slug)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var platformUser = new PlatformUser
        {
            Email = $"{slug}-{tenantId}@estructura.test",
            EmailVerified = true,
            Status = PlatformUserStatus.Active
        };
        ctx.PlatformUsers.Add(platformUser);
        var tenantUser = new TenantUser
        {
            TenantId = tenantId,
            PlatformUser = platformUser,
            Email = platformUser.Email
        };
        ctx.TenantUsers.Add(tenantUser);
        await ctx.SaveChangesAsync();
        return tenantUser.Id;
    }

    private sealed record SeedData(long TenantId, long TenantUserId, long PlatformUserId);

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }

    private sealed class NoOpAuditWriter : IAuditWriter
    {
        public void Write(long actorUserId, string actionName, string entityName, long? entityId,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human)
        {
        }

        public void Write(long actorUserId, string actionName, string entityName,
            Tronox.Domain.Common.BaseEntity entity,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human)
        {
        }
    }
}

/// <summary>Motor PostgreSQL (contenedor efimero postgres:16-alpine, Testcontainers).</summary>
public sealed class EstructuraOrganizacionalTests_Postgres
    : EstructuraOrganizacionalTestsBase, IClassFixture<PostgresTenantIsolationFixture>
{
    public EstructuraOrganizacionalTests_Postgres(PostgresTenantIsolationFixture fixture) : base(fixture) { }
}
