using Tronox.Application.Trd;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Logica PURA de la construccion de la TRD (RQ02 - RF04): CCD compuesto, validaciones y la maquina
/// de permisos de edicion segun el estado de la version (RF01 3.1.3).
/// </summary>
public class TrdConstruccionRulesTests
{
    // ---- CCD ----

    [Theory]
    [InlineData("100", "05", "100.05")]
    [InlineData("100", "05.1", "100.05.1")]
    [InlineData("200", "05", "200.05")]
    [InlineData(" 100 ", " 05 ", "100.05")]
    public void ComponerCodigoCcd_ConcatenaDependenciaYSerie(string dep, string serie, string esperado)
        => Assert.Equal(esperado, TrdConstruccionRules.ComponerCodigoCcd(dep, serie));

    // ---- ValidateReglas ----

    [Fact]
    public void Reglas_TiemposNoNegativos_Validas()
        => Assert.Null(TrdConstruccionRules.ValidateReglas(2, 8, "proc"));

    [Fact]
    public void Reglas_SinLimiteMaximo_Validas()
        => Assert.Null(TrdConstruccionRules.ValidateReglas(500, 999, null)); // AGN 2.5: sin tope

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Reglas_TiempoNegativo_Invalidas(int gestion, int central)
        => Assert.NotNull(TrdConstruccionRules.ValidateReglas(gestion, central, null));

    [Fact]
    public void Reglas_ProcedimientoLargo_Invalido()
        => Assert.NotNull(TrdConstruccionRules.ValidateReglas(1, 1, new string('P', 1001)));

    // ---- ValidateMetadato ----

    [Fact]
    public void Metadato_Valido()
        => Assert.Null(TrdConstruccionRules.ValidateMetadato("Fecha de ingreso", TipoDatoMetadato.Fecha, null));

    [Fact]
    public void Metadato_SinNombre_Invalido()
        => Assert.NotNull(TrdConstruccionRules.ValidateMetadato("  ", TipoDatoMetadato.TextoCorto, null));

    [Fact]
    public void Metadato_ListaSinLista_Invalido()
        => Assert.NotNull(TrdConstruccionRules.ValidateMetadato("Tipo", TipoDatoMetadato.Lista, null));

    [Fact]
    public void Metadato_NoListaConLista_Invalido()
        => Assert.NotNull(TrdConstruccionRules.ValidateMetadato("Tipo", TipoDatoMetadato.TextoCorto, 5));

    [Fact]
    public void Metadato_ListaConLista_Valido()
        => Assert.Null(TrdConstruccionRules.ValidateMetadato("Vinculacion", TipoDatoMetadato.Lista, 5));

    // ---- Maquina de permisos (RF01 3.1.3) ----

    [Theory]
    [InlineData(TrdVersionEstado.EnConstruccion, true)]
    [InlineData(TrdVersionEstado.Vigente, true)]
    [InlineData(TrdVersionEstado.Historico, false)]
    [InlineData(TrdVersionEstado.Inactivo, false)]
    public void PermiteAgregar_SegunEstado(TrdVersionEstado estado, bool esperado)
        => Assert.Equal(esperado, TrdConstruccionRules.PermiteAgregar(estado));

    [Theory]
    [InlineData(TrdVersionEstado.EnConstruccion, true)]
    [InlineData(TrdVersionEstado.Vigente, false)]
    [InlineData(TrdVersionEstado.Historico, false)]
    public void PermiteEditarEstructura_SoloEnConstruccion(TrdVersionEstado estado, bool esperado)
        => Assert.Equal(esperado, TrdConstruccionRules.PermiteEditarEstructura(estado));

    [Theory]
    [InlineData(TrdVersionEstado.EnConstruccion, true)]
    [InlineData(TrdVersionEstado.Vigente, true)]
    [InlineData(TrdVersionEstado.Historico, false)]
    public void PermiteEditarProcedimientoYMetadatos_EnConstruccionOVigente(TrdVersionEstado estado, bool esperado)
        => Assert.Equal(esperado, TrdConstruccionRules.PermiteEditarProcedimientoYMetadatos(estado));

    [Theory]
    [InlineData(TrdVersionEstado.EnConstruccion, true)]
    [InlineData(TrdVersionEstado.Vigente, false)]
    public void PermiteEliminar_SoloEnConstruccion(TrdVersionEstado estado, bool esperado)
        => Assert.Equal(esperado, TrdConstruccionRules.PermiteEliminar(estado));
}
