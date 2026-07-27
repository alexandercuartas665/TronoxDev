using Tronox.Application.TrdVersiones;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Validacion PURA de las versiones de TRD (RQ02 - RF01), sin base de datos: campos obligatorios y
/// acotados, y la maquina de estados (editar/activar/descartar solo desde EnConstruccion).
/// </summary>
public class TrdVersionRulesTests
{
    private static readonly DateOnly Vigencia = new(2026, 1, 1);

    // ---- ValidateVersion ----

    [Fact]
    public void VersionValida_NoDaError()
        => Assert.Null(TrdVersionRules.ValidateVersion("TRD-2026-v1", "Primera version", "Resolucion 001 de 2026", Vigencia));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CodigoVacio_EsInvalido(string? codigo)
        => Assert.NotNull(TrdVersionRules.ValidateVersion(codigo, null, null, Vigencia));

    [Fact]
    public void CodigoLargo_EsInvalido()
        => Assert.NotNull(TrdVersionRules.ValidateVersion(new string('X', 51), null, null, Vigencia));

    [Fact]
    public void SinFechaVigencia_EsInvalido()
        => Assert.NotNull(TrdVersionRules.ValidateVersion("TRD-2026-v1", null, null, default));

    [Fact]
    public void DescripcionLarga_EsInvalida()
        => Assert.NotNull(TrdVersionRules.ValidateVersion("TRD-2026-v1", new string('D', 301), null, Vigencia));

    [Fact]
    public void ActoLargo_EsInvalido()
        => Assert.NotNull(TrdVersionRules.ValidateVersion("TRD-2026-v1", null, new string('A', 201), Vigencia));

    // ---- Maquina de estados ----

    [Fact]
    public void SoloSeEdita_EnConstruccion()
    {
        Assert.Null(TrdVersionRules.CanEditar(TrdVersionEstado.EnConstruccion));
        Assert.NotNull(TrdVersionRules.CanEditar(TrdVersionEstado.Vigente));
        Assert.NotNull(TrdVersionRules.CanEditar(TrdVersionEstado.Historico));
        Assert.NotNull(TrdVersionRules.CanEditar(TrdVersionEstado.Inactivo));
    }

    [Fact]
    public void SoloSeActiva_EnConstruccion()
    {
        Assert.Null(TrdVersionRules.CanActivar(TrdVersionEstado.EnConstruccion));
        Assert.NotNull(TrdVersionRules.CanActivar(TrdVersionEstado.Vigente));
        Assert.NotNull(TrdVersionRules.CanActivar(TrdVersionEstado.Historico));
    }

    [Fact]
    public void SoloSeDescarta_EnConstruccion()
    {
        Assert.Null(TrdVersionRules.CanDescartar(TrdVersionEstado.EnConstruccion));
        Assert.NotNull(TrdVersionRules.CanDescartar(TrdVersionEstado.Vigente));
    }
}
