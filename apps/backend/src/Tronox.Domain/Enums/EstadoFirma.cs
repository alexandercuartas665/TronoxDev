namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de un registro de firma (RQ05 - FIR_FIRMAS.ESTADO). Distinto de
/// <see cref="EstadoFirmaDocumento"/> (la dimension de firma del documento): aqui es el estado de la
/// firma individual. <see cref="Pendiente"/> = solicitud a un firmante aun no cumplida;
/// <see cref="Firmado"/> = firma ejecutada; <see cref="Cancelado"/> = solicitud cancelada o rechazada.
/// </summary>
public enum EstadoFirma
{
    Pendiente = 0,
    Firmado = 1,
    Cancelado = 2,
    /// <summary>El firmante rechazo la solicitud (RF10, con comentario obligatorio). Distinto de Cancelado (lo cancela el solicitante).</summary>
    Rechazado = 3
}
