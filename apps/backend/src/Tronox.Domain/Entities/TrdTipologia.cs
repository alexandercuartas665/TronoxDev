using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Tipo documental (tipologia) que conforma una Serie en una Dependencia dentro de una version de
/// TRD (RQ02 - RF05). TENANT-SCOPED. Se vincula a una asignacion Dependencia+Serie ya creada en RF04
/// (RF05 3.5.1): equivale al legacy GEN_TRD_DETALLE_TIPOLOGIA colgado del GEN_TRD_DETALLE.
///
/// Sus metadatos (contexto = Documento) se diligencian al CARGAR cada documento al expediente
/// (RF05 3.5.3), a diferencia de los metadatos del expediente (contexto = Expediente, RF04 paso 6).
/// </summary>
public class TrdTipologia : TenantEntity
{
    /// <summary>Asignacion Dependencia+Serie a la que pertenece (RF04). Vive y muere con ella.</summary>
    public long TrdAsignacionId { get; set; }
    public TrdAsignacion? TrdAsignacion { get; set; }

    /// <summary>Nombre del tipo documental. Ej: Hoja de Vida, Historia Clinica, Contrato.</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Soporte del documento: Fisico / Electronico / Hibrido (RF05 3.5.2).</summary>
    public SoporteTipologia Soporte { get; set; } = SoporteTipologia.Electronico;

    /// <summary>Formato del archivo (opcional). Ej: PDF, XML, DOCX, Papel.</summary>
    public string? Formato { get; set; }

    /// <summary>
    /// Si es true, el expediente no puede cerrarse sin este documento (RF05 3.5.2). El bloqueo
    /// efectivo se implementa en el modulo de Expedientes; aqui solo se declara.
    /// </summary>
    public bool ObligatorioEnExpediente { get; set; }

    /// <summary>
    /// Metadatos del DOCUMENTO (contexto = Documento) que se diligencian al cargar un documento de
    /// esta tipologia (RF05 3.5.3). Son independientes de los metadatos del expediente (RF04).
    /// </summary>
    public ICollection<TrdMetadato> Metadatos { get; set; } = [];

    /// <summary>Inactivacion en vez de borrado fisico (invariante 8). RF05 3.5.5-5.</summary>
    public bool IsArchived { get; set; }
}
