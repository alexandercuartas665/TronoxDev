namespace Tronox.Domain.Enums;

/// <summary>
/// Estado de un correo capturado de un buzon, pendiente de convertirse en radicado. Espejo del legacy
/// RAD_CORREOS.ESTADO. El panel cuenta los Pendiente ("Correos por revisar"). El modulo completo llega
/// al portar rad_correos.aspx.
/// </summary>
public enum CorreoRevisionEstado
{
    Pendiente,
    Radicado,
    Descartado
}
