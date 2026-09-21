namespace Tronox.Application.Common;

/// <summary>
/// Hora legal para el sellado de firma (RQ05 - RF02), port de NtpHelper del legacy. Cuando la entidad
/// activa NTP (FirmaConfig.NtpActivo + NtpServidor), devuelve la hora obtenida por SNTP (UDP/123) del
/// servidor configurado (por defecto hora.mintic.gov.co), con cache del offset. Es BEST-EFFORT: si NTP
/// no esta activo o no responde, devuelve la hora del servidor (UtcNow); la firma nunca se frena por NTP.
/// </summary>
public interface INtpTimeProvider
{
    /// <summary>Hora UTC actual, por NTP si <paramref name="activo"/> y el servidor responde; si no, la del servidor.</summary>
    Task<DateTimeOffset> ObtenerAsync(bool activo, string? servidor, CancellationToken cancellationToken = default);
}
