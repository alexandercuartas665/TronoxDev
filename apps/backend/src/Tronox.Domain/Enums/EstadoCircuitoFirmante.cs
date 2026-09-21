namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de un firmante dentro del circuito (RQ05 - RF07). En_Espera: aun no es su turno (secuencial).
/// Pendiente: es su turno, tiene una solicitud activa. Firmado / Rechazado: resultado.
/// </summary>
public enum EstadoCircuitoFirmante
{
    EnEspera = 0,
    Pendiente = 1,
    Firmado = 2,
    Rechazado = 3
}
