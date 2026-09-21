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

/// <summary>Config de almacenamiento de binarios (Azure Blob) por entidad. La cadena de conexion NUNCA
/// se devuelve en claro: en la lectura solo se informa si esta configurada.</summary>
public sealed class AlmacenamientoDto
{
    /// <summary>true si ya hay una cadena de conexion guardada (cifrada).</summary>
    public bool Configurado { get; set; }
    public bool Activo { get; set; }
    public string Contenedor { get; set; } = "tronox-documentos";
    public string? Prefijo { get; set; }
    /// <summary>Solo ESCRITURA: si viene con valor se cifra y reemplaza; si va vacio se conserva la actual.</summary>
    public string? ConnectionString { get; set; }
}

/// <summary>Config del OCR (Azure Computer Vision) por entidad. La API key NUNCA se devuelve en claro.</summary>
public sealed class OcrConfigDto
{
    public bool Configurado { get; set; }
    public bool Activo { get; set; }
    public string? Endpoint { get; set; }
    /// <summary>Solo de ENTRADA al guardar; nunca se devuelve poblada.</summary>
    public string? ApiKey { get; set; }
}

public interface IEntidadConfigExtraService
{
    Task<SeguridadDto> GetSeguridadAsync(CancellationToken ct = default);
    Task GuardarSeguridadAsync(SeguridadDto dto, CancellationToken ct = default);
    Task<FirmaConfigDto> GetFirmaAsync(CancellationToken ct = default);
    Task GuardarFirmaAsync(FirmaConfigDto dto, CancellationToken ct = default);
    Task<AlmacenamientoDto> GetAlmacenamientoAsync(CancellationToken ct = default);
    Task GuardarAlmacenamientoAsync(AlmacenamientoDto dto, CancellationToken ct = default);
    /// <summary>Prueba la conexion contra Azure Blob con la cadena/contenedor dados. Devuelve error o null si OK.</summary>
    Task<string?> ProbarAlmacenamientoAsync(string connectionString, string contenedor, CancellationToken ct = default);

    // ---- OCR (Azure Computer Vision) por entidad, RQ04 RF04 ----
    Task<OcrConfigDto> GetOcrAsync(CancellationToken ct = default);
    Task GuardarOcrAsync(OcrConfigDto dto, CancellationToken ct = default);
}

/// <summary>
/// Sub-configuraciones embebidas en Datos de la Entidad: Parametros de Seguridad (RQ01) y Firma
/// Electronica (RQ05 RF01). Cada una es singleton por tenant; se crea con defaults en la primera lectura.
/// </summary>
public sealed class EntidadConfigExtraService : IEntidadConfigExtraService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _protector;
    private readonly IBlobConnectionTester _blobTester;

    public EntidadConfigExtraService(
        IApplicationDbContext db, ITenantContext tenant, ISecretProtector protector, IBlobConnectionTester blobTester)
    {
        _db = db;
        _tenant = tenant;
        _protector = protector;
        _blobTester = blobTester;
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

    // ---- Almacenamiento de binarios (Azure Blob por entidad, ADR-012) ----

    public async Task<AlmacenamientoDto> GetAlmacenamientoAsync(CancellationToken ct = default)
    {
        var a = await _db.AlmacenamientosConfig.AsNoTracking().FirstOrDefaultAsync(ct);
        return new AlmacenamientoDto
        {
            Configurado = a is not null && !string.IsNullOrWhiteSpace(a.ConnectionStringCifrada),
            Activo = a?.Activo ?? false,
            Contenedor = a?.Contenedor ?? "tronox-documentos",
            Prefijo = a?.Prefijo,
            ConnectionString = null // el secreto nunca se devuelve
        };
    }

    public async Task GuardarAlmacenamientoAsync(AlmacenamientoDto d, CancellationToken ct = default)
    {
        var a = await _db.AlmacenamientosConfig.FirstOrDefaultAsync(ct);
        if (a is null) { a = new AlmacenamientoConfig { TenantId = TenantId }; _db.AlmacenamientosConfig.Add(a); }

        // Si viene una cadena nueva, se cifra y reemplaza; si va vacia, se conserva la actual.
        if (!string.IsNullOrWhiteSpace(d.ConnectionString))
        {
            a.ConnectionStringCifrada = _protector.Protect(d.ConnectionString.Trim());
        }
        a.Contenedor = string.IsNullOrWhiteSpace(d.Contenedor) ? "tronox-documentos" : d.Contenedor.Trim();
        a.Prefijo = string.IsNullOrWhiteSpace(d.Prefijo) ? null : d.Prefijo.Trim();
        a.Activo = d.Activo && !string.IsNullOrWhiteSpace(a.ConnectionStringCifrada);
        await _db.SaveChangesAsync(ct);
    }

    // ---- OCR (Azure Computer Vision) por entidad, calcado del patron de Almacenamiento ----

    public async Task<OcrConfigDto> GetOcrAsync(CancellationToken ct = default)
    {
        var o = await _db.OcrConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
        return new OcrConfigDto
        {
            Configurado = o is not null && !string.IsNullOrWhiteSpace(o.ApiKeyCifrada),
            Activo = o?.Activo ?? false,
            Endpoint = o?.Endpoint,
            ApiKey = null // el secreto nunca se devuelve
        };
    }

    public async Task GuardarOcrAsync(OcrConfigDto d, CancellationToken ct = default)
    {
        var o = await _db.OcrConfigs.FirstOrDefaultAsync(ct);
        if (o is null) { o = new OcrConfig { TenantId = TenantId }; _db.OcrConfigs.Add(o); }

        o.Endpoint = string.IsNullOrWhiteSpace(d.Endpoint) ? null : d.Endpoint.Trim();
        // Si viene una llave nueva, se cifra y reemplaza; si va vacia, se conserva la actual.
        if (!string.IsNullOrWhiteSpace(d.ApiKey))
        {
            o.ApiKeyCifrada = _protector.Protect(d.ApiKey.Trim());
        }
        // Solo puede quedar activo si hay endpoint + llave cifrada.
        o.Activo = d.Activo && !string.IsNullOrWhiteSpace(o.Endpoint) && !string.IsNullOrWhiteSpace(o.ApiKeyCifrada);
        await _db.SaveChangesAsync(ct);
    }

    public Task<string?> ProbarAlmacenamientoAsync(string connectionString, string contenedor, CancellationToken ct = default)
        => _blobTester.TestAsync(connectionString, string.IsNullOrWhiteSpace(contenedor) ? "tronox-documentos" : contenedor, ct);
}
