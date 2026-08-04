namespace Tronox.Application.Expedientes;

/// <summary>
/// Casos de uso de la bandeja de expedientes (RQ03 - RF01/RF03/RF04/RF10). Primer slice: listar
/// (5 vistas, fail-closed por clasificacion), crear (codigo estructurado + herencia + metadatos),
/// editar metadatos y eliminar (logico). Diferido a slices posteriores: detalle completo, cierre/
/// reapertura + indice/firma, transferencias, compartir, ubicacion, alertas, exportadores.
/// </summary>
public interface IExpedienteService
{
    /// <summary>Bandeja principal: filas visibles para el usuario (fail-closed) + estadisticas.</summary>
    Task<ExpedienteBandejaDto> GetBandejaAsync(
        BandejaVista vista, ExpedienteFiltro filtro, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Fondos activos para el primer nivel de la cascada de creacion.</summary>
    Task<IReadOnlyList<FondoOpcionDto>> GetFondosAsync(CancellationToken cancellationToken = default);

    /// <summary>Dependencias con al menos una serie asignada en la TRD Vigente (opcional por fondo).</summary>
    Task<IReadOnlyList<DependenciaOpcionDto>> GetDependenciasParaCrearAsync(
        long? fondoId, CancellationToken cancellationToken = default);

    /// <summary>Series asignables (asignaciones de la version Vigente) para una dependencia.</summary>
    Task<IReadOnlyList<SerieOpcionDto>> GetSeriesParaCrearAsync(
        long dependenciaId, CancellationToken cancellationToken = default);

    /// <summary>Definiciones de metadatos de expediente de la serie elegida (DAT-04).</summary>
    Task<IReadOnlyList<MetadatoDefDto>> GetMetadatosSerieAsync(
        long trdAsignacionId, CancellationToken cancellationToken = default);

    /// <summary>Niveles de clasificacion activos (para el desplegable de "solo elevar").</summary>
    Task<IReadOnlyList<NivelClasificacionOpcionDto>> GetNivelesAsync(CancellationToken cancellationToken = default);

    /// <summary>Crea un expediente (RF03/RF04): valida herencia, obligatorios y emite el codigo.</summary>
    Task<ExpedienteResult<ExpedienteDetalleDto>> CrearAsync(
        CrearExpedienteRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Detalle basico del expediente, respetando la clasificacion fail-closed.</summary>
    Task<ExpedienteResult<ExpedienteDetalleDto>> GetDetalleAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Edita nombre, clasificacion (solo elevar) y metadatos. Solo si esta Abierto.</summary>
    Task<ExpedienteResult<ExpedienteDetalleDto>> EditarAsync(
        long id, EditarExpedienteRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Eliminacion logica con justificacion y auditoria (invariante 8).</summary>
    Task<ExpedienteResult<bool>> EliminarAsync(
        long id, string justificacion, long actorUserId, CancellationToken cancellationToken = default);
}
