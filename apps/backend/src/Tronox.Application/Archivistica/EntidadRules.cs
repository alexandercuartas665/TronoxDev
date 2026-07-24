using System.Text.RegularExpressions;
using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

/// <summary>
/// LOGICA PURA de los Datos de la Entidad (RQ01 - RF01 seccion 4.1): validacion sin EF y sin
/// base de datos, testeable en Tronox.Application.Tests. Aqui viven las tres reglas que la
/// pantalla NO puede reinterpretar:
///
/// 1. El digito de verificacion del NIT se calcula con el algoritmo de la DIAN, no se cree.
/// 2. El codigo de fondo AGN se GENERA (resolucion M01), no se captura.
/// 3. Si la entidad es Publica, DIVIPOLA y codigo de fondo AGN pasan a obligatorios.
///
/// Lo que NO se valida aqui (necesita base de datos y vive en EntidadService): que exista una
/// sola entidad por tenant, que el NIT no se repita y que pais/departamento/ciudad existan y
/// sean coherentes entre si.
/// </summary>
public static class EntidadRules
{
    public const int MaxNit = 15;
    public const int MaxRazonSocial = 200;

    /// <summary>
    /// Resolucion M01: la spec decia 20, pero la sigla entra LITERAL en el codigo de fondo AGN
    /// que se estampa en todos los expedientes. Se acota a 10.
    /// </summary>
    public const int MaxSigla = 10;

    public const int MaxNaturalezaJuridica = 100;
    public const int MaxDireccion = 200;
    public const int MaxTelefono = 20;
    public const int MaxCorreo = 150;
    public const int MaxPaginaWeb = 200;
    public const int MaxRepresentanteLegal = 150;
    public const int MaxCodigoFondoAgn = 30;
    public const int LongitudDivipola = 5;

    /// <summary>Tamano maximo del logo (5 MB, RF01 4.1.1).</summary>
    public const long MaxLogoBytes = 5L * 1024 * 1024;

    /// <summary>
    /// Ponderadores del algoritmo de digito de verificacion del NIT (DIAN). Se aplican a los
    /// digitos del NIT leidos de DERECHA a IZQUIERDA. Son 15 porque el NIT admite 15 digitos.
    /// </summary>
    private static readonly int[] PonderadoresNit =
        [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    private static readonly Regex SoloDigitos = new("^[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex CorreoBasico =
        new(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.Compiled);

    // ---- NIT ----

    /// <summary>
    /// Normaliza el NIT tal como lo escribe el usuario: quita puntos, guiones y espacios.
    /// "900.123.456" y "900 123 456" son el mismo NIT.
    /// </summary>
    public static string NormalizarNit(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) { return string.Empty; }
        // Sin stackalloc a proposito: la longitud la controla el cliente y no se acota antes.
        var buffer = new System.Text.StringBuilder(nit.Length);
        foreach (var c in nit)
        {
            if (char.IsAsciiDigit(c)) { buffer.Append(c); }
        }
        return buffer.ToString();
    }

    /// <summary>
    /// Digito de verificacion del NIT segun la DIAN: se ponderan los digitos de derecha a
    /// izquierda, se toma el residuo entre 11 y el DV es el residuo si es 0 o 1, u 11 menos el
    /// residuo en cualquier otro caso. Devuelve null si el NIT no es utilizable (vacio, con
    /// caracteres no numericos o de mas de 15 digitos).
    ///
    /// Comprobacion de referencia: NIT 800197268 -> DV 4.
    /// </summary>
    public static int? CalcularDigitoVerificacion(string? nit)
    {
        var limpio = NormalizarNit(nit);
        if (limpio.Length == 0 || limpio.Length > MaxNit) { return null; }

        var suma = 0;
        for (var i = 0; i < limpio.Length; i++)
        {
            // limpio[^ (i+1)] recorre de derecha a izquierda.
            var digito = limpio[limpio.Length - 1 - i] - '0';
            suma += digito * PonderadoresNit[i];
        }

        var residuo = suma % 11;
        return residuo is 0 or 1 ? residuo : 11 - residuo;
    }

    /// <summary>
    /// El digito de verificacion informado coincide con el calculado. Se acepta el DV como texto
    /// porque la UI lo captura en un campo aparte (igual que el sistema anterior).
    /// </summary>
    public static bool DigitoVerificacionCoincide(string? nit, string? digitoVerificacion)
    {
        var esperado = CalcularDigitoVerificacion(nit);
        if (esperado is null) { return false; }
        var dv = (digitoVerificacion ?? string.Empty).Trim();
        return dv.Length == 1 && char.IsAsciiDigit(dv[0]) && dv[0] - '0' == esperado.Value;
    }

    // ---- Codigo de fondo AGN ----

    /// <summary>
    /// Genera el codigo de fondo AGN con el patron CO-{DIVIPOLA}-{SIGLA_MAYUSCULAS}
    /// (resolucion M01). Devuelve null si falta cualquiera de las dos piezas: un codigo a
    /// medias seria peor que ninguno, porque se estampa como metadato raiz en todo expediente.
    ///
    /// El prefijo "CO" es fijo: el patron de la Guia Tecnica MinTIC es ISO 3166-2:CO.
    /// </summary>
    public static string? GenerarCodigoFondoAgn(string? codigoDivipola, string? sigla)
    {
        var divipola = (codigoDivipola ?? string.Empty).Trim();
        var siglaLimpia = (sigla ?? string.Empty).Trim().ToUpperInvariant();
        if (divipola.Length == 0 || siglaLimpia.Length == 0) { return null; }
        return $"CO-{divipola}-{siglaLimpia}";
    }

    // ---- Obligatoriedad condicional ----

    /// <summary>
    /// Criterio de aceptacion 4 de RF01: en una entidad Publica, DIVIPOLA y codigo de fondo AGN
    /// dejan de ser opcionales. Para Privada y Mixta son recomendados, no exigidos.
    /// </summary>
    public static bool RequiereDatosAgn(TipoEntidad tipoEntidad) => tipoEntidad == TipoEntidad.Publica;

    // ---- Validacion completa ----

    /// <summary>
    /// Valida el formulario completo. Devuelve null si es valido, o el mensaje de error listo
    /// para la presentacion (mismo estilo que ArchivisticaRules).
    /// </summary>
    public static string? Validate(
        string? nit,
        string? digitoVerificacion,
        string? razonSocial,
        string? sigla,
        TipoEntidad tipoEntidad,
        string? naturalezaJuridica,
        string? codigoDivipola,
        long? paisId,
        long? departamentoId,
        long? ciudadId,
        string? direccionPrincipal,
        string? telefono,
        string? correoInstitucional,
        string? paginaWeb,
        string? representanteLegal,
        string? codigoFondoAgn,
        string? zonaHoraria,
        string? idiomaDefecto)
    {
        // --- NIT y digito verificador ---
        var nitLimpio = NormalizarNit(nit);
        if (nitLimpio.Length == 0) { return "El NIT es obligatorio."; }
        if (nitLimpio.Length > MaxNit) { return $"El NIT no puede superar {MaxNit} digitos."; }
        if (!DigitoVerificacionCoincide(nitLimpio, digitoVerificacion))
        {
            var esperado = CalcularDigitoVerificacion(nitLimpio);
            return $"El digito de verificacion del NIT no es correcto: para {nitLimpio} corresponde {esperado}.";
        }

        // --- Identificacion ---
        if (string.IsNullOrWhiteSpace(razonSocial)) { return "La razon social es obligatoria."; }
        if (razonSocial.Trim().Length > MaxRazonSocial)
        {
            return $"La razon social no puede superar {MaxRazonSocial} caracteres.";
        }
        if (string.IsNullOrWhiteSpace(sigla)) { return "La sigla es obligatoria."; }
        if (sigla.Trim().Length > MaxSigla)
        {
            return $"La sigla no puede superar {MaxSigla} caracteres: entra literal en el codigo de fondo AGN.";
        }
        if (naturalezaJuridica is { } nj && nj.Trim().Length > MaxNaturalezaJuridica)
        {
            return $"La naturaleza juridica no puede superar {MaxNaturalezaJuridica} caracteres.";
        }
        if (string.IsNullOrWhiteSpace(representanteLegal))
        {
            return "El representante legal es obligatorio.";
        }
        if (representanteLegal.Trim().Length > MaxRepresentanteLegal)
        {
            return $"El representante legal no puede superar {MaxRepresentanteLegal} caracteres.";
        }

        // --- Ubicacion (selectores encadenados) ---
        if (paisId is null) { return "El pais es obligatorio."; }
        if (departamentoId is null) { return "El departamento es obligatorio."; }
        if (ciudadId is null) { return "La ciudad o municipio es obligatorio."; }
        if (string.IsNullOrWhiteSpace(direccionPrincipal)) { return "La direccion principal es obligatoria."; }
        if (direccionPrincipal.Trim().Length > MaxDireccion)
        {
            return $"La direccion no puede superar {MaxDireccion} caracteres.";
        }
        if (telefono is { } tel && tel.Trim().Length > MaxTelefono)
        {
            return $"El telefono no puede superar {MaxTelefono} caracteres.";
        }

        // --- Contacto ---
        if (string.IsNullOrWhiteSpace(correoInstitucional))
        {
            return "El correo institucional es obligatorio.";
        }
        if (correoInstitucional.Trim().Length > MaxCorreo)
        {
            return $"El correo institucional no puede superar {MaxCorreo} caracteres.";
        }
        if (!CorreoBasico.IsMatch(correoInstitucional.Trim()))
        {
            return "El correo institucional no tiene un formato valido.";
        }
        if (!string.IsNullOrWhiteSpace(paginaWeb))
        {
            if (paginaWeb.Trim().Length > MaxPaginaWeb)
            {
                return $"La pagina web no puede superar {MaxPaginaWeb} caracteres.";
            }
            if (!EsUrlValida(paginaWeb.Trim()))
            {
                return "La pagina web debe ser una URL valida (http:// o https://).";
            }
        }

        // --- DIVIPOLA / AGN, obligatorios solo si la entidad es Publica ---
        var divipola = (codigoDivipola ?? string.Empty).Trim();
        if (divipola.Length > 0)
        {
            if (divipola.Length != LongitudDivipola || !SoloDigitos.IsMatch(divipola))
            {
                return $"El codigo DIVIPOLA debe tener {LongitudDivipola} digitos (ej. 11001 = Bogota D.C.).";
            }
        }
        var agn = (codigoFondoAgn ?? string.Empty).Trim();
        if (agn.Length > MaxCodigoFondoAgn)
        {
            return $"El codigo de fondo AGN no puede superar {MaxCodigoFondoAgn} caracteres.";
        }

        if (RequiereDatosAgn(tipoEntidad))
        {
            if (divipola.Length == 0)
            {
                return "El codigo DIVIPOLA es obligatorio en una entidad Publica.";
            }
            if (agn.Length == 0)
            {
                return "El codigo de fondo AGN es obligatorio en una entidad Publica.";
            }
        }

        // --- Sistema ---
        if (string.IsNullOrWhiteSpace(zonaHoraria))
        {
            return "La zona horaria es obligatoria: los timestamps se guardan en UTC y se muestran en ella.";
        }
        if (string.IsNullOrWhiteSpace(idiomaDefecto))
        {
            return "El idioma por defecto es obligatorio.";
        }

        return null;
    }

    /// <summary>Mensaje unico del bloqueo de eliminacion (criterio 8 de RF01 + invariante 8).</summary>
    public const string EntidadNoEliminable =
        "La entidad no se elimina. Cambie su estado a Inactivo o Suspendido.";

    /// <summary>Mensaje unico del conflicto de "una sola entidad por tenant" (criterio 1 de RF01).</summary>
    public const string EntidadYaExiste =
        "El tenant ya tiene registrada su entidad. Solo puede haber una: edite la existente.";

    private static bool EsUrlValida(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
