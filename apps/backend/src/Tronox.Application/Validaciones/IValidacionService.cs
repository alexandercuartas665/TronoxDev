namespace Tronox.Application.Validaciones;

/// <summary>
/// Tareas de validacion de documentos (RQ04 - RF11 solicitar / RF12 Mis Tareas). La validacion es un
/// flujo de metadatos paralelo: NO cambia el estado del documento. Solo el usuario asignado responde.
/// Diferido: circuitos secuencial/paralelo, notificaciones por correo, badge del menu en tiempo real.
/// </summary>
public interface IValidacionService
{
    /// <summary>RF11: crea una tarea de Revision/Aprobacion sobre un documento del solicitante.</summary>
    Task<ValidacionResult<long>> SolicitarAsync(
        SolicitarValidacionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF12: tareas Pendientes asignadas al usuario (opcional por tipo).</summary>
    Task<IReadOnlyList<TareaItemDto>> ListarPendientesAsync(
        long actorUserId, Domain.Enums.TipoValidacion? tipo = null, CancellationToken cancellationToken = default);

    /// <summary>Tareas ya respondidas por el usuario (historial).</summary>
    Task<IReadOnlyList<TareaHistorialDto>> ListarHistorialAsync(long actorUserId, CancellationToken cancellationToken = default);

    Task<TareaContadoresDto> GetContadoresAsync(long actorUserId, CancellationToken cancellationToken = default);

    Task<ValidacionResult<TareaItemDto>> GetDetalleAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>RF12: responde una tarea (Aprobado/Devuelto/Rechazado). Solo el asignado; solo Pendiente.</summary>
    Task<ValidacionResult<bool>> ResponderAsync(
        long id, Domain.Enums.EstadoValidacion nuevoEstado, string? comentario, long actorUserId,
        CancellationToken cancellationToken = default);

    // ---- Apoyo para el formulario de solicitud (RF11) ----

    Task<IReadOnlyList<UsuarioAsignableDto>> GetUsuariosAsignablesAsync(long actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoSolicitarDto>> GetDocumentosParaSolicitarAsync(
        long actorUserId, string? texto = null, CancellationToken cancellationToken = default);
}
