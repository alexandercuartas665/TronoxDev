using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Valor de un metadato dinamico de un expediente (RQ03, DAT-04). NO se agregan columnas a
/// <see cref="Expediente"/>: los metadatos se definen en el motor de RQ02 (<see cref="TrdMetadato"/>,
/// contexto Expediente) y aqui se guarda solo el valor por expediente. TENANT-SCOPED.
/// </summary>
public class ExpedienteMetadato : TenantEntity
{
    /// <summary>Expediente dueno del valor. Vive y muere con el.</summary>
    public long ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    /// <summary>
    /// Definicion del metadato en RQ02 (contexto Expediente, colgado de la asignacion de TRD). La
    /// definicion (nombre, tipo de dato, obligatoriedad, lista) vive alli; aqui solo el valor.
    /// </summary>
    public long TrdMetadatoId { get; set; }
    public TrdMetadato? TrdMetadato { get; set; }

    /// <summary>Valor diligenciado. Se guarda como texto; el tipo lo impone la definicion (RQ02).</summary>
    public string? Valor { get; set; }
}
