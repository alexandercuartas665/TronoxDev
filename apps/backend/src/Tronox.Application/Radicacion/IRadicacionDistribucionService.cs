namespace Tronox.Application.Radicacion;

/// <summary>
/// Distribucion de radicados (RQ09 RF07, port de rad_tramites action=distribuir). Asigna un radicado a
/// una dependencia y (opcional) funcionario: crea la tarea, cambia el estado y escribe trazabilidad.
/// TODO en UNA transaccion (un solo SaveChanges), a diferencia del legacy. Resultado tipado.
/// </summary>
public interface IRadicacionDistribucionService
{
    Task<DistribuirResult> DistribuirAsync(DistribuirRequest request, CancellationToken ct = default);

    /// <summary>Distribucion circular interna (rad_interna_wizard): crea N tareas (una por dependencia) sobre
    /// un radicado recien creado, en una sola transaccion. La cabecera toma el primer destino; el funcionario
    /// de cabecera solo se fija con un unico destino. Estado EnTramite si hay funcionario directo, si no Distribuido.</summary>
    Task<DistribuirResult> DistribuirCircularAsync(DistribuirCircularRequest request, CancellationToken ct = default);
}
