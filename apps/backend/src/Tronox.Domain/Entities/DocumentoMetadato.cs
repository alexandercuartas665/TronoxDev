using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Valor de un metadato de documento (RQ04, DAT-04). Los metadatos se definen en el motor de RQ02
/// (<see cref="TrdMetadato"/>, contexto Documento, colgados de una tipologia); aqui se guarda solo el
/// valor por documento. TENANT-SCOPED.
/// </summary>
public class DocumentoMetadato : TenantEntity
{
    public long DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    /// <summary>Definicion del metadato en RQ02 (contexto Documento). La definicion vive alli.</summary>
    public long TrdMetadatoId { get; set; }
    public TrdMetadato? TrdMetadato { get; set; }

    public string? Valor { get; set; }
}
