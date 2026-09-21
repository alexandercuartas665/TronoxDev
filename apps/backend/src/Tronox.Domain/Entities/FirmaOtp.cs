using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Codigo OTP de un solo uso para la verificacion de identidad en la firma (RQ05 - RF08), calca
/// FIR_OTP del legacy. Se guarda SOLO el hash SHA-256 del codigo (nunca en claro). Vigencia acotada
/// (minutos); al generar uno nuevo se invalidan los vigentes previos de la misma firma. Un solo uso:
/// <see cref="VerificadoAt"/> se sella al validar correctamente. TENANT-SCOPED.
/// </summary>
public class FirmaOtp : TenantEntity
{
    /// <summary>Solicitud de firma (Firma pendiente) a la que pertenece el OTP.</summary>
    public long FirmaId { get; set; }
    public Firma? Firma { get; set; }

    /// <summary>Firmante al que se envio el codigo (PlatformUserId).</summary>
    public long Usuario { get; set; }

    /// <summary>Identificador del lote (RF09): un solo OTP cubre todo el lote. Null en OTP de firma individual.</summary>
    public string? LoteId { get; set; }

    /// <summary>Hash SHA-256 del codigo de 6 digitos (hex). Nunca se guarda el codigo en claro.</summary>
    public string CodigoHash { get; set; } = string.Empty;

    /// <summary>Canal de envio (por ahora "correo").</summary>
    public string Canal { get; set; } = "correo";

    public DateTimeOffset ExpiraAt { get; set; }

    /// <summary>Intentos de validacion (auditable). Se incrementa en cada intento.</summary>
    public int Intentos { get; set; }

    /// <summary>Momento de la validacion correcta. Null = aun no verificado (o invalidado).</summary>
    public DateTimeOffset? VerificadoAt { get; set; }
}
