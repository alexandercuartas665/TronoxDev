namespace Tronox.Application.Radicacion;

/// <summary>
/// Modulo "Correos por Revisar" (RQ09 RF04, port de rad_correos). Opera sobre correos ya capturados en
/// los buzones: listar, leer, radicar (nuevo o vincular como respuesta), editar el pre-radicado, descartar
/// (log inmutable) y recuperar. Tenant-scoped. La captura IMAP/Graph esta diferida (worker); "Simular"
/// siembra un correo de prueba en dev.
/// </summary>
public interface IRadicacionCorreosService
{
    Task<CorreosListaDto> ListarAsync(string tab, CancellationToken ct = default);
    Task<CorreoDetalleDto?> DetalleAsync(long reg, CancellationToken ct = default);
    Task<CorreoResult> RadicarAsync(long reg, long? tipoOverrideId, bool vincular, CancellationToken ct = default);
    Task<CorreoResult> EditarAsync(EditarCorreoRequest req, CancellationToken ct = default);
    Task<CorreoResult> DescartarAsync(long reg, string causal, CancellationToken ct = default);
    Task<CorreoResult> RecuperarAsync(long reg, CancellationToken ct = default);
    Task<IReadOnlyList<OpcionDto>> TiposEntradaAsync(CancellationToken ct = default);
    Task<CorreoResult> SimularAsync(CancellationToken ct = default);
}
