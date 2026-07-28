namespace Tronox.Domain.Enums;

/// <summary>
/// Disposicion final de una serie en una dependencia (RQ02 - RF04 3.4.1, paso 4). Mutuamente
/// excluyente: exactamente UNA por asignacion. El codigo corto (CT/S/E) es el que usan la spec, la
/// exportacion y las plantillas de carga masiva; el nombre de miembro es descriptivo.
///
/// La reproduccion tecnica del soporte fisico NO es un valor de disposicion: es un flag
/// COMPLEMENTARio (TrdAsignacion.ReproduccionTecnica) que puede combinarse con cualquiera de los 3.
/// </summary>
public enum DisposicionFinal
{
    /// <summary>CT: se conserva permanentemente.</summary>
    ConservacionTotal = 0,

    /// <summary>S: se conserva una muestra representativa.</summary>
    Seleccion = 1,

    /// <summary>E: se destruye al cumplir el tiempo de retencion.</summary>
    Eliminacion = 2
}
