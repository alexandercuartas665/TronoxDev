using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Documentos;
using Tronox.Domain.Enums;
using Tronox.Web.Auth;

namespace Tronox.Web.Services;

/// <summary>
/// OCR automatico al incorporar (RQ04 - RF04). Los documentos con binario indexable nacen con
/// OcrEstado=Pendiente; este BackgroundService (el host de Workers no se despliega a prod) recorre los
/// tenants con pendientes, y para los que tienen OCR configurado (OcrConfig activa) ejecuta el OCR real
/// (Azure Computer Vision) de un lote acotado por ciclo. Best-effort por documento; el OcrService marca
/// Completado/Error, asi que un pendiente se procesa una sola vez (sin bucles). Reemplaza el "Reprocesar"
/// manual como via por defecto, dejandolo como respaldo.
/// </summary>
public sealed class OcrAutoHostedService : BackgroundService
{
    private static readonly TimeSpan RetrasoInicial = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);
    private const int LotePorTenant = 10;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OcrAutoHostedService> _log;

    public OcrAutoHostedService(IServiceScopeFactory scopes, ILogger<OcrAutoHostedService> log)
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
            catch (Exception ex) { _log.LogError(ex, "Ciclo de auto-OCR fallo"); }
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
        List<long> tenantIds;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            tenantIds = await db.Documentos.IgnoreQueryFilters()
                .Where(d => d.OcrEstado == OcrEstadoDocumento.Pendiente && d.TieneBinario)
                .Select(d => d.TenantId)
                .Distinct()
                .ToListAsync(ct);
        }

        foreach (var tid in tenantIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using (AmbientTenantContext.Begin(tid))
                {
                    // Solo procesa si el tenant tiene OCR configurado y activo.
                    List<long> pendientes;
                    using (var scope = _scopes.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                        var cfg = await db.OcrConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
                        if (cfg is null || !cfg.Activo || string.IsNullOrWhiteSpace(cfg.Endpoint) || string.IsNullOrWhiteSpace(cfg.ApiKeyCifrada))
                        {
                            continue;
                        }
                        pendientes = await db.Documentos
                            .Where(d => d.OcrEstado == OcrEstadoDocumento.Pendiente && d.TieneBinario)
                            .OrderBy(d => d.Id).Take(LotePorTenant)
                            .Select(d => d.Id).ToListAsync(ct);
                    }

                    foreach (var docId in pendientes)
                    {
                        ct.ThrowIfCancellationRequested();
                        using var scope = _scopes.CreateScope();
                        var ocr = scope.ServiceProvider.GetRequiredService<IOcrService>();
                        var r = await ocr.ReprocesarAsync(docId, 0, ct);
                        if (r.Ok) { _log.LogInformation("Auto-OCR completado (tenant {Tenant}, doc {Doc})", tid, docId); }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "Auto-OCR fallo para el tenant {Tenant}", tid); }
        }
    }
}
