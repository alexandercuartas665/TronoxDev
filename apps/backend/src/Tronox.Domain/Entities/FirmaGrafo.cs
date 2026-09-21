using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Firma manuscrita (grafo) del usuario (RQ05 - RF03 3.3.3, legacy FIR_FIRMA_GRAFO). Una firma vigente por
/// (tenant, usuario). Se captura en un pad (signature_pad) y se guarda como PNG en base64 puro (sin el
/// prefijo data URI). La cajita de firma la pinta al sellar el documento. TENANT-SCOPED.
/// </summary>
public class FirmaGrafo : TenantEntity
{
    /// <summary>Dueno del grafo, por PlatformUserId (mismo espacio de identidad que FirmanteUserId de Firma).</summary>
    public long PlatformUserId { get; set; }

    /// <summary>Imagen de la firma manuscrita en base64 puro (PNG del pad). Sin prefijo "data:...;base64,".</summary>
    public string ImagenBase64 { get; set; } = string.Empty;

    public string ContentType { get; set; } = "image/png";

    /// <summary>Firma vigente (siempre true en v1; se conserva por si se desactiva sin borrar, invariante 8).</summary>
    public bool Activo { get; set; } = true;
}
