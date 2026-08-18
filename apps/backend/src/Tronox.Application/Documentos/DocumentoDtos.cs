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

/// <summary>
/// Incorporacion de UN documento directamente en un expediente (RQ04 - Carga de Archivos, Flujo A del
/// legacy ctrlIncorporarDoc). Sube el binario, calcula hash y folia; nace Archivado. El nivel se hereda
/// del expediente (no se pide). La tipologia es opcional (solo si la serie tiene tipologias).
/// </summary>
public sealed record IncorporarDocRequest(
    long ExpedienteId,
    string Nombre,
    DateOnly FechaDocumento,
    long? TrdTipologiaId,
    int Folios,
    bool EsFisico,
    byte[]? Contenido,
    string? NombreArchivo,
    IReadOnlyList<DocMetadatoInput> Metadatos);

/// <summary>Documento de un expediente para la pestana Documentos de la vista de detalle (RQ03).</summary>
public sealed record ExpedienteDocumentoDto(
    long Id,
    int? OrdenEnExpediente,
    string Nombre,
    string? TipologiaNombre,
    string? Formato,
    long? TamanoBytes,
    int? PaginaInicio,
    int? PaginaFin,
    int? Folios,
    DateOnly? FechaDocumento,
    DateTime? FechaIncorporacion,
    SoporteDocumento Soporte,
    EstadoDocumento Estado,
    EstadoFirmaDocumento EstadoFirma,
    bool TieneBinario);

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

// ---- Editar metadatos (RF04/RF05, calcado de exp_visor_data.ashx op=doc/tipos/campos/guardar) ----

/// <summary>Opcion de tipo documental (tipologia) para el editor de metadatos. Calcado de op=tipos.</summary>
public sealed record DocTipoOpcionDto(long Id, string Nombre);

/// <summary>
/// Metadato con su valor ACTUAL para el editor de metadatos. Calcado de op=campos
/// (reg/nombre/tipo/obligatorio/valor). Igual que <see cref="DocMetadatoDefDto"/> pero con el valor.
/// </summary>
public sealed record DocMetadatoValorDefDto(
    long TrdMetadatoId,
    string Nombre,
    TipoDatoMetadato TipoDato,
    bool Obligatorio,
    long? ListaMaestraId,
    IReadOnlyList<DocMetadatoOpcionDto> OpcionesLista,
    string? Valor);

/// <summary>Estado inicial del editor de metadatos (op=doc + op=tipos + op=campos del tipo actual).</summary>
public sealed record DocEditarMetadatosDto(
    long DocId,
    string Nombre,
    DateOnly? Fecha,
    long? TipologiaId,
    IReadOnlyList<DocTipoOpcionDto> Tipos,
    IReadOnlyList<DocMetadatoValorDefDto> Campos);

/// <summary>
/// Guardado del editor de metadatos (calcado de op=guardar): nombre + fecha (obligatorios) + tipo
/// documental (0/null = sin tipo) + valores de los metadatos del tipo.
/// </summary>
public sealed record GuardarMetadatosRequest(
    long DocId,
    string Nombre,
    DateOnly? Fecha,
    long? TipologiaId,
    IReadOnlyList<DocMetadatoInput> Metadatos);

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
