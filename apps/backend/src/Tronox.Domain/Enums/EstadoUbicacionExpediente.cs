namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de ubicacion fisica del expediente (RQ03 - RF12). Independiente del estado de tramite y de
/// la fase. La asignacion de topografia (DOC_BODEGA_R / RQ01 - RF06) se difiere a un slice posterior;
/// por ahora el expediente nace SinUbicar.
/// </summary>
public enum EstadoUbicacionExpediente
{
    /// <summary>Sin ubicacion fisica asignada. Estado por defecto.</summary>
    SinUbicar = 0,

    /// <summary>Con ubicacion fisica asignada.</summary>
    Ubicado = 1,

    /// <summary>Reubicado a una ubicacion distinta de la inicial.</summary>
    Reubicado = 2
}
