using Tronox.Domain.Enums;

namespace Tronox.Application.Documentos;

/// <summary>Las tres bandejas de "Mis Documentos" (RQ04 - RF15).</summary>
public enum BandejaDocumento
{
    /// <summary>Borradores del creador (privados).</summary>
    MisBorradores = 0,
    /// <summary>Documentos archivados por el usuario en algun expediente.</summary>
    ArchivadosPorMi = 1,
    /// <summary>Compartidos con el usuario (RF07). Diferido: por ahora vacia.</summary>
    CompartidosConmigo = 2
}

/// <summary>Fila de "Mis Borradores".</summary>
public sealed record BorradorItemDto(
    long Id,
    string Nombre,
    string? Formato,
    SoporteDocumento Soporte,
    DateTimeOffset FechaCreacion,
    int? Folios,
    long? TamanoBytes,
    EstadoFirmaDocumento EstadoFirma,
    bool TieneBinario);

/// <summary>Fila de "Archivados por mi".</summary>
public sealed record ArchivadoItemDto(
    long Id,
    string Nombre,
    string? TipologiaNombre,
    string ExpedienteCodigo,
    string ExpedienteNombre,
    DateTime? FechaIncorporacion,
    int? OrdenEnExpediente,
    int? Folios,
    long? TamanoBytes,
    string NivelNombre,
    EstadoFirmaDocumento EstadoFirma,
    bool TieneBinario);

/// <summary>Detalle de un documento (basico en este slice).</summary>
public sealed record DocumentoDetalleDto(
    long Id,
    string Nombre,
    string? NombreArchivoOriginal,
    EstadoDocumento Estado,
    SoporteDocumento Soporte,
    EstadoFirmaDocumento EstadoFirma,
    string? Formato,
    long? TamanoBytes,
    int? Folios,
    string? HashSha256,
    bool TieneBinario,
    DateOnly? FechaDocumento,
    DateTime? FechaIncorporacion,
    string? ExpedienteCodigo,
    string? ExpedienteNombre,
    string? TipologiaNombre,
    string? NivelNombre,
    IReadOnlyList<DocMetadatoValorDto> Metadatos);

public sealed record DocMetadatoValorDto(long TrdMetadatoId, string Nombre, TipoDatoMetadato TipoDato, string? Valor);

// ---- Descarga ----

public sealed record DocumentoDescargaDto(byte[] Contenido, string NombreArchivo, string ContentType);

// ---- Requests de creacion (Flujo B) ----

public sealed record CrearBorradorFisicoRequest(string Nombre, DateOnly? FechaDocumento);

/// <summary>Metadatos para archivar / editar (contexto Documento).</summary>
public sealed record DocMetadatoInput(long TrdMetadatoId, string? Valor);

public sealed record DocMetadatoDefDto(
    long TrdMetadatoId,
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    long? ListaMaestraId,
    IReadOnlyList<DocMetadatoOpcionDto> OpcionesLista);

public sealed record DocMetadatoOpcionDto(string Clave, string Valor);

// ---- Archivar (RF16) ----

/// <summary>Expediente destino candidato para archivar (visible y Abierto).</summary>
public sealed record ExpedienteDestinoDto(
    long Id, string Codigo, string Nombre, long TrdAsignacionId, int NivelHeredadoOrden);

/// <summary>Tipologia disponible en el expediente destino (de su serie).</summary>
public sealed record TipologiaOpcionDto(long Id, string Nombre, SoporteTipologia Soporte);

public sealed record NivelDocOpcionDto(long Id, string Nombre, int Orden);

public sealed record ArchivarRequest(
    long DocumentoId,
    long ExpedienteId,
    long TrdTipologiaId,
    long NivelClasificacionId,
    DateOnly? FechaDocumento,
    IReadOnlyList<DocMetadatoInput> Metadatos);
