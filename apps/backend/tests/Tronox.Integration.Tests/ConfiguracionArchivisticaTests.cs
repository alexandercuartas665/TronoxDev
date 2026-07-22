using Microsoft.EntityFrameworkCore;
using Tronox.Application.Archivistica;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Persistence;

namespace Tronox.Integration.Tests;

/// <summary>
/// Tests de integracion de la configuracion archivistica (RQ01 - RF01-P.3, RF01 4.1.2 y RF02)
/// contra PostgreSQL real (Testcontainers): unicidad de codigo_fondo POR TENANT, fondo Cerrado
/// como solo lectura, fecha de cierre y entidad de origen, sede_id NULL = transversal, sede
/// Inactiva no ofrecida, bloqueo de eliminacion con dependencias, unicidad del codigo de
/// subfondo dentro del fondo, y siembra IDEMPOTENTE de los 4 niveles de clasificacion.
///
/// Las validaciones puras estan en Tronox.Application.Tests/ArchivisticaRulesTests.
/// </summary>
public abstract class ConfiguracionArchivisticaTestsBase
{
    private readonly TenantIsolationDbFixture _fixture;

    protected ConfiguracionArchivisticaTestsBase(TenantIsolationDbFixture fixture) => _fixture = fixture;

    private static readonly DateOnly Apertura = new(2020, 1, 15);

    // ================= Niveles de clasificacion documental (RF01-P.3) =================

    [Fact]
    public async Task Provisioning_SiembraLosCuatroNiveles_YEsIdempotente()
    {
        var tenantId = await NewTenantAsync("Arch Niveles");

        // Primera siembra: los 4 canonicos, con codigo, color y orden exactos de la spec.
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(tenantId);
        }

        var niveles = await ListNivelesAsync(tenantId);
        Assert.Equal(4, niveles.Count);
        Assert.Equal(new[] { "Publico", "Interno", "Reservado", "Clasificado" }, niveles.Select(n => n.Nombre));
        Assert.Equal(new[] { "01", "02", "03", "04" }, niveles.Select(n => n.Codigo));
        Assert.Equal(new[] { "#27AE60", "#2980B9", "#E67E22", "#C0392B" }, niveles.Select(n => n.ColorEtiqueta));
        Assert.Equal(new[] { 1, 2, 3, 4 }, niveles.Select(n => n.NivelOrden));
        Assert.All(niveles, n => Assert.True(n.Activo));

        // IDEMPOTENTE: correrla de nuevo no duplica ni revive nada.
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(tenantId);
        }
        Assert.Equal(4, (await ListNivelesAsync(tenantId)).Count);

        // Y no reintroduce lo que el tenant desactivo: la segunda pasada respeta su estado.
        var publico = niveles.Single(n => n.Codigo == "01");
        await RunNivelesAsync(tenantId, s => s.SetActivoAsync(publico.Id, false, "prueba", TestIds.Next()));
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(tenantId);
        }
        var tras = await ListNivelesAsync(tenantId);
        Assert.Equal(4, tras.Count);
        Assert.False(tras.Single(n => n.Codigo == "01").Activo);
    }

    [Fact]
    public async Task Niveles_SeSiembranPorTenant_YNoSeVenDesdeOtroTenant()
    {
        var a = await NewTenantAsync("Arch Niveles A");
        var b = await NewTenantAsync("Arch Niveles B");

        await using (var ctx = _fixture.CreateContext(a))
        {
            await new ClasificacionProvisioningService(ctx).EnsureNivelesClasificacionAsync(a);
        }

        Assert.Equal(4, (await ListNivelesAsync(a)).Count);
        // El tenant B no recibe los niveles de A (filtro global, DAT-01).
        Assert.Empty(await ListNivelesAsync(b));
    }

    // ================= Fondos documentales (RF02) =================

    [Fact]
    public async Task CodigoFondo_EsUnicoDentroDelTenant()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Unico");

        var primero = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("F01", "Fondo Alcaldia"), TestIds.Next()));
        Assert.True(primero.IsOk, primero.Error);

        var duplicado = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("F01", "Otro fondo"), TestIds.Next()));
        Assert.False(duplicado.IsOk);
        Assert.Equal(ArchivisticaServiceStatus.Conflict, duplicado.Status);
    }

    [Fact]
    public async Task CodigoFondo_NoChocaEntreTenantsDistintos()
    {
        var a = await NewTenantAsync("Arch Fondo Tenant A");
        var b = await NewTenantAsync("Arch Fondo Tenant B");

        // Regla 3 de RF02: la unicidad es POR TENANT, no global.
        var enA = await RunFondosAsync(a, s => s.SaveAsync(NuevoFondo("F01", "Fondo de A"), TestIds.Next()));
        Assert.True(enA.IsOk, enA.Error);

        var enB = await RunFondosAsync(b, s => s.SaveAsync(NuevoFondo("F01", "Fondo de B"), TestIds.Next()));
        Assert.True(enB.IsOk, enB.Error);
        Assert.NotEqual(enA.Value!.Id, enB.Value!.Id);

        // Y cada tenant ve solo el suyo.
        var listaA = await RunFondosAsync(a, s => s.ListAsync());
        Assert.Single(listaA);
        Assert.Equal("Fondo de A", listaA[0].NombreFondo);
    }

    [Fact]
    public async Task Tenant_AdmiteMultiplesFondos()
    {
        var tenantId = await NewTenantAsync("Arch Multiples Fondos");

        foreach (var codigo in new[] { "F01", "F02", "F03" })
        {
            var res = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo(codigo, $"Fondo {codigo}"), TestIds.Next()));
            Assert.True(res.IsOk, res.Error);
        }

        Assert.Equal(3, (await RunFondosAsync(tenantId, s => s.ListAsync())).Count);
    }

    [Fact]
    public async Task FechaCierre_EsObligatoriaYPosterior_CuandoElEstadoEsCerrado()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Cierre");

        // Sin fecha de cierre: invalido.
        var sinFecha = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FC1", "Fondo cerrado") with { Estado = FondoEstado.Cerrado }, TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, sinFecha.Status);
        Assert.Contains("fecha de cierre es obligatoria", sinFecha.Error);

        // Con fecha anterior a la apertura: invalido.
        var anterior = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FC1", "Fondo cerrado") with
            {
                Estado = FondoEstado.Cerrado,
                FechaCierre = Apertura.AddDays(-1)
            }, TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, anterior.Status);
        Assert.Contains("posterior a la fecha de apertura", anterior.Error);

        // Con fecha posterior: valido.
        var ok = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FC1", "Fondo cerrado") with
            {
                Estado = FondoEstado.Cerrado,
                FechaCierre = Apertura.AddYears(2)
            }, TestIds.Next()));
        Assert.True(ok.IsOk, ok.Error);
        Assert.Equal(FondoEstado.Cerrado, ok.Value!.Estado);
        Assert.Equal(Apertura.AddYears(2), ok.Value.FechaCierre);
    }

    [Fact]
    public async Task EntidadOrigen_EsObligatoria_CuandoElTipoEsAcumulado()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Acumulado");

        var sinOrigen = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FA1", "Fondo acumulado") with { TipoFondo = FondoTipo.Acumulado }, TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, sinOrigen.Status);
        Assert.Contains("entidad de origen es obligatoria", sinOrigen.Error);

        var conOrigen = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FA1", "Fondo acumulado") with
            {
                TipoFondo = FondoTipo.Acumulado,
                EntidadOrigen = "Instituto Departamental (liquidado 2019)"
            }, TestIds.Next()));
        Assert.True(conOrigen.IsOk, conOrigen.Error);
        Assert.Equal("Instituto Departamental (liquidado 2019)", conOrigen.Value!.EntidadOrigen);
    }

    [Fact]
    public async Task FondoCerrado_EsSoloLectura_PeroSeConsultaYExporta()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Cerrado");

        var creado = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("FCE", "Fondo a cerrar"), TestIds.Next()));
        var fondoId = creado.Value!.Id;

        var cerrado = await RunFondosAsync(tenantId,
            s => s.CerrarAsync(fondoId, Apertura.AddYears(3), "fin de vigencia", TestIds.Next()));
        Assert.True(cerrado.IsOk, cerrado.Error);
        Assert.Equal(FondoEstado.Cerrado, cerrado.Value!.Estado);
        Assert.True(cerrado.Value.EsSoloLectura);

        // 1) La puerta de altas se cierra: nada nuevo puede colgar del fondo.
        var admite = await RunFondosAsync(tenantId, s => s.EnsureAdmiteAltasAsync(fondoId));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, admite.Status);
        Assert.Contains("solo lectura", admite.Error);

        // 2) Crear un subfondo colgando de el se rechaza (primer consumidor real de la regla).
        var sub = await RunSubfondosAsync(tenantId, s => s.SaveAsync(
            new SaveSubfondoRequest(null, fondoId, "SF01", "Subfondo", SubfondoEstado.Activo), TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, sub.Status);
        Assert.Contains("solo lectura", sub.Error);

        // 3) Modificarlo manteniendolo Cerrado se rechaza.
        var edicion = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FCE", "Nombre cambiado") with
            {
                Id = fondoId,
                Estado = FondoEstado.Cerrado,
                FechaCierre = Apertura.AddYears(3)
            }, TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, edicion.Status);

        // 4) Pero consultar y listar (base de la exportacion) sigue funcionando sin limite.
        var leido = await RunFondosAsync(tenantId, s => s.GetAsync(fondoId));
        Assert.NotNull(leido);
        Assert.Equal("Fondo a cerrar", leido!.NombreFondo);
        Assert.Contains(await RunFondosAsync(tenantId, s => s.ListAsync()), f => f.Id == fondoId);
    }

    [Fact]
    public async Task FondoActivo_AdmiteAltas()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Activo");
        var creado = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("FAC", "Fondo vivo"), TestIds.Next()));

        var admite = await RunFondosAsync(tenantId, s => s.EnsureAdmiteAltasAsync(creado.Value!.Id));
        Assert.True(admite.IsOk, admite.Error);
    }

    [Fact]
    public async Task SedeIdNulo_SignificaFondoTransversalATodaLaEntidad()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Transversal");

        var sede = await RunSedesAsync(tenantId, s => s.SaveAsync(NuevaSede("SED01", "Sede Principal"), TestIds.Next()));
        Assert.True(sede.IsOk, sede.Error);

        // Sin sede -> transversal a toda la entidad (semantica explicita de la spec).
        var transversal = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FT1", "Fondo transversal"), TestIds.Next()));
        Assert.True(transversal.IsOk, transversal.Error);
        Assert.Null(transversal.Value!.SedeId);
        Assert.True(transversal.Value.EsTransversal);
        Assert.Null(transversal.Value.NombreSede);

        // Con sede -> fondo de esa sede, NO transversal.
        var deSede = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FT2", "Fondo de sede") with { SedeId = sede.Value!.Id }, TestIds.Next()));
        Assert.True(deSede.IsOk, deSede.Error);
        Assert.Equal(sede.Value!.Id, deSede.Value!.SedeId);
        Assert.False(deSede.Value.EsTransversal);
        Assert.Equal("Sede Principal", deSede.Value.NombreSede);

        // Y la distincion sobrevive a la relectura desde base.
        var releido = await RunFondosAsync(tenantId, s => s.ListAsync());
        Assert.True(releido.Single(f => f.CodigoFondo == "FT1").EsTransversal);
        Assert.False(releido.Single(f => f.CodigoFondo == "FT2").EsTransversal);
    }

    [Fact]
    public async Task Fondo_ConDependencias_NoSePuedeEliminar_YSeSugiereInactivarOCerrar()
    {
        var tenantId = await NewTenantAsync("Arch Fondo NoBorrable");

        var fondo = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("FNB", "Fondo con hijos"), TestIds.Next()));
        var fondoId = fondo.Value!.Id;

        var sub = await RunSubfondosAsync(tenantId, s => s.SaveAsync(
            new SaveSubfondoRequest(null, fondoId, "SF01", "Subfondo Secretaria", SubfondoEstado.Activo), TestIds.Next()));
        Assert.True(sub.IsOk, sub.Error);

        var borrado = await RunFondosAsync(tenantId, s => s.DeleteAsync(fondoId, TestIds.Next()));
        Assert.False(borrado.IsOk);
        Assert.Equal(ArchivisticaServiceStatus.Invalid, borrado.Status);
        Assert.Contains("1 subfondo(s)", borrado.Error);
        Assert.Contains("Inactivelo o cierrelo", borrado.Error);

        // El fondo sigue ahi: no hubo borrado fisico (invariante 8).
        Assert.NotNull(await RunFondosAsync(tenantId, s => s.GetAsync(fondoId)));
    }

    [Fact]
    public async Task Fondo_SinDependencias_TampocoSeElimina_SeInactiva()
    {
        var tenantId = await NewTenantAsync("Arch Fondo Inactivar");
        var fondo = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("FIN", "Fondo suelto"), TestIds.Next()));
        var fondoId = fondo.Value!.Id;

        var borrado = await RunFondosAsync(tenantId, s => s.DeleteAsync(fondoId, TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, borrado.Status);
        Assert.Contains("no se eliminan", borrado.Error);

        var inactivado = await RunFondosAsync(tenantId, s => s.InactivarAsync(fondoId, "cese de operaciones", TestIds.Next()));
        Assert.True(inactivado.IsOk, inactivado.Error);
        Assert.Equal(FondoEstado.Inactivo, inactivado.Value!.Estado);
        Assert.NotNull(await RunFondosAsync(tenantId, s => s.GetAsync(fondoId)));
    }

    [Fact]
    public async Task Fondo_QuedaEnLaPistaDeAuditoria_AlCrearYModificar()
    {
        // Regla 5 de RF02, con el AuditWriter REAL: el alta debe quedar con el id ya materializado.
        var tenantId = await NewTenantAsync("Arch Fondo Auditoria");

        long fondoId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var svc = new FondoService(ctx, new TestTenantContext(tenantId), new AuditWriter(ctx));
            var creado = await svc.SaveAsync(NuevoFondo("FAU", "Fondo auditado"), actorUserId: 77);
            Assert.True(creado.IsOk, creado.Error);
            fondoId = creado.Value!.Id;

            var editado = await svc.SaveAsync(
                NuevoFondo("FAU", "Fondo auditado v2") with { Id = fondoId }, actorUserId: 77);
            Assert.True(editado.IsOk, editado.Error);
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var asientos = await ctx.SuperAdminAuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(Fondo) && a.TenantId == tenantId)
                .ToListAsync();

            var alta = Assert.Single(asientos, a => a.ActionName == "fondo.create");
            // El id NO puede quedar en 0: la sobrecarga por entidad lo resuelve tras el INSERT.
            Assert.Equal(fondoId, alta.EntityId);
            var cambio = Assert.Single(asientos, a => a.ActionName == "fondo.update");
            Assert.Equal(fondoId, cambio.EntityId);
        }
    }

    // ================= Sedes (RF01 4.1.2) =================

    [Fact]
    public async Task SedeInactiva_NoSeOfreceAlCrearFondos_YNoSePuedeAsignar()
    {
        var tenantId = await NewTenantAsync("Arch Sede Inactiva");

        var activa = await RunSedesAsync(tenantId, s => s.SaveAsync(NuevaSede("SED01", "Sede Norte"), TestIds.Next()));
        var inactiva = await RunSedesAsync(tenantId, s => s.SaveAsync(NuevaSede("SED02", "Sede Sur"), TestIds.Next()));
        await RunSedesAsync(tenantId, s => s.InactivarAsync(inactiva.Value!.Id, "cierre de sede", TestIds.Next()));

        // Criterio de aceptacion: la sede Inactiva no se ofrece al crear fondos.
        var seleccionables = await RunSedesAsync(tenantId, s => s.ListSeleccionablesParaFondoAsync());
        Assert.Single(seleccionables);
        Assert.Equal(activa.Value!.Id, seleccionables[0].Id);

        // Y el servicio la rechaza aunque el cliente la mande igual.
        var conInactiva = await RunFondosAsync(tenantId, s => s.SaveAsync(
            NuevoFondo("FSI", "Fondo de sede muerta") with { SedeId = inactiva.Value!.Id }, TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, conInactiva.Status);
        Assert.Contains("Inactiva", conInactiva.Error);

        // La sede inactiva sigue existiendo: no hubo borrado fisico.
        Assert.Equal(2, (await RunSedesAsync(tenantId, s => s.ListAsync())).Count);
    }

    [Fact]
    public async Task CodigoSede_EsUnicoPorTenant_YNoChocaEntreTenants()
    {
        var a = await NewTenantAsync("Arch Sede A");
        var b = await NewTenantAsync("Arch Sede B");

        Assert.True((await RunSedesAsync(a, s => s.SaveAsync(NuevaSede("SED01", "Principal A"), TestIds.Next()))).IsOk);

        var duplicada = await RunSedesAsync(a, s => s.SaveAsync(NuevaSede("SED01", "Otra de A"), TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Conflict, duplicada.Status);

        // Mismo codigo en otro tenant: sin conflicto.
        Assert.True((await RunSedesAsync(b, s => s.SaveAsync(NuevaSede("SED01", "Principal B"), TestIds.Next()))).IsOk);
    }

    // ================= Subfondos (RF02 5.2.2) =================

    [Fact]
    public async Task CodigoSubfondo_EsUnicoDentroDelFondo_NoDelTenant()
    {
        var tenantId = await NewTenantAsync("Arch Subfondo Unico");

        var f1 = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("F01", "Fondo uno"), TestIds.Next()));
        var f2 = await RunFondosAsync(tenantId, s => s.SaveAsync(NuevoFondo("F02", "Fondo dos"), TestIds.Next()));

        var primero = await RunSubfondosAsync(tenantId, s => s.SaveAsync(
            new SaveSubfondoRequest(null, f1.Value!.Id, "SF01", "Subfondo A", SubfondoEstado.Activo), TestIds.Next()));
        Assert.True(primero.IsOk, primero.Error);

        // Mismo codigo en el MISMO fondo: conflicto.
        var duplicado = await RunSubfondosAsync(tenantId, s => s.SaveAsync(
            new SaveSubfondoRequest(null, f1.Value!.Id, "SF01", "Subfondo B", SubfondoEstado.Activo), TestIds.Next()));
        Assert.Equal(ArchivisticaServiceStatus.Conflict, duplicado.Status);

        // Mismo codigo en OTRO fondo: permitido (la unicidad es dentro del fondo).
        var enOtroFondo = await RunSubfondosAsync(tenantId, s => s.SaveAsync(
            new SaveSubfondoRequest(null, f2.Value!.Id, "SF01", "Subfondo C", SubfondoEstado.Activo), TestIds.Next()));
        Assert.True(enOtroFondo.IsOk, enOtroFondo.Error);

        Assert.Single(await RunSubfondosAsync(tenantId, s => s.ListAsync(f1.Value!.Id)));
        Assert.Single(await RunSubfondosAsync(tenantId, s => s.ListAsync(f2.Value!.Id)));
    }

    // ================= Helpers =================

    private static SaveFondoRequest NuevoFondo(string codigo, string nombre) => new(
        Id: null,
        CodigoFondo: codigo,
        NombreFondo: nombre,
        Descripcion: null,
        SedeId: null,
        TipoFondo: FondoTipo.Activo,
        Estado: FondoEstado.Activo,
        FechaApertura: Apertura,
        FechaCierre: null,
        EntidadOrigen: null);

    private static SaveSedeRequest NuevaSede(string codigo, string nombre) => new(
        Id: null,
        NombreSede: nombre,
        CodigoSede: codigo,
        SiglaSede: "SD",
        PaisId: null,
        DepartamentoId: null,
        CiudadId: null,
        Direccion: "Calle 1 # 2-3",
        Telefono: "6011234567",
        CorreoSede: "sede@entidad.gov.co",
        Estado: SedeEstado.Activo);

    private async Task<long> NewTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    private async Task<IReadOnlyList<NivelClasificacionDto>> ListNivelesAsync(long tenantId)
        => await RunNivelesAsync(tenantId, s => s.ListAsync());

    private async Task<T> RunNivelesAsync<T>(long tenantId, Func<INivelClasificacionService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        return await action(new NivelClasificacionService(ctx, new TestTenantContext(tenantId), new NoOpAuditWriter()));
    }

    private async Task<T> RunSedesAsync<T>(long tenantId, Func<ISedeService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        return await action(new SedeService(ctx, new TestTenantContext(tenantId), new NoOpAuditWriter()));
    }

    private async Task<T> RunFondosAsync<T>(long tenantId, Func<IFondoService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        return await action(new FondoService(ctx, new TestTenantContext(tenantId), new NoOpAuditWriter()));
    }

    private async Task<T> RunSubfondosAsync<T>(long tenantId, Func<ISubfondoService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var tenant = new TestTenantContext(tenantId);
        var audit = new NoOpAuditWriter();
        return await action(new SubfondoService(ctx, tenant, audit, new FondoService(ctx, tenant, audit)));
    }

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
            // La escritura real de auditoria se verifica aparte, con el AuditWriter autentico.
        }
    }
}

/// <summary>Motor PostgreSQL (contenedor efimero postgres:16-alpine, Testcontainers).</summary>
public sealed class ConfiguracionArchivisticaTests_Postgres
    : ConfiguracionArchivisticaTestsBase, IClassFixture<PostgresTenantIsolationFixture>
{
    public ConfiguracionArchivisticaTests_Postgres(PostgresTenantIsolationFixture fixture) : base(fixture) { }
}
