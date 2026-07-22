using Tronox.Application.Archivistica;
using Tronox.Application.Roles;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS del catalogo canonico de roles predeterminados (RQ01 - RF05). Comprueban que la
/// definicion que consume el aprovisionamiento del alta del tenant es exactamente la de la spec:
/// los 6 roles de negocio con su nivel, mas el rol tecnico "Lider de Dependencia" (DAT-05).
/// </summary>
public class RolCatalogoTests
{
    [Fact]
    public void Catalogo_TieneLosSieteRolesDeLaSpec()
    {
        Assert.Equal(7, RolCatalogo.Roles.Count);

        var porNombre = RolCatalogo.Roles.ToDictionary(r => r.Nombre, StringComparer.Ordinal);
        Assert.Contains("Super Administrador", porNombre.Keys);
        Assert.Contains("Administrador", porNombre.Keys);
        Assert.Contains("Administrador de Archivo", porNombre.Keys);
        Assert.Contains("Radicador", porNombre.Keys);
        Assert.Contains("Archivista", porNombre.Keys);
        Assert.Contains("Consulta General", porNombre.Keys);
        Assert.Contains("Lider de Dependencia", porNombre.Keys);
    }

    [Theory]
    // Nivel maximo de cada rol segun la tabla de la spec.
    [InlineData("Super Administrador", "Clasificado")]
    [InlineData("Administrador", "Clasificado")]
    [InlineData("Administrador de Archivo", "Reservado")]
    [InlineData("Radicador", "Interno")]
    [InlineData("Archivista", "Reservado")]
    [InlineData("Consulta General", "Publico")]
    public void Catalogo_CadaRolApuntaAlNivelDeLaSpec(string rol, string nivelEsperado)
    {
        var semilla = RolCatalogo.Roles.Single(r => r.Nombre == rol);
        var nivel = NivelClasificacionCatalogo.Niveles.Single(n => n.Codigo == semilla.NivelCodigo);

        Assert.Equal(nivelEsperado, nivel.Nombre);
    }

    [Fact]
    public void Catalogo_TodosLosNivelesReferenciadosExisten()
    {
        // Blindaje: si alguien cambia un codigo de nivel, la siembra fallaria en silencio
        // (nivel_acceso_maximo es FK obligatorio) y el tenant naceria sin ese rol.
        var codigos = NivelClasificacionCatalogo.Niveles.Select(n => n.Codigo).ToHashSet(StringComparer.Ordinal);

        foreach (var semilla in RolCatalogo.Roles)
        {
            Assert.Contains(semilla.NivelCodigo, codigos);
        }
    }

    [Fact]
    public void LiderDeDependencia_EsRenombrable_YEsElUnico()
    {
        // DAT-05: el rol tecnico se renombra (cada entidad lo llama distinto) pero no se elimina.
        var lider = RolCatalogo.Roles.Single(r => r.CodigoSistema == RolCatalogo.CodigoLiderDependencia);
        Assert.True(lider.AllowRename);
        Assert.Equal("Lider de Dependencia", lider.Nombre);

        // Ningun otro rol predeterminado puede renombrarse.
        foreach (var otro in RolCatalogo.Roles.Where(r => r.CodigoSistema != RolCatalogo.CodigoLiderDependencia))
        {
            Assert.False(otro.AllowRename, $"{otro.Nombre} no deberia ser renombrable");
        }
    }

    [Fact]
    public void SoloLosRolesDeGobierno_NacenConMatrizCompleta()
    {
        // Es lo que sustituye al bypass "AllowAll" del backbone: el acceso total del Super
        // Administrador es una MATRIZ, no una excepcion en el codigo. Y no se reparte a nadie mas.
        var conMatriz = RolCatalogo.Roles.Where(r => r.MatrizCompleta).Select(r => r.CodigoSistema).ToList();

        Assert.Equal(
            [RolCatalogo.CodigoSuperAdministrador, RolCatalogo.CodigoAdministrador],
            conMatriz);
    }

    [Fact]
    public void CodigosDeSistema_SonUnicos_YSonLaIdentidadDeLaSiembra()
    {
        // La siembra es idempotente por CodigoSistema; si se repitieran, sembraria de menos.
        Assert.Equal(RolCatalogo.Roles.Count, RolCatalogo.Codigos.Count);
        Assert.All(RolCatalogo.Roles, r => Assert.False(string.IsNullOrWhiteSpace(r.CodigoSistema)));
    }

    [Fact]
    public void Nombres_CabenEnLaColumna()
    {
        // nombre_rol es 100 y descripcion 300 (spec). La siembra no puede violar su propio esquema.
        Assert.All(RolCatalogo.Roles, r =>
        {
            Assert.InRange(r.Nombre.Length, 1, 100);
            Assert.InRange(r.Descripcion.Length, 1, 300);
        });
    }
}
