using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Registro de firma electronica (RQ05 - RF05), calca FIR_FIRMAS del legacy. La firma es una dimension
/// independiente del estado de archivado del documento (un documento puede archivarse sin firmar). Dos
/// caminos:
///  - Firma directa (auto-firma): el usuario logueado firma su propio documento Terminado. Se inserta
///    una fila <see cref="EstadoFirma.Firmado"/> y el documento pasa a
///    <see cref="EstadoFirmaDocumento.Firmado"/>. Sin stepper, sin OTP (calca FirmaDirectaHelper).
///  - Solicitud a otro (RF06): fila <see cref="EstadoFirma.Pendiente"/>; el firmante la cumple con el
///    stepper de 5 pasos (Lectura/Identidad/Consentimiento/OTP/Resultado) del modulo RQ05 completo.
///
/// TENANT-SCOPED. El firmante es un PlatformUser (mismo espacio de id que ITenantContext.UserId).
/// TIMESTAMP_FIRMA usa la hora del servidor en el primer slice (la hora legal NTP - RF12 - queda para
/// el modulo RQ05 completo). Sin eliminacion real (invariante 8): una solicitud se cancela, no se borra.
/// </summary>
public class Firma : TenantEntity
{
    public long DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    /// <summary>Firmante: PlatformUserId (mismo id que el usuario logueado).</summary>
    public long FirmanteUserId { get; set; }

    /// <summary>Snapshot del firmante al momento de la firma/solicitud (nombre, cargo, dependencia).</summary>
    public string NombreFirmante { get; set; } = string.Empty;
    public string? CargoFirmante { get; set; }
    public string? DependenciaFirmante { get; set; }

    public TipoFirma TipoFirma { get; set; } = TipoFirma.Electronica;
    public EstadoFirma Estado { get; set; } = EstadoFirma.Pendiente;

    /// <summary>Hash SHA-256 del PDF sellado (integridad, RNF). Null mientras la solicitud esta pendiente.</summary>
    public string? HashDocumento { get; set; }

    /// <summary>Momento de la firma efectiva (server UtcNow en el slice 1). Null si pendiente.</summary>
    public DateTimeOffset? TimestampFirma { get; set; }

    public string? IpFirma { get; set; }
    public string? SesionId { get; set; }

    /// <summary>La solicitud exige OTP al firmante (RF06 -> stepper). En firma directa siempre false.</summary>
    public bool OtpRequerido { get; set; }

    /// <summary>Quien solicito la firma (PlatformUserId). En firma directa = el propio firmante.</summary>
    public long SolicitadoPor { get; set; }

    /// <summary>Circuito multi-firmante (RF07) al que pertenece esta firma. Null = firma individual.</summary>
    public long? CircuitoId { get; set; }

    // ---- Contexto de la solicitud para la bandeja "Mis Firmas" (RF10). Calca FIR_FIRMAS. ----

    /// <summary>Prioridad de la solicitud (semaforo de la bandeja). Default Media.</summary>
    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Media;

    /// <summary>Fecha limite opcional de la solicitud (informativa; alimenta el badge de urgencia).</summary>
    public DateOnly? FechaLimite { get; set; }

    /// <summary>Instrucciones del solicitante para el firmante (opcional).</summary>
    public string? Instrucciones { get; set; }

    /// <summary>Etiqueta libre opcional (agrupacion en la bandeja).</summary>
    public string? Tag { get; set; }

    /// <summary>Comentario del firmante al rechazar (OBLIGATORIO si Rechazado).</summary>
    public string? ComentarioRechazo { get; set; }

    /// <summary>
    /// Ultima vez que se alerto al firmante de esta solicitud pendiente (RF11 Inc.2). Null = nunca.
    /// Controla la frecuencia de re-alerta (FirmaConfig.FirmaFrecuenciaDias) para no repetir a diario.
    /// </summary>
    public DateTimeOffset? UltimaAlertaAt { get; set; }
}
