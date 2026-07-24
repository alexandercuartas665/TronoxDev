using Tronox.Application.Archivistica;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS (sin base de datos) de las reglas de "Datos de la Entidad"
/// (RQ01 - RF01 seccion 4.1.1 y resolucion M01):
///
/// - digito de verificacion del NIT (algoritmo DIAN);
/// - generacion del codigo de fondo AGN CO-{DIVIPOLA}-{SIGLA};
/// - obligatoriedad condicional de DIVIPOLA y AGN si tipo_entidad = Publica;
/// - limite de 10 caracteres de la sigla (M01, no los 20 de la spec).
///
/// La regla "una sola entidad por tenant" necesita base de datos y se cubre en
/// Tronox.Integration.Tests/DatosEntidadTests.
/// </summary>
public class EntidadRulesTests
{
    // ================= Digito de verificacion del NIT =================

    [Theory]
    // Caso de referencia publico: NIT 800197268 -> DV 4.
    [InlineData("800197268", 4)]
    [InlineData("900123456", 8)]
    // Las dos ramas del residuo: 0 y 1 se devuelven tal cual, el resto se resta de 11.
    [InlineData("15", 0)]
    [InlineData("4", 1)]
    public void DigitoVerificacion_SeCalculaConElAlgoritmoDeLaDian(string nit, int esperado)
    {
        Assert.Equal(esperado, EntidadRules.CalcularDigitoVerificacion(nit));
    }

    [Fact]
    public void DigitoVerificacion_IgnoraPuntosGuionesYEspacios()
    {
        // El usuario escribe "800.197.268"; es el mismo NIT que "800197268".
        Assert.Equal(
            EntidadRules.CalcularDigitoVerificacion("800197268"),
            EntidadRules.CalcularDigitoVerificacion("800.197.268"));
        Assert.Equal("800197268", EntidadRules.NormalizarNit("800 197 268"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC")]
    // 16 digitos: supera el maximo de 15 de la spec.
    [InlineData("1234567890123456")]
    public void DigitoVerificacion_EsNuloCuandoElNitNoEsUtilizable(string? nit)
    {
        Assert.Null(EntidadRules.CalcularDigitoVerificacion(nit));
    }

    [Fact]
    public void DigitoVerificacion_RechazaElDigitoEquivocado()
    {
        Assert.True(EntidadRules.DigitoVerificacionCoincide("800197268", "4"));
        Assert.False(EntidadRules.DigitoVerificacionCoincide("800197268", "7"));
        Assert.False(EntidadRules.DigitoVerificacionCoincide("800197268", ""));
        Assert.False(EntidadRules.DigitoVerificacionCoincide("800197268", "44"));
    }

    [Fact]
    public void Validate_RechazaElNitConDigitoVerificadorIncorrecto_YDiceCualEs()
    {
        var error = Validar(nit: "800197268", dv: "7");

        Assert.NotNull(error);
        Assert.Contains("digito de verificacion", error);
        Assert.Contains("4", error);
    }

    [Fact]
    public void Validate_AceptaElNitConSuDigitoVerificadorCorrecto()
    {
        Assert.Null(Validar(nit: "800197268", dv: "4"));
    }

    // ================= Codigo de fondo AGN =================

    [Fact]
    public void CodigoFondoAgn_SeGeneraConElPatronDeLaGuiaMinTic()
    {
        Assert.Equal("CO-11001-EMSP", EntidadRules.GenerarCodigoFondoAgn("11001", "emsp"));
        Assert.Equal("CO-05001-TRONO", EntidadRules.GenerarCodigoFondoAgn(" 05001 ", " Trono "));
    }

    [Theory]
    [InlineData(null, "EMSP")]
    [InlineData("", "EMSP")]
    [InlineData("11001", null)]
    [InlineData("11001", "  ")]
    public void CodigoFondoAgn_EsNuloSiFaltaAlgunaDeLasDosPiezas(string? divipola, string? sigla)
    {
        // Un codigo a medias es peor que ninguno: se estampa como metadato raiz en todo expediente.
        Assert.Null(EntidadRules.GenerarCodigoFondoAgn(divipola, sigla));
    }

    [Fact]
    public void Sigla_NoPuedeSuperar10Caracteres_ResolucionM01()
    {
        // La spec decia 20; M01 lo baja a 10 porque la sigla entra literal en el codigo AGN.
        Assert.Equal(10, EntidadRules.MaxSigla);

        var error = Validar(sigla: "ABCDEFGHIJK"); // 11
        Assert.NotNull(error);
        Assert.Contains("sigla", error);
        Assert.Contains("10", error);

        Assert.Null(Validar(sigla: "ABCDEFGHIJ")); // 10 exactos
    }

    // ================= Obligatoriedad condicional (criterio 4 de RF01) =================

    [Fact]
    public void EntidadPublica_ExigeDivipola()
    {
        var error = Validar(tipo: TipoEntidad.Publica, divipola: "", agn: "");

        Assert.NotNull(error);
        Assert.Contains("DIVIPOLA", error);
        Assert.Contains("Publica", error);
    }

    [Fact]
    public void EntidadPublica_ExigeCodigoFondoAgn()
    {
        var error = Validar(tipo: TipoEntidad.Publica, divipola: "11001", agn: "");

        Assert.NotNull(error);
        Assert.Contains("fondo AGN", error);
        Assert.Contains("Publica", error);
    }

    [Theory]
    [InlineData(TipoEntidad.Privada)]
    [InlineData(TipoEntidad.Mixta)]
    public void EntidadNoPublica_NoExigeDivipolaNiAgn(TipoEntidad tipo)
    {
        Assert.False(EntidadRules.RequiereDatosAgn(tipo));
        Assert.Null(Validar(tipo: tipo, divipola: "", agn: ""));
    }

    [Fact]
    public void RequiereDatosAgn_SoloEsCiertoParaLaEntidadPublica()
    {
        Assert.True(EntidadRules.RequiereDatosAgn(TipoEntidad.Publica));
        Assert.False(EntidadRules.RequiereDatosAgn(TipoEntidad.Privada));
        Assert.False(EntidadRules.RequiereDatosAgn(TipoEntidad.Mixta));
    }

    [Fact]
    public void Divipola_DebeTenerCincoDigitos()
    {
        Assert.Contains("5 digitos", Validar(divipola: "110", agn: "CO-110-EMSP")!);
        Assert.Contains("5 digitos", Validar(divipola: "1100A", agn: "CO-1100A-EMSP")!);
        Assert.Null(Validar(divipola: "11001", agn: "CO-11001-EMSP"));
    }

    // ================= Resto de campos obligatorios =================

    [Fact]
    public void Validate_ExigeLosCamposObligatoriosDeLaSpec()
    {
        Assert.Contains("razon social", Validar(razonSocial: " ")!);
        Assert.Contains("representante legal", Validar(representante: "")!);
        Assert.Contains("pais", Validar(paisId: null)!);
        Assert.Contains("departamento", Validar(departamentoId: null)!);
        Assert.Contains("ciudad", Validar(ciudadId: null)!);
        Assert.Contains("direccion", Validar(direccion: "")!);
        Assert.Contains("correo institucional", Validar(correo: "")!);
        Assert.Contains("zona horaria", Validar(zona: "")!);
        Assert.Contains("idioma", Validar(idioma: "")!);
    }

    [Fact]
    public void Validate_RechazaCorreoYUrlMalFormados()
    {
        Assert.Contains("correo institucional", Validar(correo: "no-es-un-correo")!);
        Assert.Contains("URL valida", Validar(web: "www.entidad.gov.co")!);
        Assert.Null(Validar(web: "https://www.entidad.gov.co"));
    }

    [Fact]
    public void MaxLogo_SonCincoMegabytes()
    {
        Assert.Equal(5L * 1024 * 1024, EntidadRules.MaxLogoBytes);
    }

    // ================= Helper =================

    /// <summary>
    /// Entidad valida por defecto; cada test cambia SOLO el campo que esta probando. Asi un
    /// fallo apunta al campo y no a "algo del formulario".
    /// </summary>
    private static string? Validar(
        string nit = "800197268",
        string dv = "4",
        string razonSocial = "Empresa Municipal de Servicios Publicos",
        string sigla = "EMSP",
        TipoEntidad tipo = TipoEntidad.Publica,
        string? naturaleza = null,
        string divipola = "11001",
        long? paisId = 1,
        long? departamentoId = 3,
        long? ciudadId = 3,
        string direccion = "Calle 26 No. 57-83",
        string? telefono = "6012203456",
        string correo = "contacto@emsp.gov.co",
        string? web = null,
        string representante = "Maria Fernanda Rodriguez",
        string? agn = "CO-11001-EMSP",
        string zona = "America/Bogota",
        string idioma = "es-CO")
        => EntidadRules.Validate(
            nit, dv, razonSocial, sigla, tipo, naturaleza, divipola,
            paisId, departamentoId, ciudadId, direccion, telefono, correo, web,
            representante, agn, zona, idioma);
}
