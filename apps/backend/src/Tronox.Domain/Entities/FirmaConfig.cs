using Tronox.Domain.Common;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion de firma electronica de la entidad (legacy FIR_CONFIG, RQ05 RF01). Singleton por tenant.
/// Embebida en la pantalla de Datos de la Entidad (el modulo de firma RQ05 la reutiliza). Los valores de
/// seleccion se guardan como texto (posicion/tamano QR/modo OTP/canal). TENANT-SCOPED.
/// </summary>
public class FirmaConfig : TenantEntity
{
    // Activacion y hora oficial (NTP). Activo por defecto: el admin puede apagarlo.
    public bool ModuloFirmaActivo { get; set; } = true;
    public bool NtpActivo { get; set; }
    public string? NtpServidor { get; set; } = "pool.ntp.org";

    // Apariencia de la firma / QR.
    public string FirmaPosicionDefault { get; set; } = "pie_derecho"; // pie_derecho/pie_izquierdo/centrado
    public string FirmaQrTamano { get; set; } = "mediano";            // pequeno/mediano
    public string? FirmaTextoDefault { get; set; }
    public bool FirmaMostrarQr { get; set; } = true;
    public bool FirmaMostrarIdentificacion { get; set; } = true;
    public bool FirmaMostrarNombre { get; set; } = true;

    // OTP.
    public string OtpModo { get; set; } = "opcional";  // siempre/opcional/nunca
    public int OtpExpiracionMinutos { get; set; } = 5;
    public string OtpCanal { get; set; } = "correo";   // correo/sms
    public bool OtpRequeridoGlobal { get; set; }

    // Alertas y firma masiva.
    public int FirmaDias { get; set; } = 3;
    public int FirmaFrecuenciaDias { get; set; } = 1;
    public bool FirmaMasivaActiva { get; set; } = true;

    // Experiencia del firmante (RF08).
    public bool FirmaForzarLectura { get; set; }
    public bool FirmaConsentimiento { get; set; } = true;
}
