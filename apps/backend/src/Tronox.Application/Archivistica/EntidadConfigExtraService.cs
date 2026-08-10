using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Archivistica;

/// <summary>Parametros de seguridad (mutable para @bind).</summary>
public sealed class SeguridadDto
{
    public int IntentosFallidosMax { get; set; } = 5;
    public int TiempoInactividadSesionMin { get; set; } = 30;
    public int VigenciaContrasenaDias { get; set; } = 90;
    public int HistorialContrasenas { get; set; } = 5;
    public int LongitudMinimaContrasena { get; set; } = 8;
    public int? TiempoAvisoVencimientoDias { get; set; } = 7;
    public bool ComplejidadContrasena { get; set; } = true;
}

/// <summary>Configuracion de firma electronica (mutable para @bind).</summary>
public sealed class FirmaConfigDto
{
    public bool ModuloFirmaActivo { get; set; }
    public bool NtpActivo { get; set; }
    public string? NtpServidor { get; set; } = "pool.ntp.org";
    public string FirmaPosicionDefault { get; set; } = "pie_derecho";
    public string FirmaQrTamano { get; set; } = "mediano";
    public string? FirmaTextoDefault { get; set; }
    public bool FirmaMostrarQr { get; set; } = true;
    public bool FirmaMostrarIdentificacion { get; set; } = true;
    public bool FirmaMostrarNombre { get; set; } = true;
    public string OtpModo { get; set; } = "opcional";
    public int OtpExpiracionMinutos { get; set; } = 5;
    public string OtpCanal { get; set; } = "correo";
    public bool OtpRequeridoGlobal { get; set; }
    public int FirmaDias { get; set; } = 3;
    public int FirmaFrecuenciaDias { get; set; } = 1;
    public bool FirmaMasivaActiva { get; set; }
    public bool FirmaForzarLectura { get; set; }
    public bool FirmaConsentimiento { get; set; } = true;
}

public interface IEntidadConfigExtraService
{
    Task<SeguridadDto> GetSeguridadAsync(CancellationToken ct = default);
    Task GuardarSeguridadAsync(SeguridadDto dto, CancellationToken ct = default);
    Task<FirmaConfigDto> GetFirmaAsync(CancellationToken ct = default);
    Task GuardarFirmaAsync(FirmaConfigDto dto, CancellationToken ct = default);
}

/// <summary>
/// Sub-configuraciones embebidas en Datos de la Entidad: Parametros de Seguridad (RQ01) y Firma
/// Electronica (RQ05 RF01). Cada una es singleton por tenant; se crea con defaults en la primera lectura.
/// </summary>
public sealed class EntidadConfigExtraService : IEntidadConfigExtraService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public EntidadConfigExtraService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private long TenantId => _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

    public async Task<SeguridadDto> GetSeguridadAsync(CancellationToken ct = default)
    {
        var s = await _db.ParametrosSeguridad.FirstOrDefaultAsync(ct);
        if (s is null) { s = new ParametrosSeguridad { TenantId = TenantId }; _db.ParametrosSeguridad.Add(s); await _db.SaveChangesAsync(ct); }
        return new SeguridadDto
        {
            IntentosFallidosMax = s.IntentosFallidosMax,
            TiempoInactividadSesionMin = s.TiempoInactividadSesionMin,
            VigenciaContrasenaDias = s.VigenciaContrasenaDias,
            HistorialContrasenas = s.HistorialContrasenas,
            LongitudMinimaContrasena = s.LongitudMinimaContrasena,
            TiempoAvisoVencimientoDias = s.TiempoAvisoVencimientoDias,
            ComplejidadContrasena = s.ComplejidadContrasena
        };
    }

    public async Task GuardarSeguridadAsync(SeguridadDto d, CancellationToken ct = default)
    {
        var s = await _db.ParametrosSeguridad.FirstOrDefaultAsync(ct) ?? Add();
        s.IntentosFallidosMax = d.IntentosFallidosMax;
        s.TiempoInactividadSesionMin = d.TiempoInactividadSesionMin;
        s.VigenciaContrasenaDias = d.VigenciaContrasenaDias;
        s.HistorialContrasenas = d.HistorialContrasenas;
        s.LongitudMinimaContrasena = d.LongitudMinimaContrasena;
        s.TiempoAvisoVencimientoDias = d.TiempoAvisoVencimientoDias;
        s.ComplejidadContrasena = d.ComplejidadContrasena;
        await _db.SaveChangesAsync(ct);

        ParametrosSeguridad Add() { var n = new ParametrosSeguridad { TenantId = TenantId }; _db.ParametrosSeguridad.Add(n); return n; }
    }

    public async Task<FirmaConfigDto> GetFirmaAsync(CancellationToken ct = default)
    {
        var f = await _db.FirmaConfigs.FirstOrDefaultAsync(ct);
        if (f is null) { f = new FirmaConfig { TenantId = TenantId }; _db.FirmaConfigs.Add(f); await _db.SaveChangesAsync(ct); }
        return new FirmaConfigDto
        {
            ModuloFirmaActivo = f.ModuloFirmaActivo, NtpActivo = f.NtpActivo, NtpServidor = f.NtpServidor,
            FirmaPosicionDefault = f.FirmaPosicionDefault, FirmaQrTamano = f.FirmaQrTamano, FirmaTextoDefault = f.FirmaTextoDefault,
            FirmaMostrarQr = f.FirmaMostrarQr, FirmaMostrarIdentificacion = f.FirmaMostrarIdentificacion, FirmaMostrarNombre = f.FirmaMostrarNombre,
            OtpModo = f.OtpModo, OtpExpiracionMinutos = f.OtpExpiracionMinutos, OtpCanal = f.OtpCanal, OtpRequeridoGlobal = f.OtpRequeridoGlobal,
            FirmaDias = f.FirmaDias, FirmaFrecuenciaDias = f.FirmaFrecuenciaDias, FirmaMasivaActiva = f.FirmaMasivaActiva,
            FirmaForzarLectura = f.FirmaForzarLectura, FirmaConsentimiento = f.FirmaConsentimiento
        };
    }

    public async Task GuardarFirmaAsync(FirmaConfigDto d, CancellationToken ct = default)
    {
        var f = await _db.FirmaConfigs.FirstOrDefaultAsync(ct) ?? Add();
        f.ModuloFirmaActivo = d.ModuloFirmaActivo; f.NtpActivo = d.NtpActivo; f.NtpServidor = d.NtpServidor;
        f.FirmaPosicionDefault = d.FirmaPosicionDefault; f.FirmaQrTamano = d.FirmaQrTamano; f.FirmaTextoDefault = d.FirmaTextoDefault;
        f.FirmaMostrarQr = d.FirmaMostrarQr; f.FirmaMostrarIdentificacion = d.FirmaMostrarIdentificacion; f.FirmaMostrarNombre = d.FirmaMostrarNombre;
        f.OtpModo = d.OtpModo; f.OtpExpiracionMinutos = d.OtpExpiracionMinutos; f.OtpCanal = d.OtpCanal; f.OtpRequeridoGlobal = d.OtpRequeridoGlobal;
        f.FirmaDias = d.FirmaDias; f.FirmaFrecuenciaDias = d.FirmaFrecuenciaDias; f.FirmaMasivaActiva = d.FirmaMasivaActiva;
        f.FirmaForzarLectura = d.FirmaForzarLectura; f.FirmaConsentimiento = d.FirmaConsentimiento;
        await _db.SaveChangesAsync(ct);

        FirmaConfig Add() { var n = new FirmaConfig { TenantId = TenantId }; _db.FirmaConfigs.Add(n); return n; }
    }
}
