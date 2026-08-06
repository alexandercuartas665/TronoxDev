namespace Tronox.Application.Radicacion;

/// <summary>
/// Portal ciudadano de radicacion (RQ09 RF03, port de rad_portal). Superficie PUBLICA (sin login):
/// radicar PQRSD + consultar estado. El tenant se resuelve por SLUG server-side (no por query
/// manipulable, invariante 1). El radicar reutiliza RadicadorService. La consulta es de solo lectura y
/// solo expone informacion publica (sin funcionario ni notas internas).
/// </summary>
public interface IPortalCiudadanoService
{
    /// <summary>Resuelve el tenant del portal por su slug (bypass del filtro global). Null si no existe.</summary>
    Task<long?> ResolverTenantAsync(string slug, CancellationToken ct = default);

    /// <summary>Datos publicos del portal (branding + tipos publicados). Requiere el scope de tenant activo.</summary>
    Task<PortalPublicoDto?> GetPortalAsync(CancellationToken ct = default);

    Task<PortalRadicarResult> RadicarAsync(PortalRadicarRequest request, CancellationToken ct = default);

    Task<PortalConsultaResult> ConsultarAsync(string numero, string documento, CancellationToken ct = default);
}
