using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Tarea de validacion de un documento (RQ04 - RF11/RF12): una solicitud de Revision o Aprobacion
/// asignada a un usuario. TENANT-SCOPED.
///
/// INVARIANTE (RF11 CA-1/CA-2): la validacion NO cambia el estado del documento ni bloquea el
/// archivado. Es un registro de metadatos de trazabilidad, INMUTABLE una vez respondido. Vive en
/// paralelo al ciclo de vida del documento.
///
/// La creacion (RF11) se inicia desde un documento; la resolucion (RF12) ocurre en "Mis Tareas" y
/// solo la puede hacer el <see cref="UsuarioAsignadoId"/>.
/// </summary>
public class DocumentoValidacion : TenantEntity
{
    public long DocumentoId { get; set; }
    public Documento? Documento { get; set; }

    public TipoValidacion Tipo { get; set; }

    /// <summary>Usuario que debe revisar/aprobar. Solo el puede responder (RF12).</summary>
    public long UsuarioAsignadoId { get; set; }
    public TenantUser? UsuarioAsignado { get; set; }

    /// <summary>Snapshot del nombre del asignado al asignar (traza estable).</summary>
    public string? NombreAsignado { get; set; }
    public string? CargoAsignado { get; set; }

    public EstadoValidacion Estado { get; set; } = EstadoValidacion.Pendiente;

    public PrioridadTarea Prioridad { get; set; } = PrioridadTarea.Media;

    /// <summary>Fecha limite opcional (semaforo de la bandeja). No es un SLA (DAT-06); es informativa.</summary>
    public DateOnly? FechaLimite { get; set; }

    /// <summary>Instrucciones del solicitante (opcional).</summary>
    public string? Instrucciones { get; set; }

    /// <summary>Comentario de la respuesta. OBLIGATORIO si Devuelto o Rechazado.</summary>
    public string? Comentarios { get; set; }

    /// <summary>Timestamp de la respuesta. Null mientras Pendiente. La fecha de asignacion es CreatedAt.</summary>
    public DateTime? FechaRespuesta { get; set; }
}
