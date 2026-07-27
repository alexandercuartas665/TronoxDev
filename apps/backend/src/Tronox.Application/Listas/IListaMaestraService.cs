namespace Tronox.Application.Listas;

/// <summary>
/// Administrador de Listas (RQ02 - RF03): listas de opciones reutilizables que alimentan los
/// metadatos de tipo Lista en RF04/RF05. Maestro-detalle Lista -> Opciones.
/// </summary>
public interface IListaMaestraService
{
    Task<IReadOnlyList<ListaMaestraDto>> ListAsync(
        bool includeInactivas = true, CancellationToken cancellationToken = default);

    Task<ListaMaestraDto?> GetAsync(long listaId, CancellationToken cancellationToken = default);

    Task<ListaKpisDto> GetKpisAsync(CancellationToken cancellationToken = default);

    // ---- Lista ----

    Task<ListaResult<ListaMaestraDto>> CreateListaAsync(
        SaveListaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<ListaResult<ListaMaestraDto>> UpdateListaAsync(
        long listaId, SaveListaRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Inactiva o reactiva una lista (invariante 8: nunca borrado fisico).</summary>
    Task<ListaResult<bool>> SetListaEstadoAsync(
        long listaId, bool activar, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default);

    // ---- Opciones ----

    /// <summary>Agrega una opcion a la lista. El orden se asigna al final. Clave unica en la lista.</summary>
    Task<ListaResult<ListaOpcionDto>> AddOpcionAsync(
        long listaId, SaveOpcionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    Task<ListaResult<ListaOpcionDto>> UpdateOpcionAsync(
        long opcionId, SaveOpcionRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inactiva o reactiva una opcion. Inactivar no borra los valores ya guardados (RF03 3.3.4-4):
    /// solo deja de ofrecerse en el desplegable.
    /// </summary>
    Task<ListaResult<bool>> SetOpcionEstadoAsync(
        long opcionId, bool activar, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reordena las opciones de una lista (drag and drop, RF03 3.3.4-5). Recibe los ids en el orden
    /// deseado; reasigna el campo orden. Los ids deben ser exactamente los de la lista.
    /// </summary>
    Task<ListaResult<bool>> ReordenarOpcionesAsync(
        long listaId, IReadOnlyList<long> opcionIdsEnOrden, long actorUserId,
        CancellationToken cancellationToken = default);
}
