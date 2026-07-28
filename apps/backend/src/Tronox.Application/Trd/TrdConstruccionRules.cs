using Tronox.Domain.Enums;

namespace Tronox.Application.Trd;

/// <summary>
/// LOGICA PURA de la construccion de la TRD (RQ02 - RF04): composicion del codigo CCD, validaciones
/// de campos y la MAQUINA DE PERMISOS DE EDICION segun el estado de la version (RF01 3.1.3). Sin EF
/// ni base de datos, testeable en Tronox.Application.Tests.
///
/// Regla de solo-lectura por estado de la version (spec RF01 3.1.3, el matiz que distingue a este
/// modulo del legacy, que bloqueaba TODO sobre Vigente):
///
///   Estado           | Agregar serie | Editar tiempos/disp/clasif | Editar procedimiento/metadatos | Eliminar serie
///   En Construccion  |      Si       |            Si              |              Si                |      Si
///   Vigente          |      Si       |            NO              |              Si                |      NO
///   Historico/Inactivo|     NO       |            NO              |              NO                |      NO
/// </summary>
public static class TrdConstruccionRules
{
    public const int MaxProcedimiento = 1000;

    /// <summary>
    /// Codigo CCD compuesto (RF04 3.4.1 paso 3): codigoDependencia + "." + codigoSerie. Se genera
    /// automaticamente y NO es editable por el usuario (RF04 3.4.4-2).
    /// </summary>
    public static string ComponerCodigoCcd(string codigoDependencia, string codigoSerie)
        => $"{codigoDependencia.Trim()}.{codigoSerie.Trim()}";

    /// <summary>
    /// Reglas de los campos de una asignacion (RF04 3.4.1 paso 4): tiempos no negativos (sin limite
    /// maximo, AGN 2.5), procedimiento acotado.
    /// </summary>
    public static string? ValidateReglas(int tiempoGestion, int tiempoCentral, string? procedimiento)
    {
        if (tiempoGestion < 0) { return "El tiempo en Archivo de Gestion no puede ser negativo."; }
        if (tiempoCentral < 0) { return "El tiempo en Archivo Central no puede ser negativo."; }
        if (procedimiento is not null && procedimiento.Trim().Length > MaxProcedimiento)
        {
            return $"El procedimiento no puede superar {MaxProcedimiento} caracteres.";
        }
        return null;
    }

    /// <summary>
    /// Validacion de un metadato (RF04 paso 6): nombre obligatorio; si es de tipo Lista, exige una
    /// lista (RF03). El nombre de la lista/opciones se valida en el servicio.
    /// </summary>
    public static string? ValidateMetadato(string? nombre, TipoDatoMetadato tipoDato, long? listaMaestraId)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre del metadato es obligatorio."; }
        if (nombre.Trim().Length > 200) { return "El nombre del metadato no puede superar 200 caracteres."; }
        if (tipoDato == TipoDatoMetadato.Lista && listaMaestraId is null)
        {
            return "Un metadato de tipo Lista requiere seleccionar una lista del Administrador de Listas.";
        }
        if (tipoDato != TipoDatoMetadato.Lista && listaMaestraId is not null)
        {
            return "Solo los metadatos de tipo Lista pueden referenciar una lista.";
        }
        return null;
    }

    // ---- Maquina de permisos de edicion (RF01 3.1.3) ----

    /// <summary>Crear/agregar asignaciones: no permitido si la version es Historico o Inactivo (RF04 3.4.4-1).</summary>
    public static bool PermiteAgregar(TrdVersionEstado estado)
        => estado is TrdVersionEstado.EnConstruccion or TrdVersionEstado.Vigente;

    /// <summary>Editar tiempos, disposicion final o clasificacion: solo En Construccion (RF01 3.1.3).</summary>
    public static bool PermiteEditarEstructura(TrdVersionEstado estado)
        => estado == TrdVersionEstado.EnConstruccion;

    /// <summary>Editar procedimiento y metadatos: En Construccion o Vigente (RF01 3.1.3).</summary>
    public static bool PermiteEditarProcedimientoYMetadatos(TrdVersionEstado estado)
        => estado is TrdVersionEstado.EnConstruccion or TrdVersionEstado.Vigente;

    /// <summary>Eliminar (inactivar) una asignacion: solo En Construccion (RF01 3.1.3).</summary>
    public static bool PermiteEliminar(TrdVersionEstado estado)
        => estado == TrdVersionEstado.EnConstruccion;

    /// <summary>Mensaje unico del bloqueo estructural sobre una TRD Vigente (spec RF01 3.1.3).</summary>
    public const string MensajeVigenteSoloEstructura =
        "Esta TRD esta vigente. Para cambios estructurales (tiempos, disposicion, clasificacion, " +
        "eliminar series) debe crear una nueva version En Construccion. Los expedientes existentes " +
        "conservaran los parametros de la version bajo la que fueron creados.";

    /// <summary>Mensaje unico del bloqueo total sobre una version Historico/Inactivo.</summary>
    public const string MensajeNoEditable =
        "La version de TRD no es editable (Historico o Inactivo): solo consulta.";
}
