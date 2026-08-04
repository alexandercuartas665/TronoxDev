using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Documento electronico: el CONTENIDO (RQ04). Nace como Borrador privado del creador (Flujo B, RF15/
/// RF16) y luego se archiva en un expediente (RQ03). TENANT-SCOPED.
///
/// Dos dimensiones de estado independientes: <see cref="Estado"/> (ciclo de vida Borrador/Archivado/
/// Anulado) y <see cref="EstadoFirma"/> (RQ05, no bloquea el archivado).
///
/// BINARIO (invariante #9, ADR-009): nunca va en base de datos. Vive en object storage (Azure Blob);
/// aqui se guarda solo la KEY (<see cref="RutaAlmacenamiento"/>), el <see cref="HashSha256"/> de
/// integridad, el tamano y el formato. Un borrador puede no tener binario (soporte Fisico).
///
/// INMUTABILIDAD TRD (DAT-03): al archivar copia la asignacion de TRD del expediente
/// (<see cref="TrdAsignacionId"/>) y esa referencia no se recalcula. Foliacion (RF06) inmutable.
/// </summary>
public class Documento : TenantEntity
{
    public string Nombre { get; set; } = null!;

    /// <summary>Nombre real del archivo subido (con extension). Null si es Fisico sin binario.</summary>
    public string? NombreArchivoOriginal { get; set; }

    public SoporteDocumento Soporte { get; set; } = SoporteDocumento.Electronico;

    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Borrador;

    public EstadoFirmaDocumento EstadoFirma { get; set; } = EstadoFirmaDocumento.SinFirma;

    // ---- Vinculo con el expediente y la TRD (se llenan al ARCHIVAR, RF16) ----

    /// <summary>Expediente contenedor. Null mientras es Borrador.</summary>
    public long? ExpedienteId { get; set; }
    public Expediente? Expediente { get; set; }

    /// <summary>Asignacion de TRD heredada del expediente al archivar (DAT-03, inmutable). Null en borrador.</summary>
    public long? TrdAsignacionId { get; set; }
    public TrdAsignacion? TrdAsignacion { get; set; }

    /// <summary>Tipo documental (tipologia RF05). Determina los metadatos de documento.</summary>
    public long? TrdTipologiaId { get; set; }
    public TrdTipologia? TrdTipologia { get; set; }

    /// <summary>Nivel de clasificacion propio (RF13). Se fija al archivar (heredado, solo elevar).</summary>
    public long? NivelClasificacionId { get; set; }
    public NivelClasificacion? NivelClasificacion { get; set; }

    // ---- Foliacion e incorporacion (inmutables al archivar, RF06/RF07) ----

    /// <summary>Fecha real de elaboracion del documento (opcional).</summary>
    public DateOnly? FechaDocumento { get; set; }

    /// <summary>Timestamp de incorporacion al expediente. INMUTABLE. Null = borrador.</summary>
    public DateTime? FechaIncorporacion { get; set; }

    /// <summary>Consecutivo de orden dentro del expediente (foliacion). INMUTABLE.</summary>
    public int? OrdenEnExpediente { get; set; }

    public int? PaginaInicio { get; set; }
    public int? PaginaFin { get; set; }
    public int? Folios { get; set; }

    // ---- Binario (object storage, ADR-009) ----

    /// <summary>Formato/extension (PDF, DOCX, ...). Null si Fisico sin binario.</summary>
    public string? Formato { get; set; }

    public long? TamanoBytes { get; set; }

    /// <summary>SHA-256 del binario (integridad). INMUTABLE.</summary>
    public string? HashSha256 { get; set; }

    public bool TieneBinario { get; set; }

    /// <summary>KEY opaca del blob en object storage (GUID). Null si no hay binario.</summary>
    public string? RutaAlmacenamiento { get; set; }

    public OcrEstadoDocumento OcrEstado { get; set; } = OcrEstadoDocumento.NoAplica;

    // ---- Versionamiento (RF03; UI diferida, columnas presentes para fidelidad) ----

    public int VersionActual { get; set; } = 1;
    public bool EsVersionHistorica { get; set; }
    public long? DocumentoPadreId { get; set; }

    /// <summary>Justificacion al anular (RF05). No hay borrado fisico salvo el borrador nunca archivado.</summary>
    public string? JustificacionAnulacion { get; set; }

    /// <summary>Valores de metadatos de documento (contexto Documento, colgados de la tipologia).</summary>
    public ICollection<DocumentoMetadato> Metadatos { get; set; } = [];
}
