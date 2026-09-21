using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Application.Notifications;
using Tronox.Application.Radicacion;
using Tronox.Domain.Enums;

namespace Tronox.Application.Firmas;

/// <summary>
/// Implementacion de las alertas de firma pendiente (RF11 Inc.2). Calca FirmaAlertaRepository del legacy:
/// lee las firmas Pendientes, calcula el vencimiento con dias HABILES (ICalendarioHabilService, el mismo
/// calendario de la entidad que usa el SLA de radicacion) sobre FirmaConfig.FirmaDias, y avisa al
/// firmante. Marca UltimaAlertaAt para respetar FirmaFrecuenciaDias. Best-effort en la emision: un fallo
/// de correo/campana no frena el resto del lote.
/// </summary>
public sealed class FirmaAlertaService : IFirmaAlertaService
{
    private readonly IApplicationDbContext _db;
    private readonly ICalendarioHabilService _cal;
    private readonly INotificationService _notif;
    private readonly IEmailSender _email;

    public FirmaAlertaService(
        IApplicationDbContext db, ICalendarioHabilService cal, INotificationService notif, IEmailSender email)
    {
        _db = db;
        _cal = cal;
        _notif = notif;
        _email = email;
    }

    public async Task<int> EscanearYAlertarAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await _db.FirmaConfigs.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var moduloActivo = cfg?.ModuloFirmaActivo ?? true;
        var firmaDias = cfg?.FirmaDias ?? 3;
        var frecuencia = Math.Max(1, cfg?.FirmaFrecuenciaDias ?? 1);
        if (!moduloActivo || firmaDias <= 0) { return 0; }

        var pendientes = await _db.Firmas
            .Include(f => f.Documento)
            .Where(f => f.Estado == EstadoFirma.Pendiente)
            .ToListAsync(cancellationToken);
        if (pendientes.Count == 0) { return 0; }

        var hoy = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var alertadas = 0;

        foreach (var f in pendientes)
        {
            var fechaSolicitud = DateOnly.FromDateTime(f.CreatedAt.UtcDateTime);
            // Vence cuando pasan FirmaDias dias habiles desde la solicitud (calendario de la entidad).
            var umbral = await _cal.SumarDiasHabilesAsync(fechaSolicitud, firmaDias, cancellationToken);
            if (hoy < umbral) { continue; }

            if (f.UltimaAlertaAt is DateTimeOffset ua)
            {
                var proxima = await _cal.SumarDiasHabilesAsync(
                    DateOnly.FromDateTime(ua.UtcDateTime), frecuencia, cancellationToken);
                if (hoy < proxima) { continue; }   // aun dentro de la ventana de frecuencia
            }

            var doc = f.Documento?.Nombre ?? "un documento";
            await NotificarAsync(
                f.FirmanteUserId,
                "Firma pendiente",
                $"Tienes una firma pendiente del documento \"{doc}\" que lleva mas de {firmaDias} dia(s) habil(es) esperando.",
                cancellationToken);
            f.UltimaAlertaAt = DateTimeOffset.UtcNow;
            alertadas++;
        }

        if (alertadas > 0) { await _db.SaveChangesAsync(cancellationToken); }
        return alertadas;
    }

    /// <summary>Campana in-app (INotificationService) + correo (IEmailSender), ambos best-effort. Calca NotificarFirmaAsync.</summary>
    private async Task NotificarAsync(long destinoPlatformUserId, string titulo, string cuerpo, CancellationToken ct)
    {
        try
        {
            var tuId = await _notif.ResolveTenantUserIdAsync(destinoPlatformUserId, ct);
            if (tuId is long tid)
            {
                await _notif.CreateAsync(tid, NotificationKind.TaskAssigned, titulo, cuerpo, linkRoute: "modulo/firmas-mis", cancellationToken: ct);
            }
        }
        catch { /* best-effort: la campana no frena el lote */ }

        try
        {
            var correo = await _db.PlatformUsers.AsNoTracking()
                .Where(p => p.Id == destinoPlatformUserId).Select(p => p.Email).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(correo))
            {
                await _email.SendAsync(correo, $"{titulo} - TRONOX", $"<p><strong>{titulo}</strong></p><p>{cuerpo}</p>", ct);
            }
        }
        catch { /* best-effort */ }
    }
}
