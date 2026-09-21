namespace Tronox.Domain.Enums;

/// <summary>
/// Ciclo de vida del radicado. Espejo del legacy RAD_RADICADOS.ESTADO. El panel considera "abiertos"
/// (activos) todos menos Respondido/Archivado/Anulado/Borrador.
/// </summary>
public enum RadicadoEstado
{
    Borrador,
    Radicado,
    Distribuido,
    EnTramite,
    Respondido,
    Archivado,
    Anulado
}
