using Tronox.Application.Tenancy;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Validacion PURA del funcionario (RQ01 - RF06), sin base de datos.
///
/// Lo que importa fijar aqui es el criterio 2 de 5.6.3: para ACTIVARSE hacen falta DEPENDENCIA,
/// CARGO y al menos un ROL. Y en particular que la dependencia que se exige es la DERIVADA del
/// cargo (ADR-003, Addendum): un cargo colgado de la raiz no da area documental, y ese caso debe
/// resolver a "no activable" en vez de a "activable sin restricciones".
/// </summary>
public class FuncionarioRulesTests
{
    private const long Cargo = 10;
    private const long Dependencia = 20;

    // ---- Datos personales (5.6.1) ----

    private static string? Datos(
        TipoDocumento? tipo = TipoDocumento.CC,
        string? numero = "1020304050",
        string? nombres = "Ana Maria",
        string? apellidos = "Gomez Perez",
        string? correo = "ana.gomez@entidad.gov.co",
        string? telefono = "3001234567")
        => FuncionarioRules.ValidateDatos(tipo, numero, nombres, apellidos, correo, telefono);

    [Fact]
    public void DatosCompletos_SonValidos()
    {
        Assert.Null(Datos());
    }

    [Fact]
    public void TipoYNumeroDeDocumento_SonObligatorios()
    {
        Assert.NotNull(Datos(tipo: null));
        Assert.NotNull(Datos(numero: null));
        Assert.NotNull(Datos(numero: "   "));
    }

    [Fact]
    public void NombresYApellidos_SonObligatorios()
    {
        Assert.NotNull(Datos(nombres: ""));
        Assert.NotNull(Datos(apellidos: ""));
    }

    [Fact]
    public void Documento_NoSupera20Caracteres()
    {
        Assert.NotNull(Datos(numero: new string('9', 21)));
        Assert.Null(Datos(numero: new string('9', 20)));
    }

    [Theory]
    [InlineData("sin-arroba.gov.co")]
    [InlineData("@entidad.gov.co")]
    [InlineData("ana@")]
    [InlineData("ana@entidad")]
    [InlineData("ana perez@entidad.gov.co")]
    [InlineData("doble@arroba@entidad.gov.co")]
    public void Correo_Invalido_SeRechaza(string correo)
    {
        Assert.NotNull(Datos(correo: correo));
    }

    [Fact]
    public void Correo_SeNormalizaAMinusculas_PorqueEsElLogin()
    {
        Assert.Equal("ana.gomez@entidad.gov.co",
            FuncionarioRules.NormalizarCorreo("  Ana.Gomez@Entidad.Gov.CO  "));
    }

    // ---- Criterio 2 de 5.6.3: dependencia + cargo + rol ----

    [Fact]
    public void PuedeActivarse_ConCargoDependenciaYRol()
    {
        Assert.Null(FuncionarioRules.ValidatePuedeActivar(Cargo, Dependencia, rolesVigentes: 1));
    }

    [Fact]
    public void NoPuedeActivarse_SinCargo()
    {
        var error = FuncionarioRules.ValidatePuedeActivar(null, null, rolesVigentes: 2);
        Assert.NotNull(error);
        Assert.Contains("cargo", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoPuedeActivarse_SinDependenciaDerivada_AunqueTengaCargoYRol()
    {
        // Cargo colgado de la raiz: el resolver devuelve null. FAIL-CLOSED (ADR-003, Addendum
        // punto 3): sin area documental NO se activa; jamas se interpreta como "ve todo".
        var error = FuncionarioRules.ValidatePuedeActivar(Cargo, dependenciaDerivadaId: null, rolesVigentes: 1);
        Assert.NotNull(error);
        Assert.Contains("dependencia", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoPuedeActivarse_SinNingunRol()
    {
        var error = FuncionarioRules.ValidatePuedeActivar(Cargo, Dependencia, rolesVigentes: 0);
        Assert.NotNull(error);
        Assert.Contains("rol", error, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Estados que dan acceso (5.6.2) ----

    [Theory]
    [InlineData(PlatformUserStatus.Inactive)]
    [InlineData(PlatformUserStatus.Blocked)]
    [InlineData(PlatformUserStatus.Suspended)]
    [InlineData(PlatformUserStatus.Invited)]
    [InlineData(PlatformUserStatus.PendingActivation)]
    public void SoloElEstadoActivo_PermiteIniciarSesion(PlatformUserStatus estado)
    {
        Assert.False(FuncionarioRules.PuedeIniciarSesion(estado));
        Assert.True(FuncionarioRules.PuedeIniciarSesion(PlatformUserStatus.Active));
    }
}
