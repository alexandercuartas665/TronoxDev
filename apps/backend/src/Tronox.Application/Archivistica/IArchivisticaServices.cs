namespace Tronox.Application.Archivistica;

/// <summary>
/// Niveles de clasificacion documental del tenant (RQ01 - RF01-P.3). Los 4 canonicos los siembra
/// IClasificacionProvisioningService al crear el tenant; este servicio los administra despues.
/// </summary>
public interface INivelClasificacionService
{
    Task<IReadOnlyList<NivelClasificacionDto>> ListAsync(bool soloActivos = false, CancellationToken cancellationToken = default);
    Task<NivelClasificacionDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<ArchivisticaResult<NivelClasificacionDto>> SaveAsync(
        SaveNivelClasificacionRequest request, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>Activa/desactiva el nivel. Nunca se borra fisicamente (invariante 8).</summary>
    Task<ArchivisticaResult<bool>> SetActivoAsync(
        long id, bool activo, string? motivo, long actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>Sedes de la entidad (RQ01 - RF01 seccion 4.1.2). Opcionales.</summary>
public interface ISedeService
{
    Task<IReadOnlyList<SedeDto>> ListAsync(bool soloActivas = false, CancellationToken cancellationToken = default);
    Task<SedeDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<ArchivisticaResult<SedeDto>> SaveAsync(
        SaveSedeRequest request, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>Inactiva la sede con motivo y auditoria. Nunca borrado fisico.</summary>
    Task<ArchivisticaResult<bool>> InactivarAsync(
        long id, string? motivo, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Sedes que SI se pueden ofrecer al crear un fondo: solo las Activas
    /// (criterio de aceptacion de RF01 4.1.2).
    /// </summary>
    Task<IReadOnlyList<SedeDto>> ListSeleccionablesParaFondoAsync(CancellationToken cancellationToken = default);
}

/// <summary>Fondos documentales (RQ01 - RF02).</summary>
public interface IFondoService
{
    Task<IReadOnlyList<FondoDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<FondoDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<ArchivisticaResult<FondoDto>> SaveAsync(
        SaveFondoRequest request, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>Cierra el fondo (pasa a solo lectura) con fecha de cierre y auditoria.</summary>
    Task<ArchivisticaResult<FondoDto>> CerrarAsync(
        long id, DateOnly fechaCierre, string? motivo, long actorUserId, CancellationToken cancellationToken = default);
    Task<ArchivisticaResult<FondoDto>> InactivarAsync(
        long id, string? motivo, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Regla 4 de RF02: un fondo con dependencias NO se elimina. Devuelve Invalid con el mensaje
    /// que sugiere Inactivar o Cerrar. Ver FondoService.ContarDependenciasAsync.
    /// </summary>
    Task<ArchivisticaResult<bool>> DeleteAsync(
        long id, long actorUserId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Puerta de entrada de TODO modulo que quiera crear algo colgando de un fondo (subfondos hoy;
    /// series y expedientes cuando existan). Devuelve Invalid si el fondo esta Cerrado.
    /// </summary>
    Task<ArchivisticaResult<bool>> EnsureAdmiteAltasAsync(
        long fondoId, CancellationToken cancellationToken = default);
}

/// <summary>Subfondos (RQ01 - RF02 seccion 5.2.2). Opcionales.</summary>
public interface ISubfondoService
{
    Task<IReadOnlyList<SubfondoDto>> ListAsync(long fondoId, CancellationToken cancellationToken = default);
    Task<SubfondoDto?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<ArchivisticaResult<SubfondoDto>> SaveAsync(
        SaveSubfondoRequest request, long actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Aprovisionamiento de los NIVELES DE CLASIFICACION de un tenant (RQ01 - RF01-P.3).
///
/// Cuelga del camino de ALTA DEL TENANT, igual que IMenuProvisioningService y por la misma razon
/// (trampa heredada del backbone: lo que se siembra desde un seeder de demo deja a los clientes
/// creados por el panel sin datos base). La implementacion vive en Infrastructure.
/// </summary>
public interface IClasificacionProvisioningService
{
    /// <summary>
    /// Siembra los 4 niveles canonicos si el tenant aun no tiene NINGUNO. IDEMPOTENTE: si ya
    /// tiene niveles, no hace nada (no reintroduce los que el tenant haya modificado).
    /// </summary>
    Task EnsureNivelesClasificacionAsync(long tenantId, CancellationToken cancellationToken = default);
}
