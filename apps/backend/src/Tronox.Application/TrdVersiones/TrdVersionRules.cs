using Tronox.Domain.Enums;

namespace Tronox.Application.TrdVersiones;

/// <summary>
/// LOGICA PURA de las versiones de TRD (RQ02 - RF01): validacion de campos y maquina de estados,
/// SIN EF y sin base de datos, testeables en Tronox.Application.Tests. La unicidad del codigo y el
/// flip de la Vigente anterior necesitan la base y viven en el servicio.
///
/// Devuelven null cuando es valido, o el mensaje de error listo para la presentacion.
/// </summary>
public static class TrdVersionRules
{
    public const int MaxCodigo = 50;
    public const int MaxDescripcion = 300;
    public const int MaxActo = 200;

    /// <summary>
    /// Reglas de los campos de una version (RF01 3.1.1): codigo obligatorio y acotado, textos
    /// acotados, fecha de inicio de vigencia obligatoria.
    /// </summary>
    public static string? ValidateVersion(
        string? codigoVersion, string? descripcion, string? actoAdministrativo,
        DateOnly fechaVigenciaDesde)
    {
        if (string.IsNullOrWhiteSpace(codigoVersion)) { return "El codigo de la version es obligatorio."; }
        if (codigoVersion.Trim().Length > MaxCodigo) { return $"El codigo no puede superar {MaxCodigo} caracteres."; }
        if (descripcion is not null && descripcion.Trim().Length > MaxDescripcion)
        {
            return $"La descripcion no puede superar {MaxDescripcion} caracteres.";
        }
        if (actoAdministrativo is not null && actoAdministrativo.Trim().Length > MaxActo)
        {
            return $"El acto administrativo no puede superar {MaxActo} caracteres.";
        }
        if (fechaVigenciaDesde == default)
        {
            return "La fecha de inicio de vigencia es obligatoria.";
        }
        return null;
    }

    /// <summary>
    /// Los campos de una version solo se editan en estado EnConstruccion (RF01 3.1.2): una vez
    /// Vigente/Historico/Inactivo la identidad de la version queda congelada. Los cambios sobre el
    /// CONTENIDO de una TRD Vigente (series, tiempos) son otro asunto y viven en RF04.
    /// </summary>
    public static string? CanEditar(TrdVersionEstado estado)
        => estado == TrdVersionEstado.EnConstruccion
            ? null
            : "Solo se pueden editar los datos de una version En Construccion. " +
              "Para cambios estructurales cree una nueva version.";

    /// <summary>
    /// Activar (pasar a Vigente) solo procede desde EnConstruccion (RF01 3.1.2). Al activarla, la
    /// Vigente anterior pasa AUTOMATICAMENTE a Historico (lo hace el servicio).
    /// </summary>
    public static string? CanActivar(TrdVersionEstado estado)
        => estado == TrdVersionEstado.EnConstruccion
            ? null
            : "Solo se puede activar una version En Construccion.";

    /// <summary>Descartar (pasar a Inactivo) solo procede desde EnConstruccion (RF01 3.1.2).</summary>
    public static string? CanDescartar(TrdVersionEstado estado)
        => estado == TrdVersionEstado.EnConstruccion
            ? null
            : "Solo se puede descartar una version En Construccion. " +
              "Una version Vigente se reemplaza activando una nueva.";
}
