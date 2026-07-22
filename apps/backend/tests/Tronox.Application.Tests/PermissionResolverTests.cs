using Tronox.Application.Roles;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS (sin base de datos) de la resolucion de permisos efectivos (RQ01 - RF05).
///
/// El nucleo de este archivo es el INVARIANTE 10 (FAIL-CLOSED): un usuario sin roles, con roles
/// vencidos o con roles inactivos resuelve a SIN PERMISOS, nunca a acceso total. El backbone
/// (ECOREX) hacia lo contrario y por eso estos tests existen: si alguien reintroduce la puerta
/// trasera "Unrestricted", varios de estos casos se ponen en rojo.
///
/// Cubre ademas: union (OR) entre varios roles, nivel de acceso maximo (gana el mas alto),
/// vigencia temporal, las 6 acciones y el filtrado de filas persistibles.
/// </summary>
public class PermissionResolverTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    private static ModulePermissionDto P(
        string key, bool v = false, bool c = false, bool e = false, bool d = false,
        bool x = false, bool i = false)
        => new(key, v, c, e, d, x, i);

    private static RolGrant Grant(
        long rolId,
        IEnumerable<ModulePermissionDto>? permisos = null,
        int nivelOrden = 1,
        RolEstado estado = RolEstado.Activo,
        DateTimeOffset? desde = null,
        DateTimeOffset? hasta = null)
        => new(rolId, estado, nivelOrden, desde, hasta, (permisos ?? []).ToList());

    // ---- FAIL-CLOSED (invariante 10) ----

    [Fact]
    public void Resolve_UsuarioSinRoles_NoTienePermisos()
    {
        // EL test de la casa: sin rol NO significa "acceso como antes", significa SIN PERMISOS.
        var eff = PermissionResolver.Resolve([], Ahora);

        Assert.True(eff.IsEmpty);
        Assert.Empty(eff.RolIds);
        Assert.Null(eff.NivelAccesoMaximoOrden);
        Assert.False(eff.Can("actividades", PermissionAction.View));
        Assert.False(eff.Can("cualquier-modulo", PermissionAction.Delete));
        Assert.Equal(ModuleAccess.None, eff.For("actividades"));
        // Y tampoco alcanza NINGUN nivel documental, ni el mas bajo.
        Assert.False(eff.AlcanzaNivel(1));
    }

    [Fact]
    public void Resolve_ColeccionNula_NoTienePermisos()
    {
        var eff = PermissionResolver.Resolve(null, Ahora);

        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("actividades", PermissionAction.View));
    }

    [Fact]
    public void EffectivePermissions_None_NoConcedeNada()
    {
        // El valor que devuelve TODO camino de fallo. Si alguna vez concede algo, el sistema
        // entero deja de ser fail-closed.
        var eff = EffectivePermissions.None;

        Assert.True(eff.IsEmpty);
        Assert.Null(eff.NivelAccesoMaximoOrden);
        foreach (var accion in PermissionActions.All)
        {
            Assert.False(eff.Can("modulo-cualquiera", accion));
        }
        Assert.False(eff.AlcanzaNivel(1));
        Assert.False(eff.AlcanzaNivel(4));
    }

    [Fact]
    public void Resolve_ModuloAusenteDeLaMatriz_NoConcede()
    {
        var eff = PermissionResolver.Resolve(
            [Grant(1, [P("inventario-items", v: true)])], Ahora);

        Assert.True(eff.Can("inventario-items", PermissionAction.View));
        // Lo que no esta en la matriz, no se concede.
        Assert.False(eff.Can("modulo-desconocido", PermissionAction.View));
    }

    // ---- Multi-rol: union (OR) ----

    [Fact]
    public void Resolve_DosRoles_UnePermisosPorOr()
    {
        // Rol A: ver+crear en expedientes. Rol B: editar en expedientes y ver en radicacion.
        var a = Grant(10, [P("expedientes", v: true, c: true)]);
        var b = Grant(20, [P("expedientes", e: true), P("radicacion", v: true)]);

        var eff = PermissionResolver.Resolve([a, b], Ahora);

        Assert.Equal(2, eff.RolIds.Count);
        // La union suma lo de ambos sobre el MISMO modulo...
        Assert.True(eff.Can("expedientes", PermissionAction.View));
        Assert.True(eff.Can("expedientes", PermissionAction.Create));
        Assert.True(eff.Can("expedientes", PermissionAction.Edit));
        // ...sin inventar lo que ninguno concede.
        Assert.False(eff.Can("expedientes", PermissionAction.Delete));
        // ...y arrastra los modulos que solo aporta uno de los dos.
        Assert.True(eff.Can("radicacion", PermissionAction.View));
        Assert.False(eff.Can("radicacion", PermissionAction.Edit));
    }

    [Fact]
    public void Resolve_TresRoles_UnionCompletaDeLasSeisAcciones()
    {
        // Cada rol aporta dos acciones distintas del mismo modulo: juntas dan las seis.
        var eff = PermissionResolver.Resolve(
        [
            Grant(1, [P("trd", v: true, c: true)]),
            Grant(2, [P("trd", e: true, d: true)]),
            Grant(3, [P("trd", x: true, i: true)])
        ], Ahora);

        foreach (var accion in PermissionActions.All)
        {
            Assert.True(eff.Can("trd", accion), $"la union deberia conceder {accion}");
        }
    }

    // ---- Multi-rol: nivel de acceso maximo ----

    [Fact]
    public void Resolve_DosRolesConNivelesDistintos_GanaElMasAlto()
    {
        // Publico (1) y Reservado (3): el usuario alcanza Reservado.
        var eff = PermissionResolver.Resolve(
        [
            Grant(1, nivelOrden: 1),
            Grant(2, nivelOrden: 3)
        ], Ahora);

        Assert.Equal(3, eff.NivelAccesoMaximoOrden);
        Assert.True(eff.AlcanzaNivel(1));
        Assert.True(eff.AlcanzaNivel(3));
        // Pero NO llega a Clasificado (4): "el mas alto" es el mas alto que tiene, no un cheque en blanco.
        Assert.False(eff.AlcanzaNivel(4));
    }

    [Fact]
    public void Resolve_ElOrdenDeLosRolesNoAlteraElNivelMaximo()
    {
        var altoPrimero = PermissionResolver.Resolve(
            [Grant(1, nivelOrden: 4), Grant(2, nivelOrden: 2)], Ahora);
        var bajoPrimero = PermissionResolver.Resolve(
            [Grant(2, nivelOrden: 2), Grant(1, nivelOrden: 4)], Ahora);

        Assert.Equal(4, altoPrimero.NivelAccesoMaximoOrden);
        Assert.Equal(4, bajoPrimero.NivelAccesoMaximoOrden);
    }

    [Fact]
    public void Resolve_ElNivelSaleSoloDeRolesVIGENTES()
    {
        // El rol Clasificado (4) esta vencido: el nivel efectivo debe caer al del rol vigente.
        var vencidoAlto = Grant(1, nivelOrden: 4, hasta: Ahora.AddDays(-1));
        var vigenteBajo = Grant(2, nivelOrden: 2);

        var eff = PermissionResolver.Resolve([vencidoAlto, vigenteBajo], Ahora);

        Assert.Equal(2, eff.NivelAccesoMaximoOrden);
        Assert.False(eff.AlcanzaNivel(4));
        Assert.Equal([2L], eff.RolIds);
    }

    // ---- Vigencia temporal ----

    [Fact]
    public void Resolve_RolConVigenteHastaPasado_NoCuenta()
    {
        // Un encargo temporal vencido queda revocado AUTOMATICAMENTE, sin que nadie borre la fila.
        var vencido = Grant(99, [P("expedientes", v: true, d: true)], hasta: Ahora.AddSeconds(-1));

        var eff = PermissionResolver.Resolve([vencido], Ahora);

        Assert.True(eff.IsEmpty);
        Assert.DoesNotContain(99L, eff.RolIds);
        Assert.False(eff.Can("expedientes", PermissionAction.View));
        Assert.False(eff.Can("expedientes", PermissionAction.Delete));
    }

    [Fact]
    public void Resolve_VigenteHastaEsExclusivo_EnElInstanteExactoYaNoCuenta()
    {
        var justoAlVencer = Grant(1, [P("m", v: true)], hasta: Ahora);
        var unSegundoAntes = Grant(2, [P("m", v: true)], hasta: Ahora.AddSeconds(1));

        Assert.True(PermissionResolver.Resolve([justoAlVencer], Ahora).IsEmpty);
        Assert.False(PermissionResolver.Resolve([unSegundoAntes], Ahora).IsEmpty);
    }

    [Fact]
    public void Resolve_RolQueAunNoEmpieza_NoCuenta()
    {
        var futuro = Grant(1, [P("m", v: true)], desde: Ahora.AddDays(1));

        var eff = PermissionResolver.Resolve([futuro], Ahora);

        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("m", PermissionAction.View));
    }

    [Fact]
    public void Resolve_RolDentroDeSuVentana_SiCuenta()
    {
        var vigente = Grant(1, [P("m", v: true)],
            desde: Ahora.AddDays(-1), hasta: Ahora.AddDays(1));

        Assert.True(PermissionResolver.Resolve([vigente], Ahora).Can("m", PermissionAction.View));
    }

    [Fact]
    public void Resolve_RolSinFechas_EsVigenteSiempre()
    {
        var permanente = Grant(1, [P("m", v: true)]);

        Assert.True(PermissionResolver.Resolve([permanente], Ahora).Can("m", PermissionAction.View));
    }

    [Fact]
    public void Resolve_RolInactivo_NoCuenta()
    {
        var inactivo = Grant(1, [P("m", v: true)], estado: RolEstado.Inactivo);

        var eff = PermissionResolver.Resolve([inactivo], Ahora);

        Assert.True(eff.IsEmpty);
        Assert.False(eff.Can("m", PermissionAction.View));
    }

    [Fact]
    public void Resolve_RolVencidoNoContaminaAlVigente()
    {
        // El vencido concedia Eliminar; el vigente solo Ver. Eliminar NO debe sobrevivir.
        var vencido = Grant(1, [P("expedientes", d: true)], hasta: Ahora.AddDays(-1));
        var vigente = Grant(2, [P("expedientes", v: true)]);

        var eff = PermissionResolver.Resolve([vencido, vigente], Ahora);

        Assert.True(eff.Can("expedientes", PermissionAction.View));
        Assert.False(eff.Can("expedientes", PermissionAction.Delete));
    }

    // ---- Las SEIS acciones ----

    [Fact]
    public void Resolve_LasSeisAccionesSeResuelvenIndependientemente()
    {
        var eff = PermissionResolver.Resolve(
            [Grant(1, [P("expedientes", v: true, x: true)], nivelOrden: 3)], Ahora);

        Assert.True(eff.Can("expedientes", PermissionAction.View));
        Assert.True(eff.Can("expedientes", PermissionAction.Export));
        // Exportar no arrastra imprimir: sacar informacion se concede accion por accion.
        Assert.False(eff.Can("expedientes", PermissionAction.Print));
        Assert.False(eff.Can("expedientes", PermissionAction.Create));
        Assert.False(eff.Can("expedientes", PermissionAction.Edit));
        Assert.False(eff.Can("expedientes", PermissionAction.Delete));
    }

    [Fact]
    public void PermissionActions_All_TieneLasSeisDeLaSpec()
    {
        Assert.Equal(6, PermissionActions.All.Count);
        Assert.Equal(
            [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit,
             PermissionAction.Delete, PermissionAction.Export, PermissionAction.Print],
            PermissionActions.All);
        // Las etiquetas de la spec estan en espanol.
        Assert.Equal("Exportar", PermissionActions.Label(PermissionAction.Export));
        Assert.Equal("Imprimir", PermissionActions.Label(PermissionAction.Print));
    }

    [Fact]
    public void ModuleAccess_Can_MapeaCadaAccion()
    {
        var access = new ModuleAccess(
            View: true, Create: false, Edit: true, Delete: false, Export: true, Print: false);

        Assert.True(access.Can(PermissionAction.View));
        Assert.False(access.Can(PermissionAction.Create));
        Assert.True(access.Can(PermissionAction.Edit));
        Assert.False(access.Can(PermissionAction.Delete));
        Assert.True(access.Can(PermissionAction.Export));
        Assert.False(access.Can(PermissionAction.Print));
    }

    [Fact]
    public void ModulePermissionDto_GrantedActions_SonLasFilasQueSePersisten()
    {
        var fila = P("m", v: true, d: true, i: true);

        Assert.Equal(
            [PermissionAction.View, PermissionAction.Delete, PermissionAction.Print],
            fila.GrantedActions().ToList());

        // Ida y vuelta: fila -> acciones -> fila.
        var round = ModulePermissionDto.FromActions("m", fila.GrantedActions());
        Assert.Equal(fila, round);
    }

    // ---- Filtrado de filas persistibles ----

    [Fact]
    public void FilterPersistable_DescartaFilasSinNingunPermiso()
    {
        var input = new[] { P("con-flag", v: true), P("vacio"), P("otro-flag", x: true) };

        var kept = PermissionResolver.FilterPersistable(input);

        Assert.Equal(2, kept.Count);
        Assert.Contains(kept, k => k.ModuleKey == "con-flag");
        Assert.Contains(kept, k => k.ModuleKey == "otro-flag");
        Assert.DoesNotContain(kept, k => k.ModuleKey == "vacio");
    }

    [Fact]
    public void FilterPersistable_DeduplicaPorModuleKey_GanaLaUltima()
    {
        var kept = PermissionResolver.FilterPersistable([P("mod", v: true), P("mod", e: true)]);

        var row = Assert.Single(kept);
        Assert.True(row.CanEdit);
        Assert.False(row.CanView);
    }

    [Fact]
    public void FilterPersistable_RespetaLaListaBlancaDelCatalogo()
    {
        var input = new[] { P("valido", v: true), P("fuera-de-catalogo", c: true) };
        var valid = new HashSet<string>(StringComparer.Ordinal) { "valido" };

        var row = Assert.Single(PermissionResolver.FilterPersistable(input, valid));
        Assert.Equal("valido", row.ModuleKey);
    }

    [Fact]
    public void FilterPersistable_IgnoraModuleKeysEnBlanco()
    {
        var kept = PermissionResolver.FilterPersistable([P("", v: true), P("  ", c: true), P("ok", d: true)]);

        Assert.Single(kept);
        Assert.Equal("ok", kept[0].ModuleKey);
    }
}
