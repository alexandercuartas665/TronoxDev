using Tronox.Application.Archivistica;
using Tronox.Domain.Enums;

namespace Tronox.Application.Tests;

/// <summary>
/// Tests PUROS (sin base de datos) de las reglas de configuracion archivistica
/// (RQ01 - RF01-P.3 / RF01 4.1.2 / RF02): validaciones de fondo, sede, subfondo y nivel de
/// clasificacion, mas la definicion canonica de los 4 niveles que siembra el alta del tenant.
///
/// Lo que necesita base de datos (unicidad por tenant, dependencias, siembra idempotente real)
/// se cubre en Tronox.Integration.Tests/ConfiguracionArchivisticaTests.
/// </summary>
public class ArchivisticaRulesTests
{
    private static readonly DateOnly Apertura = new(2020, 1, 15);

    // ---- Fondo: fecha de cierre ----

    [Fact]
    public void Fondo_Cerrado_RequiereFechaCierre()
    {
        var error = ArchivisticaRules.ValidateFondo(
            "F01", "Fondo Alcaldia", null, FondoTipo.Activo, FondoEstado.Cerrado,
            Apertura, fechaCierre: null, entidadOrigen: null);

        Assert.NotNull(error);
        Assert.Contains("fecha de cierre es obligatoria", error);
    }

    [Fact]
    public void Fondo_Cerrado_FechaCierreDebeSerPosteriorALaApertura()
    {
        // Anterior.
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            "F01", "Fondo Alcaldia", null, FondoTipo.Activo, FondoEstado.Cerrado,
            Apertura, Apertura.AddDays(-1), null));

        // Igual: tampoco vale, la regla exige POSTERIOR.
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            "F01", "Fondo Alcaldia", null, FondoTipo.Activo, FondoEstado.Cerrado,
            Apertura, Apertura, null));

        // Posterior: valido.
        Assert.Null(ArchivisticaRules.ValidateFondo(
            "F01", "Fondo Alcaldia", null, FondoTipo.Activo, FondoEstado.Cerrado,
            Apertura, Apertura.AddDays(1), null));
    }

    [Fact]
    public void Fondo_NoCerrado_NoExigeFechaCierre()
    {
        Assert.Null(ArchivisticaRules.ValidateFondo(
            "F01", "Fondo Alcaldia", null, FondoTipo.Activo, FondoEstado.Activo,
            Apertura, fechaCierre: null, entidadOrigen: null));
    }

    // ---- Fondo: entidad de origen ----

    [Fact]
    public void Fondo_Acumulado_RequiereEntidadOrigen()
    {
        var error = ArchivisticaRules.ValidateFondo(
            "F02", "Fondo acumulado", null, FondoTipo.Acumulado, FondoEstado.Activo,
            Apertura, null, entidadOrigen: null);

        Assert.NotNull(error);
        Assert.Contains("entidad de origen es obligatoria", error);

        // Vacio o solo espacios tampoco cuenta.
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            "F02", "Fondo acumulado", null, FondoTipo.Acumulado, FondoEstado.Activo,
            Apertura, null, "   "));

        // Con entidad de origen: valido.
        Assert.Null(ArchivisticaRules.ValidateFondo(
            "F02", "Fondo acumulado", null, FondoTipo.Acumulado, FondoEstado.Activo,
            Apertura, null, "Instituto Departamental liquidado"));
    }

    [Theory]
    [InlineData(FondoTipo.Activo)]
    [InlineData(FondoTipo.Transferido)]
    public void Fondo_NoAcumulado_NoExigeEntidadOrigen(FondoTipo tipo)
    {
        Assert.Null(ArchivisticaRules.ValidateFondo(
            "F03", "Fondo", null, tipo, FondoEstado.Activo, Apertura, null, entidadOrigen: null));
    }

    // ---- Fondo: obligatorios y longitudes ----

    [Fact]
    public void Fondo_CodigoYNombre_SonObligatorios()
    {
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            "  ", "Fondo", null, FondoTipo.Activo, FondoEstado.Activo, Apertura, null, null));
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            "F01", "  ", null, FondoTipo.Activo, FondoEstado.Activo, Apertura, null, null));
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            new string('X', 21), "Fondo", null, FondoTipo.Activo, FondoEstado.Activo, Apertura, null, null));
    }

    [Fact]
    public void Fondo_FechaApertura_EsObligatoria()
    {
        Assert.NotNull(ArchivisticaRules.ValidateFondo(
            "F01", "Fondo", null, FondoTipo.Activo, FondoEstado.Activo,
            fechaApertura: default, fechaCierre: null, entidadOrigen: null));
    }

    // ---- Fondo: solo lectura ----

    [Theory]
    [InlineData(FondoEstado.Activo, false)]
    [InlineData(FondoEstado.Inactivo, false)]
    [InlineData(FondoEstado.Cerrado, true)]
    public void Fondo_SoloLectura_SoloCuandoEstaCerrado(FondoEstado estado, bool esperado)
    {
        Assert.Equal(esperado, ArchivisticaRules.EsSoloLectura(estado));
    }

    // ---- Niveles de clasificacion ----

    [Fact]
    public void Catalogo_DeclaraLosCuatroNivelesCanonicos()
    {
        var niveles = NivelClasificacionCatalogo.Niveles;
        Assert.Equal(4, niveles.Count);

        // Nombres, codigos, colores y orden exactos de la spec RF01-P.3.
        Assert.Collection(niveles,
            n => AssertNivel(n, "Publico", "01", "#27AE60", 1),
            n => AssertNivel(n, "Interno", "02", "#2980B9", 2),
            n => AssertNivel(n, "Reservado", "03", "#E67E22", 3),
            n => AssertNivel(n, "Clasificado", "04", "#C0392B", 4));

        // Codigos y ordenes sin repetir: son los indices unicos por tenant.
        Assert.Equal(4, niveles.Select(n => n.Codigo).Distinct().Count());
        Assert.Equal(4, niveles.Select(n => n.NivelOrden).Distinct().Count());
    }

    [Fact]
    public void Catalogo_TodosLosNivelesCanonicos_PasanSuPropiaValidacion()
    {
        foreach (var n in NivelClasificacionCatalogo.Niveles)
        {
            Assert.Null(ArchivisticaRules.ValidateNivelClasificacion(
                n.Nombre, n.Codigo, n.ColorEtiqueta, n.NivelOrden, n.Descripcion));
        }
    }

    [Theory]
    [InlineData("#27AE60", true)]
    [InlineData("#abc123", true)]
    [InlineData("27AE60", false)]   // sin almohadilla
    [InlineData("#27AE6", false)]   // 5 digitos
    [InlineData("#ZZZZZZ", false)]  // no hexadecimal
    public void Nivel_ColorEtiqueta_DebeSerHexDeSeisDigitos(string color, bool valido)
    {
        var error = ArchivisticaRules.ValidateNivelClasificacion("Publico", "01", color, 1, null);
        Assert.Equal(valido, error is null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void Nivel_Orden_FueraDeRango_EsInvalido(int orden)
    {
        Assert.NotNull(ArchivisticaRules.ValidateNivelClasificacion("Nivel", "09", "#27AE60", orden, null));
    }

    // ---- Sedes ----

    [Fact]
    public void Sede_CamposObligatorios_SeExigen()
    {
        // Nombre, codigo, sigla y direccion son obligatorios.
        Assert.NotNull(ArchivisticaRules.ValidateSede("  ", "SED01", "SD1", "Calle 1", null, null));
        Assert.NotNull(ArchivisticaRules.ValidateSede("Sede Principal", "  ", "SD1", "Calle 1", null, null));
        Assert.NotNull(ArchivisticaRules.ValidateSede("Sede Principal", "SED01", "  ", "Calle 1", null, null));
        Assert.NotNull(ArchivisticaRules.ValidateSede("Sede Principal", "SED01", "SD1", "  ", null, null));

        // Telefono y correo son opcionales.
        Assert.Null(ArchivisticaRules.ValidateSede("Sede Principal", "SED01", "SD1", "Calle 1", null, null));
    }

    [Fact]
    public void Sede_CorreoConFormatoInvalido_EsRechazado()
    {
        Assert.NotNull(ArchivisticaRules.ValidateSede(
            "Sede Principal", "SED01", "SD1", "Calle 1", "6011234567", "no-es-un-correo"));
        Assert.Null(ArchivisticaRules.ValidateSede(
            "Sede Principal", "SED01", "SD1", "Calle 1", "6011234567", "sede@entidad.gov.co"));
    }

    // ---- Subfondos ----

    [Fact]
    public void Subfondo_CodigoYNombre_SonObligatorios()
    {
        Assert.NotNull(ArchivisticaRules.ValidateSubfondo("  ", "Subfondo"));
        Assert.NotNull(ArchivisticaRules.ValidateSubfondo("SF01", "  "));
        Assert.Null(ArchivisticaRules.ValidateSubfondo("SF01", "Subfondo Secretaria"));
    }

    private static void AssertNivel(
        NivelClasificacionCatalogo.NivelSemilla n, string nombre, string codigo, string color, int orden)
    {
        Assert.Equal(nombre, n.Nombre);
        Assert.Equal(codigo, n.Codigo);
        Assert.Equal(color, n.ColorEtiqueta);
        Assert.Equal(orden, n.NivelOrden);
    }
}
