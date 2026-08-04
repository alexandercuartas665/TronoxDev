namespace Tronox.Application.Plantillas;

/// <summary>
/// Plantillas documentales (RQ04 - RF09): CRUD de configuracion. Se asocian a N tipologias y se
/// consumen al crear documentos (RF10, diferido). Sin borrado fisico: activar/inactivar.
/// </summary>
public interface IPlantillaService
{
    Task<IReadOnlyList<PlantillaItemDto>> ListarAsync(
        string? texto = null, bool incluirInactivas = true, CancellationToken cancellationToken = default);

    Task<PlantillaDetalleDto?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<PlantillaResult<PlantillaDetalleDto>> CrearAsync(
        SavePlantillaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<PlantillaResult<PlantillaDetalleDto>> ActualizarAsync(
        long id, SavePlantillaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Activa/Inactiva la plantilla (toggle). Sin borrado fisico (invariante 8).</summary>
    Task<PlantillaResult<bool>> CambiarEstadoAsync(long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Tipologias documentales disponibles para asociar (RQ02 - RF05).</summary>
    Task<IReadOnlyList<TipologiaOpcionDto>> GetTipologiasAsync(CancellationToken cancellationToken = default);

    /// <summary>Variables disponibles: base (Sistema/Expediente/Firma/Terceros) + metadatos de las tipologias.</summary>
    Task<IReadOnlyList<VariableDto>> GetVariablesAsync(
        IReadOnlyList<long> tipologiaIds, CancellationToken cancellationToken = default);
}
