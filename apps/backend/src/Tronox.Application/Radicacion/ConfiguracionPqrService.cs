using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Implementacion de Configuracion PQR (Prioridades + Portal Web). Siembra 3 prioridades base la primera
/// vez; las base no se eliminan (invariante 8, se inactivan). El portal config es singleton por tenant.
/// Los tipos publicados usan TipoComunicacion.HabilitadoWeb.
/// </summary>
public sealed class ConfiguracionPqrService : IConfiguracionPqrService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public ConfiguracionPqrService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private long TenantId => _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

    // ---- Prioridades ----
    public async Task<IReadOnlyList<PrioridadDto>> ListarPrioridadesAsync(CancellationToken ct = default)
    {
        if (!await _db.RadPrioridades.AnyAsync(ct)) { await SembrarBaseAsync(ct); }
        return await _db.RadPrioridades.AsNoTracking().OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new PrioridadDto
            {
                Id = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Icono = p.Icono, Color = p.Color,
                SlaSugerido = p.SlaSugerido, Activo = p.Activo, EsBase = p.EsBase, Orden = p.Orden
            }).ToListAsync(ct);
    }

    private async Task SembrarBaseAsync(CancellationToken ct)
    {
        var tid = TenantId;
        var baseP = new[]
        {
            ("NORMAL", "Normal", "\U0001F7E2", "#0ab39c", 1),
            ("ALTA", "Alta", "\U0001F7E0", "#f59e0b", 2),
            ("URGENTE", "Urgente", "\U0001F534", "#ef4444", 3),
        };
        foreach (var (cod, nom, ico, col, ord) in baseP)
        {
            _db.RadPrioridades.Add(new RadPrioridad { TenantId = tid, Codigo = cod, Nombre = nom, Icono = ico, Color = col, Orden = ord, Activo = true, EsBase = true });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PrioridadDto> CrearPrioridadAsync(CancellationToken ct = default)
    {
        var p = new RadPrioridad
        {
            TenantId = TenantId,
            Codigo = $"PRIO_{DateTime.UtcNow:HHmmssfff}",
            Nombre = "Nueva prioridad",
            Color = "#64748b",
            Orden = 99,
            Activo = true,
            EsBase = false
        };
        _db.RadPrioridades.Add(p);
        await _db.SaveChangesAsync(ct);
        return new PrioridadDto { Id = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Icono = p.Icono, Color = p.Color, SlaSugerido = p.SlaSugerido, Activo = p.Activo, EsBase = p.EsBase, Orden = p.Orden };
    }

    public async Task GuardarPrioridadesAsync(IReadOnlyList<PrioridadDto> prioridades, CancellationToken ct = default)
    {
        var ids = prioridades.Select(p => p.Id).ToList();
        var rows = await _db.RadPrioridades.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        foreach (var r in rows)
        {
            var dto = prioridades.First(p => p.Id == r.Id);
            r.Nombre = dto.Nombre;
            r.Icono = dto.Icono;
            r.Color = dto.Color;
            r.SlaSugerido = dto.SlaSugerido;
            r.Activo = dto.Activo;
            r.Orden = dto.Orden;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> InactivarPrioridadAsync(long id, CancellationToken ct = default)
    {
        var p = await _db.RadPrioridades.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null || p.EsBase) { return false; }
        p.Activo = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Portal Web ----
    public async Task<PortalConfigDto> GetPortalConfigAsync(CancellationToken ct = default)
    {
        var c = await _db.RadPortalConfigs.FirstOrDefaultAsync(ct);
        if (c is null)
        {
            var entidad = await _db.Entidades.AsNoTracking().FirstOrDefaultAsync(ct);
            c = new RadPortalConfig
            {
                TenantId = TenantId,
                NombreEntidad = entidad?.RazonSocial,
                MaxAdjuntoMb = 20,
                PermitirAnonimo = true,
                ExigirCaptcha = true,
                Slug = entidad?.Sigla?.ToLowerInvariant()
            };
            _db.RadPortalConfigs.Add(c);
            await _db.SaveChangesAsync(ct);
        }
        return new PortalConfigDto
        {
            NombreEntidad = c.NombreEntidad, Subtitulo = c.Subtitulo, Nit = c.Nit, Color = c.Color,
            MaxAdjuntoMb = c.MaxAdjuntoMb, Banner = c.Banner, PermitirAnonimo = c.PermitirAnonimo,
            ExigirCaptcha = c.ExigirCaptcha, CanalesAtencion = c.CanalesAtencion,
            AvisoPrivacidad = c.AvisoPrivacidad, Faq = c.Faq, Slug = c.Slug
        };
    }

    public async Task GuardarPortalConfigAsync(PortalConfigDto dto, CancellationToken ct = default)
    {
        var c = await _db.RadPortalConfigs.FirstOrDefaultAsync(ct);
        if (c is null) { c = new RadPortalConfig { TenantId = TenantId }; _db.RadPortalConfigs.Add(c); }
        c.NombreEntidad = dto.NombreEntidad;
        c.Subtitulo = dto.Subtitulo;
        c.Nit = dto.Nit;
        c.Color = dto.Color;
        c.MaxAdjuntoMb = dto.MaxAdjuntoMb <= 0 ? 20 : dto.MaxAdjuntoMb;
        c.Banner = dto.Banner;
        c.PermitirAnonimo = dto.PermitirAnonimo;
        c.ExigirCaptcha = dto.ExigirCaptcha;
        c.CanalesAtencion = dto.CanalesAtencion;
        c.AvisoPrivacidad = dto.AvisoPrivacidad;
        c.Faq = dto.Faq;
        c.Slug = string.IsNullOrWhiteSpace(dto.Slug) ? c.Slug : dto.Slug.Trim().ToLowerInvariant();
        await _db.SaveChangesAsync(ct);
    }

    // ---- Tipos publicados en el portal ----
    public async Task<IReadOnlyList<TipoWebDto>> ListarTiposWebAsync(CancellationToken ct = default)
        => await _db.TiposComunicacion.AsNoTracking()
            .Where(t => t.Activo && t.Direccion == RadicacionDireccion.Entrada)
            .OrderBy(t => t.OrdenPortal ?? 999).ThenBy(t => t.Nombre)
            .Select(t => new TipoWebDto(t.Id, t.Nombre, t.Color, t.HabilitadoWeb, t.OrdenPortal, t.DescripcionCiudadano))
            .ToListAsync(ct);

    public async Task ToggleTipoWebAsync(long tipoId, bool habilitado, CancellationToken ct = default)
    {
        var t = await _db.TiposComunicacion.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) { return; }
        t.HabilitadoWeb = habilitado;
        await _db.SaveChangesAsync(ct);
    }
}
