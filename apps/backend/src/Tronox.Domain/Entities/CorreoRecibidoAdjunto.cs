using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Adjunto de un correo capturado (RAD_CORREOS_ADJUNTOS). INVARIANTE 9: el binario va a object storage
/// (el legacy lo guardaba como BLOB en ARCHIVO); aqui solo la referencia. Incluye los adjuntos sinteticos
/// del pipeline: el HTML original del cuerpo (EsCuerpoHtml) y el hilo completo (EsHilo). Al radicar el
/// correo, estos se copian a RadicadoArchivo. TENANT-SCOPED. Cascade con el correo.
/// </summary>
public class CorreoRecibidoAdjunto : TenantEntity
{
    public long CorreoRecibidoId { get; set; }
    public CorreoRecibido? CorreoRecibido { get; set; }

    public string Nombre { get; set; } = null!;
    public string? Extension { get; set; }
    public string? MimeType { get; set; }
    public long TamanoBytes { get; set; }

    /// <summary>Adjunto = HTML original del cuerpo del correo.</summary>
    public bool EsCuerpoHtml { get; set; }
    /// <summary>Adjunto = hilo completo del correo.</summary>
    public bool EsHilo { get; set; }

    // ---- Referencia a object storage, NUNCA el binario. ----
    public string? StorageBucket { get; set; }
    public string? StorageKey { get; set; }
    public string? Sha256 { get; set; }
}
