using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Radicacion.Correos;
using Tronox.Domain.Enums;
using Tronox.Web.Auth;

namespace Tronox.Web.Services;

/// <summary>
/// Proceso de fondo de ingesta de correos con IA (RQ16, Correos->PQR). Cada ciclo recorre los buzones
/// activos (cross-tenant), fija el tenant ambient y ejecuta ICorreoIngestaService (captura IMAP + IA +
/// radicar). Best-effort por buzon. El host de Workers no se despliega a prod, por eso vive aqui como
/// BackgroundService (mismo patron que FirmaAlertasHostedService). Los buzones Manual tambien se procesan
/// (capturan y clasifican), pero solo dejan pendiente; Semi/Automatico radican.
/// </summary>
public sealed class CorreoIngestaHostedService : BackgroundService
{
    private static readonly TimeSpan RetrasoInicial = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CorreoIngestaHostedService> _log;

    public CorreoIngestaHostedService(IServiceScopeFactory scopes, ILogger<CorreoIngestaHostedService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(RetrasoInicial, stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Intervalo);
        do
        {
            try { await EjecutarCicloAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Ciclo de ingesta de correos fallo"); }
        }
        while (await SiguienteAsync(timer, stoppingToken));
    }

    private static async Task<bool> SiguienteAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        // Buzones activos con IMAP y clave, cross-tenant (se ignora el filtro global).
        List<(long TenantId, long BuzonId)> buzones;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            buzones = await db.BuzonesCorreo.IgnoreQueryFilters()
                .Where(b => b.Activo && b.Protocolo == BuzonProtocolo.Imap && b.ContrasenaEncrypted != null)
                .Select(b => new ValueTuple<long, long>(b.TenantId, b.Id))
                .ToListAsync(ct);
        }

        foreach (var (tid, buzonId) in buzones)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using (AmbientTenantContext.Begin(tid))
                using (var scope = _scopes.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<ICorreoIngestaService>();
                    var r = await svc.ProcesarBuzonAsync(buzonId, ct);
                    if (r.Ok && (r.Pqr > 0 || r.Radicados > 0))
                    {
                        _log.LogInformation("Ingesta correo (tenant {Tenant}, buzon {Buzon}): {Txt}", tid, buzonId, r.Texto);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "Ingesta de correo fallo para el buzon {Buzon}", buzonId); }
        }
    }
}
