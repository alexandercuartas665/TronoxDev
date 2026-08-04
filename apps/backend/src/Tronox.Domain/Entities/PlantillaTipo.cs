using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Asociacion N:N plantilla <-> tipologia documental (RQ04 - RF09, tabla puente EXP_PLANTILLAS_X_TIPOS
/// del legacy). Al crear un documento de esa tipologia, la galeria de plantillas se filtra por aqui.
/// TENANT-SCOPED.
/// </summary>
public class PlantillaTipo : TenantEntity
{
    public long PlantillaId { get; set; }
    public Plantilla? Plantilla { get; set; }

    public long TrdTipologiaId { get; set; }
    public TrdTipologia? TrdTipologia { get; set; }

    /// <summary>Snapshot del nombre de la tipologia (agrupacion sin joins fragiles, como el legacy).</summary>
    public string? TipologiaNombre { get; set; }
}
