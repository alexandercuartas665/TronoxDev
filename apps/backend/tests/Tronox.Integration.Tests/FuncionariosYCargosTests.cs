using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Organization;
using Tronox.Application.Roles;
using Tronox.Application.Tenancy;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Tronox.Infrastructure.Auth;

namespace Tronox.Integration.Tests;

/// <summary>
/// RQ01 - RF04 (Catalogo de Cargos) y RF06 (Usuarios / Funcionarios) contra PostgreSQL real
/// (Testcontainers). Cubre lo que NO se puede comprobar sin base de datos:
///
/// 1. el correo es unico POR TENANT (es el login) y el documento tambien;
/// 2. esa unicidad es POR TENANT: otro tenant puede repetir correo y documento;
/// 3. un funcionario nace Invitado y no se activa sin dependencia + cargo + rol;
/// 4. con los tres, se activa;
/// 5. la dependencia se DERIVA del cargo, no se almacena;
/// 6. un cargo con funcionarios ACTIVOS no se puede inactivar (RF04 criterio 3);
/// 7. si el ocupante deja de estar activo, el cargo ya se puede inactivar;
/// 8. inactivar al funcionario CONSERVA su registro (nunca hay borrado).
///
/// Las reglas puras viven en Tronox.Application.Tests (FuncionarioRulesTests, OrgStructureRulesTests).
/// </summary>
public abstract class FuncionariosYCargosTestsBase
{
    private readonly TenantIsolationDbFixture _fixture;
    private static readonly Pbkdf2PasswordHasher Hasher = new();
    private static readonly DateOnly Desde = new(2024, 1, 1);

    protected FuncionariosYCargosTestsBase(TenantIsolationDbFixture fixture) => _fixture = fixture;

    // ================= 1. Unicidad de correo y documento dentro del tenant =================

    [Fact]
    public async Task Correo_EsUnicoPorTenant_PorqueEsElLogin()
    {
        var tenantId = await NewTenantAsync("Correo Unico");
        var correo = $"ana-{Guid.NewGuid():N}@entidad.gov.co";

        var primero = await RunUsersAsync(tenantId, s =>
            s.SaveFuncionarioAsync(Nuevo(correo, "1001"), TestIds.Next()));
        Assert.True(primero.IsOk);

        var repetido = await RunUsersAsync(tenantId, s =>
            s.SaveFuncionarioAsync(Nuevo(correo, "1002"), TestIds.Next()));

        Assert.Equal(TenancyServiceStatus.Conflict, repetido.Status);
        Assert.Contains(correo, repetido.Error);
    }

    [Fact]
    public async Task Documento_EsUnicoPorTenant()
    {
        var tenantId = await NewTenantAsync("Documento Unico");
        var documento = $"D{Guid.NewGuid():N}"[..12];

        Assert.True((await RunUsersAsync(tenantId, s =>
            s.SaveFuncionarioAsync(Nuevo($"uno-{Guid.NewGuid():N}@entidad.gov.co", documento), TestIds.Next()))).IsOk);

        var repetido = await RunUsersAsync(tenantId, s =>
            s.SaveFuncionarioAsync(Nuevo($"dos-{Guid.NewGuid():N}@entidad.gov.co", documento), TestIds.Next()));

        Assert.Equal(TenancyServiceStatus.Conflict, repetido.Status);
        Assert.Contains(documento, repetido.Error);
    }

    [Fact]
    public async Task Correo_Y_Documento_SeRepitenEnOtroTenant()
    {
        var a = await NewTenantAsync("Unicidad A");
        var b = await NewTenantAsync("Unicidad B");
        var correo = $"homonimo-{Guid.NewGuid():N}@entidad.gov.co";
        var documento = $"D{Guid.NewGuid():N}"[..12];

        Assert.True((await RunUsersAsync(a, s => s.SaveFuncionarioAsync(Nuevo(correo, documento), TestIds.Next()))).IsOk);

        // La unicidad es POR TENANT (DAT-01): la misma persona puede existir en dos entidades.
        var enB = await RunUsersAsync(b, s => s.SaveFuncionarioAsync(Nuevo(correo, documento), TestIds.Next()));
        Assert.True(enB.IsOk);

        // Y cada tenant solo ve el suyo.
        Assert.Single(await RunUsersAsync(a, s => s.ListFuncionariosAsync()), f => f.Email == correo);
        Assert.Single(await RunUsersAsync(b, s => s.ListFuncionariosAsync()), f => f.Email == correo);
    }

    // ================= 2. Activacion: dependencia + cargo + rol =================

    [Fact]
    public async Task Funcionario_NaceInvitado_Y_NoSeActivaSinCargo()
    {
        var tenantId = await NewTenantAsync("Activacion Sin Cargo");

        var creado = Ok(await RunUsersAsync(tenantId, s =>
            s.SaveFuncionarioAsync(Nuevo($"sin-cargo-{Guid.NewGuid():N}@entidad.gov.co", "2001"), TestIds.Next())));
        Assert.Equal(PlatformUserStatus.Invited, creado.Status);

        var activar = await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, null, TestIds.Next()));

        Assert.Equal(TenancyServiceStatus.Invalid, activar.Status);
        Assert.Contains("cargo", activar.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Funcionario_NoSeActiva_SiSuCargoNoCuelgaDeUnaDependencia()
    {
        var tenantId = await NewTenantAsync("Activacion Sin Dependencia");
        // Cargo colgado de la RAIZ: el resolver devuelve null -> sin area documental (fail-closed).
        var cargoSuelto = await NewCargoAsync(tenantId, "Cargo huerfano", parentId: null);
        var rolId = await NewRolAsync(tenantId, "Rol basico");

        var creado = Ok(await RunUsersAsync(tenantId, s => s.SaveFuncionarioAsync(
            Nuevo($"huerfano-{Guid.NewGuid():N}@entidad.gov.co", "2002", cargoSuelto), TestIds.Next())));
        await AsignarRolAsync(tenantId, creado.Id, rolId);

        var activar = await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, null, TestIds.Next()));

        Assert.Equal(TenancyServiceStatus.Invalid, activar.Status);
        Assert.Contains("dependencia", activar.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Funcionario_NoSeActiva_SinNingunRol()
    {
        var tenantId = await NewTenantAsync("Activacion Sin Rol");
        var (_, cargoId) = await NewDependenciaConCargoAsync(tenantId);

        var creado = Ok(await RunUsersAsync(tenantId, s => s.SaveFuncionarioAsync(
            Nuevo($"sin-rol-{Guid.NewGuid():N}@entidad.gov.co", "2003", cargoId), TestIds.Next())));

        var activar = await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, null, TestIds.Next()));

        Assert.Equal(TenancyServiceStatus.Invalid, activar.Status);
        Assert.Contains("rol", activar.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Funcionario_SeActiva_ConDependenciaCargoYRol_YLaDependenciaEsDerivada()
    {
        var tenantId = await NewTenantAsync("Activacion Completa");
        var (dependenciaId, cargoId) = await NewDependenciaConCargoAsync(tenantId);
        var rolId = await NewRolAsync(tenantId, "Archivista");

        var creado = Ok(await RunUsersAsync(tenantId, s => s.SaveFuncionarioAsync(
            Nuevo($"completo-{Guid.NewGuid():N}@entidad.gov.co", "2004", cargoId), TestIds.Next())));
        await AsignarRolAsync(tenantId, creado.Id, rolId);

        var activado = Ok(await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, "Alta", TestIds.Next())));

        Assert.Equal(PlatformUserStatus.Active, activado.Status);
        Assert.Null(activado.MotivoNoActivable);
        // La dependencia NO se almacena en el usuario: se deriva subiendo por el cargo (ADR-003).
        Assert.Equal(dependenciaId, activado.DependenciaId);

        await using var ctx = _fixture.CreateContext(tenantId);
        var fila = await ctx.TenantUsers.AsNoTracking().FirstAsync(u => u.Id == creado.Id);
        Assert.Equal(cargoId, fila.CargoOrgUnitId);
        Assert.Null(ctx.Entry(fila).Metadata.FindProperty("DependenciaId"));
    }

    [Fact]
    public async Task Inactivar_ConservaElRegistro_YSusDatos()
    {
        var tenantId = await NewTenantAsync("Inactivar Conserva");
        var (_, cargoId) = await NewDependenciaConCargoAsync(tenantId);
        var rolId = await NewRolAsync(tenantId, "Archivista");
        var correo = $"conserva-{Guid.NewGuid():N}@entidad.gov.co";

        var creado = Ok(await RunUsersAsync(tenantId, s =>
            s.SaveFuncionarioAsync(Nuevo(correo, "2005", cargoId), TestIds.Next())));
        await AsignarRolAsync(tenantId, creado.Id, rolId);
        Assert.True((await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, null, TestIds.Next()))).IsOk);

        var inactivado = Ok(await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Inactive, "Retiro", TestIds.Next())));

        Assert.Equal(PlatformUserStatus.Inactive, inactivado.Status);
        // Criterio 4: nada se borra. Sigue en el listado, con su cargo y sus roles.
        var enLista = Assert.Single(
            await RunUsersAsync(tenantId, s => s.ListFuncionariosAsync()), f => f.Id == creado.Id);
        Assert.Equal(cargoId, enLista.CargoOrgUnitId);
        Assert.Single(enLista.Roles);
    }

    // ================= 3. RF04 criterio 3: cargo en uso no sale del catalogo =================

    [Fact]
    public async Task Cargo_ConFuncionariosActivos_NoSePuedeInactivar()
    {
        var tenantId = await NewTenantAsync("Cargo En Uso");
        var (_, cargoId) = await NewDependenciaConCargoAsync(tenantId);
        var rolId = await NewRolAsync(tenantId, "Archivista");

        var creado = Ok(await RunUsersAsync(tenantId, s => s.SaveFuncionarioAsync(
            Nuevo($"ocupa-{Guid.NewGuid():N}@entidad.gov.co", "3001", cargoId), TestIds.Next())));
        await AsignarRolAsync(tenantId, creado.Id, rolId);
        Assert.True((await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, null, TestIds.Next()))).IsOk);

        var archivar = await RunOrgAsync(tenantId, s =>
            s.SetArchivedAsync(cargoId, archived: true, TestIds.Next()));

        Assert.Equal(OrgServiceStatus.Invalid, archivar.Status);
        Assert.Contains("activo", archivar.Error!, StringComparison.OrdinalIgnoreCase);

        // Y sigue activo en el catalogo: no se ha tocado nada.
        await using var ctx = _fixture.CreateContext(tenantId);
        Assert.False(await ctx.OrgUnits.AsNoTracking().Where(o => o.Id == cargoId).Select(o => o.IsArchived).FirstAsync());
    }

    [Fact]
    public async Task Cargo_SeInactiva_CuandoSuOcupanteYaNoEstaActivo()
    {
        var tenantId = await NewTenantAsync("Cargo Liberado");
        var (_, cargoId) = await NewDependenciaConCargoAsync(tenantId);
        var rolId = await NewRolAsync(tenantId, "Archivista");

        var creado = Ok(await RunUsersAsync(tenantId, s => s.SaveFuncionarioAsync(
            Nuevo($"libera-{Guid.NewGuid():N}@entidad.gov.co", "3002", cargoId), TestIds.Next())));
        await AsignarRolAsync(tenantId, creado.Id, rolId);
        Assert.True((await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Active, null, TestIds.Next()))).IsOk);
        Assert.True((await RunUsersAsync(tenantId, s =>
            s.SetFuncionarioEstadoAsync(creado.Id, PlatformUserStatus.Inactive, "Retiro", TestIds.Next()))).IsOk);

        // Un ocupante INACTIVO ya no bloquea: sus documentos conservan el cargo como metadato.
        var archivar = await RunOrgAsync(tenantId, s =>
            s.SetArchivedAsync(cargoId, archived: true, TestIds.Next()));
        Assert.True(archivar.IsOk);
    }

    // ---- Helpers ----

    private static SaveFuncionarioRequest Nuevo(string correo, string documento, long? cargoId = null)
        => new(null, TipoDocumento.CC, documento, "Ana Maria", "Gomez Perez", correo, "3001234567",
            CargoOrgUnitId: cargoId);

    private static FuncionarioDto Ok(TenancyResult<FuncionarioDto> result)
    {
        Assert.True(result.IsOk, result.Error);
        return result.Value!;
    }

    private async Task<long> NewTenantAsync(string name)
    {
        var tenantId = TestIds.Next();
        await using var ctx = _fixture.CreateContext(tenantId: null);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        await ctx.SaveChangesAsync();
        return tenantId;
    }

    /// <summary>Dependencia (con su fondo) + un Cargo colgando de ella. Devuelve (dependencia, cargo).</summary>
    private async Task<(long DependenciaId, long CargoId)> NewDependenciaConCargoAsync(long tenantId)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var fondo = new Fondo
        {
            TenantId = tenantId,
            CodigoFondo = $"F{TestIds.Next() % 10000}",
            NombreFondo = "Fondo unico",
            FechaApertura = new DateOnly(2020, 1, 1)
        };
        ctx.Fondos.Add(fondo);
        var dependencia = new OrgUnit
        {
            TenantId = tenantId,
            Name = "Gestion Documental",
            Classifier = OrgUnitClassifier.Dependencia,
            Fondo = fondo,
            Codigo = "GD",
            VigenteDesde = Desde
        };
        ctx.OrgUnits.Add(dependencia);
        var cargo = new OrgUnit
        {
            TenantId = tenantId,
            Name = "Profesional Universitario",
            Classifier = OrgUnitClassifier.Cargo,
            Parent = dependencia,
            NivelJerarquico = NivelJerarquico.Profesional
        };
        ctx.OrgUnits.Add(cargo);
        await ctx.SaveChangesAsync();
        return (dependencia.Id, cargo.Id);
    }

    private async Task<long> NewCargoAsync(long tenantId, string nombre, long? parentId)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var cargo = new OrgUnit
        {
            TenantId = tenantId,
            Name = nombre,
            Classifier = OrgUnitClassifier.Cargo,
            ParentId = parentId,
            NivelJerarquico = NivelJerarquico.Profesional
        };
        ctx.OrgUnits.Add(cargo);
        await ctx.SaveChangesAsync();
        return cargo.Id;
    }

    private async Task<long> NewRolAsync(long tenantId, string nombre)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        // Los niveles se siembran con el aprovisionamiento REAL (ver RolesTestHelpers): todo rol
        // exige nivel_acceso_maximo, y definir aqui un nivel a mano duplicaria el catalogo canonico.
        var nivelId = await RolesTestHelpers.SeedNivelesYObtenerIdAsync(
            ctx, tenantId, RolesTestHelpers.NivelPublico);
        var rol = new Rol
        {
            TenantId = tenantId,
            Name = $"{nombre} {TestIds.Next()}",
            NivelAccesoMaximoId = nivelId,
            Estado = RolEstado.Activo
        };
        ctx.Roles.Add(rol);
        await ctx.SaveChangesAsync();
        return rol.Id;
    }

    private async Task AsignarRolAsync(long tenantId, long tenantUserId, long rolId)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        ctx.UsuariosRoles.Add(new UsuarioRol
        {
            TenantId = tenantId,
            TenantUserId = tenantUserId,
            RolId = rolId
        });
        await ctx.SaveChangesAsync();
    }

    private async Task<T> RunUsersAsync<T>(long tenantId, Func<ITenantUserService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var service = new TenantUserService(ctx, new TestTenantContext(tenantId), Hasher, new AuditWriter(ctx));
        return await action(service);
    }

    private async Task<T> RunOrgAsync<T>(long tenantId, Func<IOrgUnitService, Task<T>> action)
    {
        await using var ctx = _fixture.CreateContext(tenantId);
        var service = new OrgUnitService(ctx, new TestTenantContext(tenantId), new AuditWriter(ctx));
        return await action(service);
    }

    private sealed class TestTenantContext(long? tenantId, long? userId = null) : ITenantContext
    {
        public long? TenantId { get; } = tenantId;
        public long? UserId { get; } = userId;
    }
}

/// <summary>Motor PostgreSQL (contenedor efimero postgres:16-alpine, Testcontainers).</summary>
public sealed class FuncionariosYCargosTests_Postgres
    : FuncionariosYCargosTestsBase, IClassFixture<PostgresTenantIsolationFixture>
{
    public FuncionariosYCargosTests_Postgres(PostgresTenantIsolationFixture fixture) : base(fixture) { }
}
