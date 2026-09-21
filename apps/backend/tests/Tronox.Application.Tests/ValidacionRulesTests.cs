using Tronox.Application.Validaciones;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS de las reglas de validacion (RQ04 - RF11/RF12): comentario obligatorio al devolver/
/// rechazar, respuestas validas y dias restantes del semaforo.
/// </summary>
public class ValidacionRulesTests
{
    [Fact]
    public void Aprobar_NoRequiereComentario()
    {
        Assert.False(ValidacionRules.RequiereComentario(EstadoValidacion.Aprobado));
        Assert.Null(ValidacionRules.ValidateRespuesta(EstadoValidacion.Aprobado, null));
    }

    [Theory]
    [InlineData(EstadoValidacion.Devuelto)]
    [InlineData(EstadoValidacion.Rechazado)]
    public void DevolverYRechazar_ExigenComentario(EstadoValidacion estado)
    {
        Assert.True(ValidacionRules.RequiereComentario(estado));
        Assert.NotNull(ValidacionRules.ValidateRespuesta(estado, "  "));
        Assert.Null(ValidacionRules.ValidateRespuesta(estado, "motivo del retorno"));
    }

    [Fact]
    public void Pendiente_NoEsRespuestaValida()
    {
        Assert.False(ValidacionRules.EsRespuestaValida(EstadoValidacion.Pendiente));
        Assert.NotNull(ValidacionRules.ValidateRespuesta(EstadoValidacion.Pendiente, "x"));
    }

    [Fact]
    public void DiasRestantes_CalculaSemaforo()
    {
        var hoy = new DateOnly(2026, 8, 4);
        Assert.Null(ValidacionRules.DiasRestantes(null, hoy));
        Assert.Equal(0, ValidacionRules.DiasRestantes(hoy, hoy));
        Assert.Equal(3, ValidacionRules.DiasRestantes(hoy.AddDays(3), hoy));
        Assert.Equal(-2, ValidacionRules.DiasRestantes(hoy.AddDays(-2), hoy));
    }
}
