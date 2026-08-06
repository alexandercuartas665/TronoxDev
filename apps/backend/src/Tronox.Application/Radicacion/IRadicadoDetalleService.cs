namespace Tronox.Application.Radicacion;

/// <summary>
/// Detalle de un radicado (RQ09, port de rad_detalle). Arma el DTO equivalente al JSON de action=detalle
/// (info + docs + traza + tareas + comunicaciones + padre + salidas), gateado por la visibilidad del
/// usuario (fail-closed). Solo lectura.
/// </summary>
public interface IRadicadoDetalleService
{
    /// <summary>Devuelve el detalle, o null si no existe o el usuario no puede verlo.</summary>
    Task<RadicadoDetalleDto?> ObtenerAsync(long radicadoId, CancellationToken ct = default);
}
