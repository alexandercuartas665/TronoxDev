using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Correo capturado de un buzon configurado (RF01-4), pendiente de convertirse en radicado. Espejo del
/// legacy RAD_CORREOS. El Panel de Control cuenta los Pendiente ("Correos por revisar", agrupados por
/// buzon). El modulo completo (bandeja de correos, radicar desde correo) llega al portar rad_correos.aspx.
/// TENANT-SCOPED.
/// </summary>
public class CorreoRecibido : TenantEntity
{
    /// <summary>Buzon del que se capturo (RF01-4). FK a BuzonCorreo. NO ACTION.</summary>
    public long? BuzonCorreoId { get; set; }
    public BuzonCorreo? BuzonCorreo { get; set; }

    /// <summary>Direccion del buzon (denormalizada, como el legacy BUZON_EMAIL: agrupa el KPI).</summary>
    public string? BuzonEmail { get; set; }

    public CorreoRevisionEstado Estado { get; set; } = CorreoRevisionEstado.Pendiente;

    /// <summary>Nombre del remitente (Remitente) y su email (separados, como el legacy).</summary>
    public string? Remitente { get; set; }
    public string? RemitenteEmail { get; set; }
    public string? Asunto { get; set; }
    public DateTime? FechaRecepcion { get; set; }

    // ---- Deduplicacion de captura (IMAP/Graph, worker diferido). ----
    public string? MessageId { get; set; }
    public string? InReplyTo { get; set; }

    /// <summary>Cuerpo tratado (HTML->texto, hilo cortado, firma excluida). El original va como adjunto.</summary>
    public string? CuerpoTratado { get; set; }

    // ---- Deteccion automatica (RF04-2): tipo detectado + confianza + duplicado + referencia. ----
    /// <summary>Tipo de comunicacion detectado (FK TipoComunicacion). NO ACTION. 100 de confianza = confirmado por operador.</summary>
    public long? TipoDetectadoId { get; set; }
    public int Confianza { get; set; }
    /// <summary>Numero de un radicado similar detectado a 30 dias (posible duplicado).</summary>
    public string? DuplicadoNumero { get; set; }
    /// <summary>Numero de radicado que este correo referencia (para vincular como respuesta, RF04-5).</summary>
    public string? RadicadoRef { get; set; }

    // ---- Modo de radicacion y temporizador (semi-automatico). ----
    public BuzonModoRadicacion Modo { get; set; } = BuzonModoRadicacion.Manual;
    /// <summary>Instante en que el job auto-radicaria el correo (modo Semi). Null si Manual.</summary>
    public DateTime? RadicaEn { get; set; }

    public int NumAdjuntos { get; set; }

    /// <summary>Radicado generado al procesar el correo (si Estado = Radicado). NO ACTION.</summary>
    public long? RadicadoId { get; set; }
    /// <summary>Numero del radicado generado (denormalizado para la vista).</summary>
    public string? RadicadoNumero { get; set; }

    public ICollection<CorreoRecibidoAdjunto> Adjuntos { get; set; } = new List<CorreoRecibidoAdjunto>();
}
