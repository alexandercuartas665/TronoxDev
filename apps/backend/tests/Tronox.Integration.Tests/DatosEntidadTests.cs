using Microsoft.EntityFrameworkCore;
using Tronox.Application.Archivistica;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Persistence;

namespace Tronox.Integration.Tests;

/// <summary>
/// Tests de integracion de "Datos de la Entidad" (RQ01 - RF01 seccion 4.1.1) contra PostgreSQL
/// real (Testcontainers):
///
/// - UNA SOLA ENTIDAD POR TENANT (criterio 1): guardar dos veces actualiza, no duplica, y el
///   indice unico sobre tenant_id lo impide incluso saltandose el servicio;
/// - aislamiento entre tenants (DAT-01);
/// - obligatoriedad condicional si tipo_entidad = Publica (criterio 4);
/// - el codigo de fondo AGN se GENERA, no se captura (resolucion M01);
/// - la entidad no se elimina: solo cambia de estado (criterio 8);
/// - los catalogos DIVIPOLA quedan sembrados por la migracion y encadenan correctamente.
///
/// Las validaciones puras estan en Tronox.Application.Tests/EntidadRulesTests.
/// </summary>
public abstract class DatosEntidadTestsBase
{
    private readonly TenantIsolationDbFixture _fixture;

    protected DatosEntidadTestsBase(TenantIsolationDbFixture fixture) => _fixture = fixture;

    // Bogota D.C.: pais 1 (Colombia), departamento 3 (codigo DANE 11), municipio 3 (11001).
    private const long ColombiaId = 1;
    private const long BogotaDeptoId = 3;
    private const long BogotaMunicipioId = 3;

    // ================= Una sola entidad por tenant =================

    [Fact]
    public async Task Tenant_SoloPuedeTenerUnaEntidad_ElSegundoGuardadoActualiza()
    {
        var tenantId = await NewTenantAsync("Entidad Unica");

        var primera = await RunAsync(tenantId, s => s.SaveAsync(NuevaEntidad(), actorUserId: 7));
        Assert.True(primera.IsOk, primera.Error);
        var id = primera.Value!.Id;

        // Segundo "alta" con datos distintos: NO crea otra, actualiza la misma fila.
        var segunda = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { RazonSocial = "Nombre corregido", Sigla = "NUEVA" }, actorUserId: 7));
        Assert.True(segunda.IsOk, segunda.Error);
        Assert.Equal(id, segunda.Value!.Id);
        Assert.Equal("Nombre corregido", segunda.Value.RazonSocial);

        await using var ctx = _fixture.CreateContext(tenantId);
        Assert.Equal(1, await ctx.Entidades.CountAsync());
    }

    [Fact]
    public async Task SegundaEntidad_LaRechazaLaBase_AunqueSeSalteElServicio()
    {
        // El criterio 1 de RF01 no puede depender solo de un if: hay un indice unico sobre
        // tenant_id. Si otro camino intentara insertar una segunda entidad, la base la rechaza.
        var tenantId = await NewTenantAsync("Entidad Indice Unico");
        Assert.True((await RunAsync(tenantId, s => s.SaveAsync(NuevaEntidad(), 7))).IsOk);

        await using var ctx = _fixture.CreateContext(tenantId);
        ctx.Entidades.Add(new Entidad
        {
            TenantId = tenantId,
            Nit = "900123456",
            DigitoVerificacion = "8",
            RazonSocial = "Entidad clandestina",
            Sigla = "CLAN",
            TipoEntidad = TipoEntidad.Privada,
            DireccionPrincipal = "Calle falsa 123",
            CorreoInstitucional = "x@y.gov.co",
            RepresentanteLegal = "Nadie",
            ZonaHoraria = "America/Bogota",
            IdiomaDefecto = "es-CO"
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Entidad_NoSeVeDesdeOtroTenant()
    {
        var a = await NewTenantAsync("Entidad Tenant A");
        var b = await NewTenantAsync("Entidad Tenant B");

        Assert.True((await RunAsync(a, s => s.SaveAsync(NuevaEntidad(), 7))).IsOk);

        // Filtro global (DAT-01): B no ve la entidad de A y puede registrar la suya con el
        // mismo NIT, porque la unicidad es POR TENANT.
        Assert.Null(await RunAsync(b, s => s.GetAsync()));
        var enB = await RunAsync(b, s => s.SaveAsync(NuevaEntidad() with { RazonSocial = "Entidad de B" }, 7));
        Assert.True(enB.IsOk, enB.Error);
        Assert.Equal("Entidad de B", enB.Value!.RazonSocial);

        var deA = await RunAsync(a, s => s.GetAsync());
        Assert.Equal("Empresa Municipal de Servicios Publicos", deA!.RazonSocial);
    }

    // ================= Obligatoriedad condicional (criterio 4) =================

    [Fact]
    public async Task EntidadPublica_ExigeDivipolaYCodigoAgn()
    {
        var tenantId = await NewTenantAsync("Entidad Publica");

        var sinDivipola = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { TipoEntidad = TipoEntidad.Publica, CodigoDivipola = null }, 7));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, sinDivipola.Status);
        Assert.Contains("DIVIPOLA", sinDivipola.Error);

        // Sin DIVIPOLA tampoco se puede generar el codigo AGN: las dos reglas caen juntas.
        var conDivipola = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { TipoEntidad = TipoEntidad.Publica }, 7));
        Assert.True(conDivipola.IsOk, conDivipola.Error);
        Assert.Equal("CO-11001-EMSP", conDivipola.Value!.CodigoFondoAgn);
    }

    [Fact]
    public async Task EntidadPrivada_SeGuardaSinDivipolaNiCodigoAgn()
    {
        var tenantId = await NewTenantAsync("Entidad Privada");

        var result = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { TipoEntidad = TipoEntidad.Privada, CodigoDivipola = null }, 7));

        Assert.True(result.IsOk, result.Error);
        Assert.Null(result.Value!.CodigoDivipola);
        Assert.Null(result.Value.CodigoFondoAgn);
        Assert.False(result.Value.RequiereDatosAgn);
    }

    // ================= Codigo AGN generado, NIT calculado =================

    [Fact]
    public async Task CodigoFondoAgn_SeGeneraYSeRegeneraAlCambiarLaSigla()
    {
        var tenantId = await NewTenantAsync("Entidad AGN");

        var creada = await RunAsync(tenantId, s => s.SaveAsync(NuevaEntidad(), 7));
        Assert.Equal("CO-11001-EMSP", creada.Value!.CodigoFondoAgn);

        // Cambiar la sigla debe arrastrar el codigo AGN: no se queda con el anterior.
        var cambiada = await RunAsync(tenantId, s => s.SaveAsync(NuevaEntidad() with { Sigla = "emsp2" }, 7));
        Assert.Equal("CO-11001-EMSP2", cambiada.Value!.CodigoFondoAgn);
        Assert.Equal("EMSP2", cambiada.Value.Sigla);
    }

    [Fact]
    public async Task Nit_SeNormalizaYSuDigitoVerificadorSeCalcula()
    {
        var tenantId = await NewTenantAsync("Entidad NIT");

        var result = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { Nit = "800.197.268", DigitoVerificacion = "4" }, 7));

        Assert.True(result.IsOk, result.Error);
        Assert.Equal("800197268", result.Value!.Nit);
        Assert.Equal("4", result.Value.DigitoVerificacion);
        Assert.Equal("800197268-4", result.Value.NitCompleto);
    }

    [Fact]
    public async Task Nit_ConDigitoVerificadorEquivocado_NoSeGuarda()
    {
        var tenantId = await NewTenantAsync("Entidad NIT Malo");

        var result = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { DigitoVerificacion = "9" }, 7));

        Assert.Equal(ArchivisticaServiceStatus.Invalid, result.Status);
        Assert.Null(await RunAsync(tenantId, s => s.GetAsync()));
    }

    // ================= Ubicacion encadenada =================

    [Fact]
    public async Task Ubicacion_RechazaCombinacionesIncoherentes()
    {
        var tenantId = await NewTenantAsync("Entidad Ubicacion");

        // Municipio de Bogota bajo el departamento de Antioquia (id 1): incoherente.
        var cruzada = await RunAsync(tenantId, s => s.SaveAsync(
            NuevaEntidad() with { DepartamentoId = 1 }, 7));
        Assert.Equal(ArchivisticaServiceStatus.Invalid, cruzada.Status);
        Assert.Contains("no pertenece al departamento", cruzada.Error);
    }

    [Fact]
    public async Task CatalogosDivipola_QuedanSembradosPorLaMigracion()
    {
        var tenantId = await NewTenantAsync("Entidad Catalogos");
        await using var ctx = _fixture.CreateContext(tenantId);
        var divipola = new DivipolaService(ctx);

        var paises = await divipola.ListPaisesAsync();
        Assert.Single(paises);
        Assert.Equal("CO", paises[0].CodigoIso2);

        // 32 departamentos + Bogota D.C.
        var departamentos = await divipola.ListDepartamentosAsync(paises[0].Id);
        Assert.Equal(33, departamentos.Count);

        // Encadenado: los municipios de Bogota D.C. traen su codigo DIVIPOLA de 5 digitos.
        var bogota = departamentos.Single(d => d.CodigoDane == "11");
        var municipios = await divipola.ListMunicipiosAsync(bogota.Id);
        Assert.Contains(municipios, m => m.CodigoDivipola == "11001");

        // Cundinamarca no tiene capital propia: se sembraron municipios para que no salga vacia.
        var cundinamarca = departamentos.Single(d => d.CodigoDane == "25");
        Assert.NotEmpty(await divipola.ListMunicipiosAsync(cundinamarca.Id));
    }

    // ================= Sin eliminacion (criterio 8) =================

    [Fact]
    public async Task Entidad_NoSeElimina_SoloCambiaDeEstado()
    {
        var tenantId = await NewTenantAsync("Entidad Estado");
        Assert.True((await RunAsync(tenantId, s => s.SaveAsync(NuevaEntidad(), 7))).IsOk);

        var suspendida = await RunAsync(tenantId,
            s => s.CambiarEstadoAsync(EntidadEstado.Suspendido, "medida cautelar", 7));
        Assert.True(suspendida.IsOk, suspendida.Error);
        Assert.Equal(EntidadEstado.Suspendido, suspendida.Value!.Estado);

        // Sigue existiendo: no hubo borrado fisico.
        Assert.NotNull(await RunAsync(tenantId, s => s.GetAsync()));

        // El mensaje unico del bloqueo esta disponible para la presentacion.
        Assert.Contains("no se elimina", EntidadRules.EntidadNoEliminable);
    }

    [Fact]
    public async Task Entidad_QuedaEnLaPistaDeAuditoria_ConValorAnteriorYNuevo()
    {
        // Criterio 7 de RF01, con el AuditWriter REAL: el alta debe quedar con el id materializado.
        var tenantId = await NewTenantAsync("Entidad Auditoria");

        long entidadId;
        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var svc = new EntidadService(ctx, new TestTenantContext(tenantId), new AuditWriter(ctx));
            var creada = await svc.SaveAsync(NuevaEntidad(), actorUserId: 77);
            Assert.True(creada.IsOk, creada.Error);
            entidadId = creada.Value!.Id;

            var editada = await svc.SaveAsync(NuevaEntidad() with { RazonSocial = "Otro nombre" }, 77);
            Assert.True(editada.IsOk, editada.Error);
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var asientos = await ctx.SuperAdminAuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(Entidad) && a.TenantId == tenantId)
                .ToListAsync();

            var alta = Assert.Single(asientos, a => a.ActionName == "entidad.create");
            Assert.Equal(entidadId, alta.EntityId);

            var cambio = Assert.Single(asientos, a => a.ActionName == "entidad.update");
            Assert.Equal(entidadId, cambio.EntityId);
            // Valor anterior y nuevo (criterio 7): el cambio de nombre debe verse en ambos.
            Assert.Contains("Empresa Municipal", cambio.PreviousValue);
            Assert.Contains("Otro nombre", cambio.NewValue);
        }
    }

    // ================= Helpers =================

    private static SaveEntidadRequest NuevaEntidad() => new(
        Nit: "800197268",
        DigitoVerificacion: "4",
        RazonSocial: "Empresa Municipal de Servicios Publicos",
        Sigla: "EMSP",
        TipoEntidad: TipoEntidad.Publica,
        NaturalezaJuridica: "Empresa Industrial y Comercial del Estado",
        CodigoDivipola: "11001",
        PaisId: ColombiaId,
        DepartamentoId: BogotaDeptoId,
        CiudadId: BogotaMunicipioId,
        DireccionPrincipal: "Calle 26 No. 57-83",
        Telefono: "6012203456",
        CorreoInstitucional: "contacto@emsp.gov.co",
        PaginaWeb: "https://www.emsp.gov.co",
        RepresentanteLegal: "Maria Fernanda Rodriguez",
        LogoUrl: null,
        ZonaHoraria: "America/Bogota",
        IdiomaDefecto: "es-CO",
        Estado: EntidadEstado.Activo);

    private async Task<long> NewTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    private async Task<T> RunAsync<T>(long tenantId, Func<IEntidadService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        return await action(new EntidadService(ctx, new TestTenantContext(tenantId), new NoOpAuditWriter()));
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
            // La escritura real se verifica aparte, con el AuditWriter autentico.
        }
    }
}

/// <summary>Motor PostgreSQL (contenedor efimero postgres:16-alpine, Testcontainers).</summary>
public sealed class DatosEntidadTests_Postgres
    : DatosEntidadTestsBase, IClassFixture<PostgresTenantIsolationFixture>
{
    public DatosEntidadTests_Postgres(PostgresTenantIsolationFixture fixture) : base(fixture) { }
}
