using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Datos de la Entidad (RQ01 - RF01 seccion 4.1.1). UNA sola entidad por tenant: no hay
/// listado ni "crear otra", solo Get + Save. Todo tenant-scoped por el filtro global.
/// </summary>
public interface IEntidadService
{
    /// <summary>La entidad del tenant activo, o null si todavia no se ha registrado.</summary>
    Task<EntidadDto?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea la entidad si el tenant no tiene ninguna, o actualiza la existente. Nunca crea una
    /// segunda (criterio 1 de RF01). El codigo de fondo AGN se genera aqui, no se recibe.
    /// </summary>
    Task<ArchivisticaResult<EntidadDto>> SaveAsync(
        SaveEntidadRequest request, long actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado (Activo / Inactivo / Suspendido) con motivo y auditoria. Es la UNICA
    /// baja posible: la entidad no se elimina (criterio 8 de RF01, invariante 8).
    /// </summary>
    Task<ArchivisticaResult<EntidadDto>> CambiarEstadoAsync(
        EntidadEstado estado, string? motivo, long actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Catalogos territoriales DIVIPOLA (pendiente P-02 de RQ01, resuelto por precarga en base de
/// datos). Son catalogos GLOBALES de plataforma: las tres consultas son de solo lectura y no
/// dependen del tenant activo.
/// </summary>
public interface IDivipolaService
{
    Task<IReadOnlyList<PaisDto>> ListPaisesAsync(CancellationToken cancellationToken = default);

    /// <summary>Departamentos del pais indicado (selector encadenado, criterio 5 de RF01).</summary>
    Task<IReadOnlyList<DepartamentoDto>> ListDepartamentosAsync(
        long paisId, CancellationToken cancellationToken = default);

    /// <summary>Municipios del departamento indicado (selector encadenado, criterio 5 de RF01).</summary>
    Task<IReadOnlyList<MunicipioDto>> ListMunicipiosAsync(
        long departamentoId, CancellationToken cancellationToken = default);

    Task<MunicipioDto?> GetMunicipioAsync(long municipioId, CancellationToken cancellationToken = default);
}
