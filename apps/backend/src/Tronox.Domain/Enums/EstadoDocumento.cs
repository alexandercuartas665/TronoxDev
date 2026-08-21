namespace Tronox.Domain.Enums;

/// <summary>
/// Ciclo de vida del documento (RQ04). Independiente del estado de firma (<see cref="EstadoFirmaDocumento"/>).
/// Borrador -&gt; Archivado -&gt; Anulado. El Borrador nunca archivado es el UNICO caso de borrado fisico
/// del sistema (invariante 8); Archivado y Anulado nunca se borran.
/// </summary>
public enum EstadoDocumento
{
    /// <summary>Produccion privada del creador (Flujo B). Sin expediente, foliacion ni indice.</summary>
    Borrador = 0,

    /// <summary>Incorporado a un expediente (RF16): con foliacion, orden e indice electronico.</summary>
    Archivado = 1,

    /// <summary>Anulado con justificacion. No se elimina; se excluye de las bandejas.</summary>
    Anulado = 2,

    /// <summary>
    /// Borrador con PDF final marcado como Terminado (ADR-003): listo para firmar antes de archivar.
    /// Sigue en la bandeja "Mis Borradores" (no es Archivado). Se persiste como string, sin migracion.
    /// </summary>
    Terminado = 3
}
