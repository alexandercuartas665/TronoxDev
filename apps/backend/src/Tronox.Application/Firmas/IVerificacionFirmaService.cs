namespace Tronox.Application.Firmas;

/// <summary>Un firmante en el resultado publico de verificacion.</summary>
public sealed record VerificacionFirmanteDto(string Nombre, DateTimeOffset? Fecha, string TipoFirma);

/// <summary>Resultado publico de la verificacion de una firma por documento (RQ05 - RF04, portal verificador).</summary>
public sealed record VerificacionResultado(
    long DocumentoId,
    string Entidad,
    string DocumentoNombre,
    bool Firmado,
    string EstadoFirma,
    string? HashRegistrado,
    IReadOnlyList<VerificacionFirmanteDto> Firmantes);

/// <summary>
/// Verificacion PUBLICA de una firma por su documento (RQ05 - RF04, portal verificador). Es cross-tenant
/// y SIN sesion: resuelve el documento por id (el que estampa la cajita/QR: verificar.tronox.co/v/{id}) y
/// devuelve solo datos probatorios (entidad, estado, hash registrado, firmantes). NUNCA expone el binario
/// ni el contenido del documento. Ignora el filtro de tenant a proposito (superficie publica).
/// </summary>
public interface IVerificacionFirmaService
{
    Task<VerificacionResultado?> VerificarAsync(long docId, CancellationToken cancellationToken = default);
}
