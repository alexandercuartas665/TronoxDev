using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Time;

/// <summary>
/// Hora legal por SNTP (UDP/123), port de NtpHelper del legacy (RFC 2030). Cachea el offset (hora NTP -
/// reloj local) por servidor durante 10 minutos y devuelve UtcNow + offset. Best-effort: ante cualquier
/// fallo (NTP inactivo, DNS, timeout) devuelve UtcNow. Singleton.
/// </summary>
public sealed class NtpTimeProvider : INtpTimeProvider
{
    private const string ServidorDefault = "hora.mintic.gov.co";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private static readonly DateTime Epoca1900 = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILogger<NtpTimeProvider> _log;
    private readonly ConcurrentDictionary<string, (double OffsetMs, DateTimeOffset At)> _cache = new();

    public NtpTimeProvider(ILogger<NtpTimeProvider> log) => _log = log;

    public async Task<DateTimeOffset> ObtenerAsync(bool activo, string? servidor, CancellationToken cancellationToken = default)
    {
        if (!activo) { return DateTimeOffset.UtcNow; }
        var srv = string.IsNullOrWhiteSpace(servidor) ? ServidorDefault : servidor.Trim();

        if (_cache.TryGetValue(srv, out var c) && DateTimeOffset.UtcNow - c.At < CacheTtl)
        {
            return DateTimeOffset.UtcNow.AddMilliseconds(c.OffsetMs);
        }

        try
        {
            var ntpUtc = await ConsultarAsync(srv, cancellationToken);
            var offsetMs = (ntpUtc - DateTime.UtcNow).TotalMilliseconds;
            _cache[srv] = (offsetMs, DateTimeOffset.UtcNow);
            return DateTimeOffset.UtcNow.AddMilliseconds(offsetMs);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "NTP {Servidor} no respondio; se usa la hora del servidor", srv);
            // Si ya habia un offset previo, se conserva; si no, hora local.
            return _cache.TryGetValue(srv, out var prev)
                ? DateTimeOffset.UtcNow.AddMilliseconds(prev.OffsetMs)
                : DateTimeOffset.UtcNow;
        }
    }

    private static async Task<DateTime> ConsultarAsync(string servidor, CancellationToken cancellationToken)
    {
        var data = new byte[48];
        data[0] = 0x1B;   // LI=0, VN=3, Mode=3 (cliente)

        using var udp = new UdpClient();
        udp.Connect(servidor, 123);
        await udp.SendAsync(data, cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);
        var resp = await udp.ReceiveAsync(cts.Token);
        var b = resp.Buffer;
        if (b.Length < 48) { throw new InvalidOperationException("Respuesta NTP incompleta."); }

        // Transmit Timestamp: bytes 40-43 (segundos) y 44-47 (fraccion), big-endian.
        ulong segundos = ((ulong)b[40] << 24) | ((ulong)b[41] << 16) | ((ulong)b[42] << 8) | b[43];
        ulong fraccion = ((ulong)b[44] << 24) | ((ulong)b[45] << 16) | ((ulong)b[46] << 8) | b[47];
        if (segundos == 0) { throw new InvalidOperationException("Transmit Timestamp NTP nulo."); }
        ulong ms = segundos * 1000UL + ((fraccion * 1000UL) >> 32);
        return Epoca1900.AddMilliseconds(ms);
    }
}
