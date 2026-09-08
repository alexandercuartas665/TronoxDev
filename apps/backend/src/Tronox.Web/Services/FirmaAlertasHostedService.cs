using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Firmas;
using Tronox.Domain.Enums;
using Tronox.Web.Auth;

namespace Tronox.Web.Services;

/// <summary>
/// Proceso de fondo de las alertas de firma pendiente (RQ05 - RF11 Inc.2). El host de Workers no se
/// despliega a prod (solo la app), asi que el escaneo vive aqui como BackgroundService. Cada ciclo
/// recorre los tenants con firmas pendientes, fija el tenant ambient (AmbientTenantContext.Begin) y
/// ejecuta IFirmaAlertaService en un scope propio. Best-effort por tenant: un fallo no frena a los demas.
/// La frecuencia de re-alerta la controla el propio servicio (UltimaAlertaAt + FirmaFrecuenciaDias).
/// </summary>
public sealed class FirmaAlertasHostedService : BackgroundService
{
    private static readonly TimeSpan RetrasoInicial = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<FirmaAlertasHostedService> _log;

    public FirmaAlertasHostedService(IServiceScopeFactory scopes, ILogger<FirmaAlertasHostedService> log)
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
            catch (Exception ex) { _log.LogError(ex, "Ciclo de alertas de firma fallo"); }
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
        // Tenants con al menos una firma pendiente (cross-tenant -> se ignora el filtro global).
        List<long> tenantIds;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            tenantIds = await db.Firmas.IgnoreQueryFilters()
                .Where(f => f.Estado == EstadoFirma.Pendiente)
                .Select(f => f.TenantId)
                .Distinct()
                .ToListAsync(ct);
        }

        foreach (var tid in tenantIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using (AmbientTenantContext.Begin(tid))
                using (var scope = _scopes.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<IFirmaAlertaService>();
                    var n = await svc.EscanearYAlertarAsync(ct);
                    if (n > 0) { _log.LogInformation("Alertas de firma emitidas (tenant {Tenant}): {N}", tid, n); }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "Alertas de firma fallaron para el tenant {Tenant}", tid); }
        }
    }
}
