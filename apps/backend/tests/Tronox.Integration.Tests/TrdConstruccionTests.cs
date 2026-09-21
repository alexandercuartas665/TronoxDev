using Tronox.Application.Common;
using Tronox.Application.Trd;
using Tronox.Domain.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Integration.Tests;

/// <summary>
/// Construccion de la TRD (RQ02 - RF04) sobre PostgreSQL real (Testcontainers). Cubre:
/// 1. CCD automatico = dependencia + serie;
/// 2. no duplicar la misma serie en la misma dependencia y version (3.4.4-4), pero SI en otra
///    dependencia (personalizacion 3.4.2);
/// 3. no asignar sobre una version Historico/Inactivo (3.4.4-1);
/// 4. solo-lectura sobre Vigente: agregar y editar procedimiento/metadatos SI; editar
///    estructura y eliminar NO (RF01 3.1.3);
/// 5. metadatos del expediente;
/// 6. aislamiento cross-tenant.
/// </summary>
public sealed class TrdConstruccionTests : IClassFixture<PostgresTenantIsolationFixture>
{
    private const long Actor = 1;
    private static readonly DateOnly Vig = new(2026, 1, 1);
    private readonly PostgresTenantIsolationFixture _fixture;

    public TrdConstruccionTests(PostgresTenantIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Asignar_GeneraCcd_NoDuplica_PeroPersonalizaPorDependencia()
    {
        var s = await SeedAsync();
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);

        var a1 = Ok(await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor));
        Assert.Equal("100.05", a1.CodigoCcd); // depA=100, serie=05

        // Misma serie, misma dependencia, misma version: conflicto (3.4.4-4).
        var dup = await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor);
        Assert.Equal(TrdServiceStatus.Conflict, dup.Status);

        // Misma serie, OTRA dependencia: permitido (personalizacion 3.4.2), CCD distinto.
        var a2 = Ok(await svc.AddAsignacionAsync(s.VersionId, Req(s.DepB, s.Serie, tiempoGestion: 3), Actor));
        Assert.Equal("200.05", a2.CodigoCcd);
        Assert.Equal(3, a2.TiempoGestion);
    }

    [Fact]
    public async Task NoSeAsigna_SobreVersionHistoricoOInactivo()
    {
        var s = await SeedAsync(estadoVersion: TrdVersionEstado.Historico);
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);

        var r = await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor);
        Assert.Equal(TrdServiceStatus.Invalid, r.Status);
    }

    [Fact]
    public async Task SoloSeriesActivas_SeAsignan()
    {
        var s = await SeedAsync();
        await using (var seedCtx = _fixture.CreateContext(s.TenantId))
        {
            var serie = await seedCtx.SeriesDocumentales.FirstAsync(x => x.Id == s.Serie);
            serie.Estado = SerieEstado.Inactivo;
            await seedCtx.SaveChangesAsync();
        }
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);

        var r = await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor);
        Assert.Equal(TrdServiceStatus.Invalid, r.Status);
    }

    [Fact]
    public async Task VersionVigente_PermiteAgregarYMetadatos_PeroNoEstructuraNiEliminar()
    {
        var s = await SeedAsync();
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);

        // Se asigna en En Construccion.
        var a = Ok(await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor));

        // Se pasa la version a Vigente.
        await using (var seedCtx = _fixture.CreateContext(s.TenantId))
        {
            var v = await seedCtx.TrdVersiones.FirstAsync(x => x.Id == s.VersionId);
            v.Estado = TrdVersionEstado.Vigente;
            await seedCtx.SaveChangesAsync();
        }

        // Editar estructura (tiempos/disposicion/clasificacion): BLOQUEADO.
        var estructura = await svc.UpdateAsignacionAsync(a.Id, new UpdateAsignacionRequest(
            5, 10, DisposicionFinal.Eliminacion, s.Nivel), Actor);
        Assert.Equal(TrdServiceStatus.Invalid, estructura.Status);

        // Editar procedimiento: PERMITIDO.
        var proc = await svc.UpdateProcedimientoAsync(a.Id, "Nuevo procedimiento", Actor);
        Assert.True(proc.IsOk, proc.Error);

        // Agregar metadato: PERMITIDO.
        var meta = await svc.AddMetadatoAsync(a.Id, new SaveMetadatoRequest(
            "Fecha de ingreso", TipoDatoMetadato.Fecha, true), Actor);
        Assert.True(meta.IsOk, meta.Error);

        // Inactivar la asignacion (eliminar): BLOQUEADO.
        var del = await svc.SetAsignacionArchivedAsync(a.Id, archived: true, Actor);
        Assert.Equal(TrdServiceStatus.Invalid, del.Status);

        // Agregar OTRA serie a la version Vigente: PERMITIDO.
        var b = await svc.AddAsignacionAsync(s.VersionId, Req(s.DepB, s.Serie), Actor);
        Assert.True(b.IsOk, b.Error);
    }

    [Fact]
    public async Task Metadato_TipoLista_ExigeLista()
    {
        var s = await SeedAsync();
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);
        var a = Ok(await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor));

        // Lista sin id: invalido (regla pura, pero via servicio).
        var sinLista = await svc.AddMetadatoAsync(a.Id, new SaveMetadatoRequest("Tipo", TipoDatoMetadato.Lista, false), Actor);
        Assert.Equal(TrdServiceStatus.Invalid, sinLista.Status);

        // Con una lista real: OK.
        long listaId;
        await using (var seedCtx = _fixture.CreateContext(s.TenantId))
        {
            var lista = new ListaMaestra { TenantId = s.TenantId, NombreLista = "Tipo Vinculacion", Estado = ListaEstado.Activo };
            seedCtx.ListasMaestras.Add(lista);
            await seedCtx.SaveChangesAsync();
            listaId = lista.Id;
        }
        var conLista = await svc.AddMetadatoAsync(a.Id, new SaveMetadatoRequest("Tipo", TipoDatoMetadato.Lista, false, listaId), Actor);
        Assert.True(conLista.IsOk, conLista.Error);
        Assert.Equal(listaId, conLista.Value!.ListaMaestraId);
    }

    [Fact]
    public async Task Asignaciones_NoSeVenEntreTenants()
    {
        var a = await SeedAsync();
        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            Ok(await NewService(ctxA, a).AddAsignacionAsync(a.VersionId, Req(a.DepA, a.Serie), Actor));
        }
        var b = await SeedAsync();
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            // El tenant B no ve las asignaciones de A (filtro global). Su version esta vacia.
            var svcB = NewService(ctxB, b);
            var deps = await svcB.GetDependenciasResumenAsync(b.VersionId);
            Assert.All(deps, d => Assert.Equal(0, d.SeriesAsignadas));
        }
    }

    // ---- RF05: tipologias documentales + metadatos de documento ----

    [Fact]
    public async Task Tipologia_CRUD_YMetadatoDocumento()
    {
        var s = await SeedAsync();
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);
        var a = Ok(await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor));

        // Alta de tipologia.
        var tip = await svc.AddTipologiaAsync(a.Id, new SaveTipologiaRequest(
            "Acta de Reunion", SoporteTipologia.Electronico, "PDF", ObligatorioEnExpediente: true), Actor);
        Assert.True(tip.IsOk, tip.Error);
        Assert.Equal(SoporteTipologia.Electronico, tip.Value!.Soporte);
        Assert.True(tip.Value.ObligatorioEnExpediente);

        // Aparece colgada de la asignacion.
        var conTip = (await svc.GetAsignacionesAsync(s.VersionId, s.DepA)).Single(x => x.Id == a.Id);
        Assert.Single(conTip.Tipologias);

        // Metadato de DOCUMENTO sobre la tipologia (contexto Documento).
        var md = await svc.AddMetadatoDocumentoAsync(tip.Value.Id, new SaveMetadatoRequest(
            "Numero de acta", TipoDatoMetadato.Numerico, true), Actor);
        Assert.True(md.IsOk, md.Error);
        Assert.Equal(ContextoMetadato.Documento, md.Value!.Contexto);
        Assert.Equal(tip.Value.Id, md.Value.TrdTipologiaId);

        // El metadato de documento va bajo la tipologia; NO como metadato del expediente.
        var recargada = (await svc.GetAsignacionesAsync(s.VersionId, s.DepA)).Single(x => x.Id == a.Id);
        Assert.Empty(recargada.Metadatos); // expediente vacio
        Assert.Single(recargada.Tipologias[0].Metadatos); // documento con 1

        // Editar y luego inactivar la tipologia.
        var upd = await svc.UpdateTipologiaAsync(tip.Value.Id, new SaveTipologiaRequest(
            "Acta Extraordinaria", SoporteTipologia.Hibrido, "PDF", false), Actor);
        Assert.True(upd.IsOk, upd.Error);
        Assert.Equal(SoporteTipologia.Hibrido, upd.Value!.Soporte);

        var arch = await svc.SetTipologiaArchivedAsync(tip.Value.Id, archived: true, Actor);
        Assert.True(arch.IsOk, arch.Error);
        var soloActivas = (await svc.GetAsignacionesAsync(s.VersionId, s.DepA)).Single(x => x.Id == a.Id);
        Assert.All(soloActivas.Tipologias, t => Assert.True(t.IsArchived));
    }

    [Fact]
    public async Task Tipologia_NoSeAgrega_SobreVersionHistorico()
    {
        var s = await SeedAsync();
        await using var ctx = _fixture.CreateContext(s.TenantId);
        var svc = NewService(ctx, s);
        var a = Ok(await svc.AddAsignacionAsync(s.VersionId, Req(s.DepA, s.Serie), Actor));

        await using (var seedCtx = _fixture.CreateContext(s.TenantId))
        {
            var v = await seedCtx.TrdVersiones.FirstAsync(x => x.Id == s.VersionId);
            v.Estado = TrdVersionEstado.Historico;
            await seedCtx.SaveChangesAsync();
        }

        var r = await svc.AddTipologiaAsync(a.Id, new SaveTipologiaRequest("X", SoporteTipologia.Fisico), Actor);
        Assert.Equal(TrdServiceStatus.Invalid, r.Status);
    }

    [Fact]
    public async Task Tipologias_NoSeVenEntreTenants()
    {
        var a = await SeedAsync();
        await using (var ctxA = _fixture.CreateContext(a.TenantId))
        {
            var svcA = NewService(ctxA, a);
            var asigA = Ok(await svcA.AddAsignacionAsync(a.VersionId, Req(a.DepA, a.Serie), Actor));
            var tipA = await svcA.AddTipologiaAsync(asigA.Id, new SaveTipologiaRequest("Acta", SoporteTipologia.Electronico), Actor);
            Assert.True(tipA.IsOk, tipA.Error);
        }
        var b = await SeedAsync();
        await using (var ctxB = _fixture.CreateContext(b.TenantId))
        {
            // El tenant B no ve nada de A: su version esta vacia de asignaciones (y por ende de tipologias).
            var svcB = NewService(ctxB, b);
            var asigB = await svcB.GetAsignacionesAsync(b.VersionId, b.DepA);
            Assert.Empty(asigB);
        }
    }

    // ================= Helpers =================

    private AddAsignacionRequest Req(long depId, long serieId, int tiempoGestion = 2)
        => new(depId, serieId, tiempoGestion, 8, DisposicionFinal.ConservacionTotal, _nivel);

    private long _nivel; // nivel de clasificacion del seed vigente

    private static TrdAsignacionDto Ok(TrdResult<TrdAsignacionDto> r)
    {
        Assert.True(r.IsOk, r.Error);
        return r.Value!;
    }

    private static TrdConstruccionService NewService(IApplicationDbContext ctx, SeedData seed)
        => new(ctx, new TestTenantContext(seed.TenantId, 1), new NoOpAuditWriter());

    private async Task<SeedData> SeedAsync(TrdVersionEstado estadoVersion = TrdVersionEstado.EnConstruccion)
    {
        var tenantId = TestIds.Next();
        await using (var ctx = _fixture.CreateContext(tenantId: null))
        {
            ctx.Tenants.Add(new Tenant { Id = tenantId, Name = $"TRD {tenantId}" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.CreateContext(tenantId))
        {
            var depA = new OrgUnit { TenantId = tenantId, Name = "Secretaria General", Codigo = "100", Classifier = OrgUnitClassifier.Dependencia };
            var depB = new OrgUnit { TenantId = tenantId, Name = "Direccion Juridica", Codigo = "200", Classifier = OrgUnitClassifier.Dependencia };
            ctx.OrgUnits.AddRange(depA, depB);

            var serie = new SerieDocumental { TenantId = tenantId, Codigo = "05", Nombre = "Actas", Estado = SerieEstado.Activo };
            ctx.SeriesDocumentales.Add(serie);

            var nivel = new NivelClasificacion { TenantId = tenantId, Nombre = "Interno", Codigo = "02", NivelOrden = 2, Activo = true };
            ctx.NivelesClasificacion.Add(nivel);

            var version = new TrdVersion { TenantId = tenantId, CodigoVersion = "TRD-2026-v1", FechaVigenciaDesde = Vig, Estado = estadoVersion };
            ctx.TrdVersiones.Add(version);

            await ctx.SaveChangesAsync();
            _nivel = nivel.Id;
            return new SeedData(tenantId, version.Id, depA.Id, depB.Id, serie.Id, nivel.Id);
        }
    }

    private sealed record SeedData(long TenantId, long VersionId, long DepA, long DepB, long Serie, long Nivel);

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }

    private sealed class NoOpAuditWriter : IAuditWriter
    {
        public void Write(long actorUserId, string actionName, string entityName, long? entityId,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human) { }
        public void Write(long actorUserId, string actionName, string entityName, BaseEntity entity,
            object? previousValue, object? newValue, long? tenantId = null, string? reason = null,
            AuditActorType actorType = AuditActorType.Human) { }
    }
}
