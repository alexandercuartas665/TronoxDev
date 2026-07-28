namespace Tronox.Domain.Enums;

/// <summary>
/// Momento del flujo en que se solicita un metadato (RQ02 - RF04 nota tabla trd_metadatos).
/// Distingue los metadatos creados en RF04 (del expediente) de los de RF05 (del documento).
/// </summary>
public enum ContextoMetadato
{
    /// <summary>Se diligencia al CREAR el expediente (metadatos de RF04, paso 6).</summary>
    Expediente = 0,

    /// <summary>Se diligencia al CARGAR cada documento (metadatos de RF05).</summary>
    Documento = 1
}
