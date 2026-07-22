using System.Text.RegularExpressions;
using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

/// <summary>
/// LOGICA PURA de la configuracion archivistica (RQ01 - RF01-P.3 / RF02): validaciones sin EF y
/// sin base de datos, testeables en Tronox.Application.Tests. Los servicios se limitan a
/// invocarlas y a resolver lo que si necesita la base (unicidad, existencia, dependencias).
///
/// Todas devuelven null cuando el dato es valido, o el mensaje de error listo para la
/// presentacion cuando no lo es (mismo estilo que MenuNodeKindRules).
/// </summary>
public static class ArchivisticaRules
{
    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    // ---- Niveles de clasificacion documental ----

    public static string? ValidateNivelClasificacion(
        string? nombre, string? codigo, string? colorEtiqueta, int nivelOrden, string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre del nivel es obligatorio."; }
        if (nombre.Trim().Length > 120) { return "El nombre no puede superar 120 caracteres."; }
        if (string.IsNullOrWhiteSpace(codigo)) { return "El codigo del nivel es obligatorio."; }
        if (codigo.Trim().Length > 10) { return "El codigo no puede superar 10 caracteres."; }
        if (descripcion is not null && descripcion.Trim().Length > 500)
        {
            return "La descripcion no puede superar 500 caracteres.";
        }
        if (!string.IsNullOrWhiteSpace(colorEtiqueta) && !HexColor.IsMatch(colorEtiqueta.Trim()))
        {
            return "El color de etiqueta debe ser un HEX de 6 digitos (formato #RRGGBB).";
        }
        // 1 = menor restriccion (Publico) ... 4 = mayor (Clasificado). El rango se acota para que
        // roles.nivel_acceso_maximo (RF05) compare contra una escala cerrada y conocida.
        if (nivelOrden is < 1 or > 4)
        {
            return "El orden del nivel debe estar entre 1 (menor restriccion) y 4 (mayor restriccion).";
        }
        return null;
    }

    // ---- Sedes ----

    public static string? ValidateSede(
        string? nombreSede, string? codigoSede, string? siglaSede,
        string? direccion, string? telefono, string? correoSede)
    {
        if (string.IsNullOrWhiteSpace(nombreSede)) { return "El nombre de la sede es obligatorio."; }
        if (nombreSede.Trim().Length > 200) { return "El nombre de la sede no puede superar 200 caracteres."; }
        if (string.IsNullOrWhiteSpace(codigoSede)) { return "El codigo de la sede es obligatorio."; }
        if (codigoSede.Trim().Length > 20) { return "El codigo de la sede no puede superar 20 caracteres."; }
        if (string.IsNullOrWhiteSpace(siglaSede)) { return "La sigla de la sede es obligatoria."; }
        if (siglaSede.Trim().Length > 10) { return "La sigla no puede superar 10 caracteres."; }
        if (string.IsNullOrWhiteSpace(direccion)) { return "La direccion de la sede es obligatoria."; }
        if (direccion.Trim().Length > 200) { return "La direccion no puede superar 200 caracteres."; }
        if (telefono is not null && telefono.Trim().Length > 20) { return "El telefono no puede superar 20 caracteres."; }
        if (correoSede is not null && correoSede.Trim().Length > 150) { return "El correo no puede superar 150 caracteres."; }
        if (!string.IsNullOrWhiteSpace(correoSede) && !correoSede.Contains('@'))
        {
            return "El correo de la sede no tiene un formato valido.";
        }
        // Nota DIVIPOLA: PaisId/DepartamentoId/CiudadId son obligatorios segun la spec, pero sus
        // catalogos aun no existen como tablas. La comprobacion se activa aqui cuando existan.
        return null;
    }

    // ---- Fondos documentales ----

    /// <summary>
    /// Reglas de negocio del fondo (RF02), todas en un solo punto:
    /// - codigo y nombre obligatorios y acotados;
    /// - FechaCierre OBLIGATORIA si Estado = Cerrado, y estrictamente POSTERIOR a FechaApertura;
    /// - EntidadOrigen OBLIGATORIA si TipoFondo = Acumulado.
    /// </summary>
    public static string? ValidateFondo(
        string? codigoFondo, string? nombreFondo, string? descripcion,
        FondoTipo tipoFondo, FondoEstado estado,
        DateOnly fechaApertura, DateOnly? fechaCierre, string? entidadOrigen)
    {
        if (string.IsNullOrWhiteSpace(codigoFondo)) { return "El codigo del fondo es obligatorio."; }
        if (codigoFondo.Trim().Length > 20) { return "El codigo del fondo no puede superar 20 caracteres."; }
        if (string.IsNullOrWhiteSpace(nombreFondo)) { return "El nombre del fondo es obligatorio."; }
        if (nombreFondo.Trim().Length > 200) { return "El nombre del fondo no puede superar 200 caracteres."; }
        if (descripcion is not null && descripcion.Trim().Length > 500)
        {
            return "La descripcion no puede superar 500 caracteres.";
        }
        if (fechaApertura == default) { return "La fecha de apertura es obligatoria."; }

        if (estado == FondoEstado.Cerrado)
        {
            if (fechaCierre is not DateOnly cierre)
            {
                return "La fecha de cierre es obligatoria cuando el fondo esta Cerrado.";
            }
            if (cierre <= fechaApertura)
            {
                return "La fecha de cierre debe ser posterior a la fecha de apertura.";
            }
        }
        else if (fechaCierre is DateOnly cierreNoCerrado && cierreNoCerrado <= fechaApertura)
        {
            // Si se informa una fecha de cierre sin estar Cerrado, al menos debe ser coherente.
            return "La fecha de cierre debe ser posterior a la fecha de apertura.";
        }

        if (tipoFondo == FondoTipo.Acumulado && string.IsNullOrWhiteSpace(entidadOrigen))
        {
            return "La entidad de origen es obligatoria en un fondo Acumulado " +
                   "(nombre de la entidad liquidada o fusionada).";
        }
        if (entidadOrigen is not null && entidadOrigen.Trim().Length > 200)
        {
            return "La entidad de origen no puede superar 200 caracteres.";
        }

        return null;
    }

    /// <summary>
    /// Un fondo Cerrado es de SOLO LECTURA: no admite nada nuevo colgando de el (subfondos,
    /// series, expedientes). Consultar y exportar si esta permitido, sin limite.
    /// </summary>
    public static bool EsSoloLectura(FondoEstado estado) => estado == FondoEstado.Cerrado;

    /// <summary>Mensaje unico del bloqueo de solo lectura, para que no derive entre modulos.</summary>
    public const string FondoCerradoSoloLectura =
        "El fondo esta Cerrado y es de solo lectura: no admite nuevos elementos. " +
        "Se puede consultar y exportar sin limite.";

    /// <summary>
    /// Mensaje unico del bloqueo de eliminacion (regla 4 de RF02 + invariante 8): un fondo con
    /// dependencias, series o expedientes NO se elimina, se Inactiva o se Cierra.
    /// </summary>
    public static string FondoNoEliminable(string dependencias) =>
        $"No se puede eliminar el fondo: tiene {dependencias} asociado(s). " +
        "Inactivelo o cierrelo en lugar de eliminarlo.";

    // ---- Subfondos ----

    public static string? ValidateSubfondo(string? codigoSubfondo, string? nombreSubfondo)
    {
        if (string.IsNullOrWhiteSpace(codigoSubfondo)) { return "El codigo del subfondo es obligatorio."; }
        if (codigoSubfondo.Trim().Length > 20) { return "El codigo del subfondo no puede superar 20 caracteres."; }
        if (string.IsNullOrWhiteSpace(nombreSubfondo)) { return "El nombre del subfondo es obligatorio."; }
        if (nombreSubfondo.Trim().Length > 200) { return "El nombre del subfondo no puede superar 200 caracteres."; }
        return null;
    }
}
