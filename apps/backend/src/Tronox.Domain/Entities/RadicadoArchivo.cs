using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Archivo/anexo de un radicado (RAD_RADICADO_ARCHIVOS). INVARIANTE 9: el binario NO va en la base de
/// datos (el legacy lo guardaba como BLOB en la columna ARCHIVO). Aqui se guarda solo la REFERENCIA al
/// object storage (bucket/key + mime + hash + tamano); el endpoint de archivo hace stream desde storage.
/// El upload real llega al portar rad_radicar. TENANT-SCOPED. Cascade con el radicado.
/// </summary>
public class RadicadoArchivo : TenantEntity
{
    public long RadicadoId { get; set; }
    public Radicado? Radicado { get; set; }

    public string Nombre { get; set; } = null!;
    public string? Extension { get; set; }
    public string? MimeType { get; set; }
    public long TamanoBytes { get; set; }

    // ---- Referencia a object storage (S3/MinIO), NUNCA el binario. ----
    public string? StorageBucket { get; set; }
    public string? StorageKey { get; set; }
    public string? Sha256 { get; set; }

    public DateTime FechaCarga { get; set; }
}
