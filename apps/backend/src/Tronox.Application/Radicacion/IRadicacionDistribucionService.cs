namespace Tronox.Application.Radicacion;

/// <summary>
/// Distribucion de radicados (RQ09 RF07, port de rad_tramites action=distribuir). Asigna un radicado a
/// una dependencia y (opcional) funcionario: crea la tarea, cambia el estado y escribe trazabilidad.
/// TODO en UNA transaccion (un solo SaveChanges), a diferencia del legacy. Resultado tipado.
/// </summary>
public interface IRadicacionDistribucionService
{
    Task<DistribuirResult> DistribuirAsync(DistribuirRequest request, CancellationToken ct = default);
}
