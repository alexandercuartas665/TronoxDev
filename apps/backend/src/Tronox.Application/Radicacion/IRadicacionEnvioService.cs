namespace Tronox.Application.Radicacion;

/// <summary>
/// Ciclo de envio de un radicado de SALIDA (RF05-6, port de rad_salida "Registrar envio" + constancia).
/// Marca ESTADO_ENVIO, registra la comunicacion (RAD_COMUNICACIONES) y, en canal EMAIL, envia el
/// documento principal por correo. La constancia/acta se arma como HTML imprimible. Tenant-scoped.
/// </summary>
public interface IRadicacionEnvioService
{
    Task<RegistrarEnvioResult> RegistrarEnvioAsync(RegistrarEnvioRequest request, CancellationToken ct = default);

    /// <summary>HTML autonomo de la constancia de envio (o acta de notificacion judicial si el canal es
    /// JUDICIAL) para imprimir desde el detalle. Null si el radicado no existe o no es una salida.</summary>
    Task<string?> ConstanciaHtmlAsync(long radicadoId, CancellationToken ct = default);
}

/// <summary>Datos para registrar el envio de una salida. Los campos usados dependen del canal:
/// EMAIL -> Correo; FISICO -> Guia; PERSONAL -> Recibe; JUDICIAL -> Juzgado + Expediente + FechaNotificacion.</summary>
public sealed record RegistrarEnvioRequest(
    long RadicadoId,
    string Canal,
    string? Correo = null,
    string? Guia = null,
    string? Recibe = null,
    string? Juzgado = null,
    string? Expediente = null,
    DateOnly? FechaNotificacion = null);

public sealed record RegistrarEnvioResult(bool Ok, string? Error = null, string? EstadoEnvio = null)
{
    public static RegistrarEnvioResult Fail(string error) => new(false, error);
    public static RegistrarEnvioResult Success(string estado) => new(true, null, estado);
}
