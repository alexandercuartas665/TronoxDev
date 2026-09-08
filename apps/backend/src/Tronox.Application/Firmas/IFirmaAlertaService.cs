namespace Tronox.Application.Firmas;

/// <summary>
/// Alertas de firma pendiente (RQ05 - RF11 Inc.2, legacy FirmaAlertaRepository + fir_alertas.ashx).
/// Recorre las firmas Pendientes del tenant ACTUAL (ambient) y, cuando llevan mas de FirmaConfig.FirmaDias
/// dias HABILES esperando (calendario habil de la entidad), avisa al firmante (campana + correo),
/// respetando FirmaConfig.FirmaFrecuenciaDias para no repetir a diario. Lo dispara un proceso de fondo
/// que fija el tenant por cada entidad (FirmaAlertasHostedService).
/// </summary>
public interface IFirmaAlertaService
{
    /// <summary>Escanea y alerta las firmas vencidas del tenant actual. Devuelve cuantas alertas emitio.</summary>
    Task<int> EscanearYAlertarAsync(CancellationToken cancellationToken = default);
}
