namespace Tronox.Domain.Enums;

/// <summary>
/// Dimension de firma del documento (RQ04, integra con RQ05). Independiente del estado del ciclo de
/// vida: un documento puede archivarse sin firmar (no bloquea el archivado). Las acciones de firma se
/// resuelven en RQ05; aqui es solo la dimension de datos.
/// </summary>
public enum EstadoFirmaDocumento
{
    SinFirma = 0,
    Pendiente = 1,
    Firmado = 2
}
