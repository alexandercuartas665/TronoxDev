using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion del OCR (Azure Computer Vision, Read API) POR ENTIDAD (RQ04 - RF04). Una fila por
/// tenant. La API key es un SECRETO: se guarda cifrada (AES-256 via ISecretProtector), nunca en claro
/// (CLAUDE.md seccion 5), igual que la cadena del Almacenamiento Azure Blob (ADR-012). Si no hay config
/// activa con endpoint + llave, el "Reprocesar" del visor responde que el OCR no esta configurado.
/// TENANT-SCOPED.
/// </summary>
public class OcrConfig : TenantEntity
{
    /// <summary>Endpoint del recurso Azure Computer Vision (ej: https://miorg-vision.cognitiveservices.azure.com/).</summary>
    public string? Endpoint { get; set; }

    /// <summary>API key de Azure Computer Vision, CIFRADA. Null = no configurada.</summary>
    public string? ApiKeyCifrada { get; set; }

    /// <summary>Si esta activa (y hay endpoint + llave), el OCR de esta entidad usa esta cuenta.</summary>
    public bool Activo { get; set; }
}
