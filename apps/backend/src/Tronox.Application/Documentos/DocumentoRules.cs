using System.Security.Cryptography;
using Tronox.Domain.Enums;

namespace Tronox.Application.Documentos;

/// <summary>
/// Reglas puras de documentos (RQ04): validacion de binario (extension/tamano), formato, content-type,
/// hash de integridad y obligatoriedad de metadatos. Sin EF ni object storage: testeable sin infra.
/// </summary>
public static class DocumentoRules
{
    /// <summary>Tamano maximo del binario: 50 MB (paridad con el legacy).</summary>
    public const long MaxBytes = 50L * 1024 * 1024;

    /// <summary>Extensiones permitidas (paridad con el legacy).</summary>
    private static readonly HashSet<string> Permitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "docx", "xlsx", "pptx", "jpg", "jpeg", "png", "tif", "tiff", "mp3", "mp4", "aac", "xml"
    };

    /// <summary>Extensiones que disparan OCR al subir (PDF e imagenes).</summary>
    private static readonly HashSet<string> OcrExts = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "jpg", "jpeg", "png", "tif", "tiff"
    };

    /// <summary>Extension en minusculas y sin punto de un nombre de archivo.</summary>
    public static string Extension(string nombreArchivo)
    {
        var ext = Path.GetExtension(nombreArchivo);
        return string.IsNullOrEmpty(ext) ? "" : ext.TrimStart('.').ToLowerInvariant();
    }

    public static string? ValidateBinario(string nombreArchivo, long sizeBytes)
    {
        if (sizeBytes <= 0) { return "El archivo esta vacio."; }
        if (sizeBytes > MaxBytes) { return "El archivo supera el limite de 50 MB."; }
        var ext = Extension(nombreArchivo);
        if (ext.Length == 0 || !Permitidas.Contains(ext))
        {
            return "Tipo de archivo no permitido. Formatos: PDF, DOCX, XLSX, PPTX, imagenes, audio/video, XML.";
        }
        return null;
    }

    /// <summary>Formato para persistir (mayusculas, max 10): PDF, DOCX, ...</summary>
    public static string Formato(string nombreArchivo)
    {
        var ext = Extension(nombreArchivo).ToUpperInvariant();
        return ext.Length > 10 ? ext[..10] : ext;
    }

    public static OcrEstadoDocumento OcrInicial(string nombreArchivo)
        => OcrExts.Contains(Extension(nombreArchivo)) ? OcrEstadoDocumento.Pendiente : OcrEstadoDocumento.NoAplica;

    /// <summary>Content-type por extension (para subir al blob y para la descarga).</summary>
    public static string ContentType(string nombreArchivo) => Extension(nombreArchivo) switch
    {
        "pdf" => "application/pdf",
        "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "jpg" or "jpeg" => "image/jpeg",
        "png" => "image/png",
        "tif" or "tiff" => "image/tiff",
        "mp3" => "audio/mpeg",
        "mp4" => "video/mp4",
        "aac" => "audio/aac",
        "xml" => "application/xml",
        _ => "application/octet-stream"
    };

    public static string HashSha256(byte[] contenido)
        => Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

    public static string? ValidateNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) { return "El nombre del documento es obligatorio."; }
        return nombre.Trim().Length > 200 ? "El nombre no puede superar 200 caracteres." : null;
    }

    public static bool PuedeElevar(int nivelHeredadoOrden, int nivelElegidoOrden)
        => nivelElegidoOrden >= nivelHeredadoOrden;

    public const string MensajeNoBajarClasificacion =
        "El nivel de clasificacion del documento solo se puede elevar respecto al del expediente, nunca bajar.";

    public static string? ValidateMetadatosObligatorios(
        IEnumerable<(long TrdMetadatoId, string Nombre, bool Obligatorio)> definiciones,
        IReadOnlyDictionary<long, string?> valores)
    {
        foreach (var def in definiciones)
        {
            if (!def.Obligatorio) { continue; }
            valores.TryGetValue(def.TrdMetadatoId, out var valor);
            if (string.IsNullOrWhiteSpace(valor)) { return $"El metadato '{def.Nombre}' es obligatorio."; }
        }
        return null;
    }
}
