using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Terceros;

/// <summary>
/// Implementacion del Catalogo de Terceros (RQ07 RF01). LINQ tenant-scoped, sin eliminacion real, dedup
/// por documento y creacion rapida (upsert) desde otros modulos. Resultado tipado, sin fuga de excepciones.
/// </summary>
public sealed class TerceroService : ITerceroService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public TerceroService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private static readonly TerceroSubtipo[] Juridicas =
    {
        TerceroSubtipo.JuridicaPrivada, TerceroSubtipo.JuridicaPublica,
        TerceroSubtipo.JuridicaMixta, TerceroSubtipo.ExtranjeraJuridica
    };

    public static string SubtipoLabel(TerceroSubtipo s) => s switch
    {
        TerceroSubtipo.PersonaNatural => "Persona Natural",
        TerceroSubtipo.JuridicaPrivada => "Juridica Privada",
        TerceroSubtipo.JuridicaPublica => "Juridica Publica",
        TerceroSubtipo.JuridicaMixta => "Juridica Mixta",
        TerceroSubtipo.ExtranjeraNatural => "Extranjera Natural",
        TerceroSubtipo.ExtranjeraJuridica => "Extranjera Juridica",
        _ => s.ToString()
    };

    private static bool EsJuridica(TerceroSubtipo s) => Juridicas.Contains(s);

    private static string NombreDe(TerceroSubtipo s, string? razon, string? nombre, string? apellidos)
        => EsJuridica(s)
            ? (razon ?? "(sin razon social)")
            : $"{nombre} {apellidos}".Trim() is { Length: > 0 } n ? n : "(sin nombre)";

    public async Task<TerceroListResult> ListarAsync(TerceroFiltro f, CancellationToken ct = default)
    {
        var q = _db.Terceros.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Buscar) && f.Buscar.Trim().Length >= 3)
        {
            var b = f.Buscar.Trim();
            q = q.Where(t => t.NumeroDocumento.Contains(b)
                || (t.RazonSocial != null && t.RazonSocial.Contains(b))
                || (t.Nombre != null && t.Nombre.Contains(b))
                || (t.Apellidos != null && t.Apellidos.Contains(b))
                || (t.NombreComercial != null && t.NombreComercial.Contains(b))
                || (t.Email != null && t.Email.Contains(b)));
        }
        if (f.Grupo == "juridica") { q = q.Where(t => Juridicas.Contains(t.Subtipo)); }
        else if (f.Grupo == "natural") { q = q.Where(t => !Juridicas.Contains(t.Subtipo)); }
        if (f.Estado is TerceroEstado est) { q = q.Where(t => t.Estado == est); }
        if (f.SoloIncompletos == true) { q = q.Where(t => !t.PerfilCompleto); }

        var total = await q.CountAsync(ct);
        var top = f.Top <= 0 ? 1000 : f.Top;
        var rows = await q
            .OrderBy(t => t.RazonSocial ?? t.Apellidos ?? t.Nombre)
            .Take(top)
            .Select(t => new
            {
                t.Id, t.Subtipo, t.TipoDocumento, t.NumeroDocumento, t.RazonSocial, t.Nombre, t.Apellidos,
                t.Email, t.Telefono, t.Estado, t.PerfilCompleto
            })
            .ToListAsync(ct);

        var items = rows.Select(t => new TerceroListItemDto(
            t.Id, NombreDe(t.Subtipo, t.RazonSocial, t.Nombre, t.Apellidos), t.Subtipo, SubtipoLabel(t.Subtipo),
            t.TipoDocumento, t.NumeroDocumento,
            string.IsNullOrWhiteSpace(t.Email) ? t.Telefono : t.Email,
            t.Estado, t.PerfilCompleto)).ToList();

        return new TerceroListResult(items, total);
    }

    public async Task<TerceroFichaDto?> ObtenerAsync(long id, CancellationToken ct = default)
        => await _db.Terceros.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new TerceroFichaDto(
                t.Id, t.Subtipo, t.TipoDocumento, t.NumeroDocumento, t.DigitoVerificador,
                t.RazonSocial, t.Nombre, t.Apellidos, t.NombreComercial, t.Email, t.Telefono, t.Direccion,
                t.MunicipioId, t.PaisId, t.SitioWeb, t.SectorEconomico, t.RegimenTributario,
                t.SectorAdministrativo, t.OrdenEntidad, t.NaturalezaJuridica, t.RepresentanteLegalId,
                t.Observaciones, t.Estado, t.PerfilCompleto))
            .FirstOrDefaultAsync(ct);

    public async Task<TerceroResult> GuardarAsync(TerceroFichaDto ficha, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return TerceroResult.Fail("Sesion no valida."); }

        var tipoDoc = (ficha.TipoDocumento ?? "").Trim();
        var numDoc = (ficha.NumeroDocumento ?? "").Trim();
        if (string.IsNullOrEmpty(tipoDoc) || string.IsNullOrEmpty(numDoc))
        {
            return TerceroResult.Fail("El tipo y numero de documento son obligatorios.");
        }
        var esJuridica = EsJuridica(ficha.Subtipo);
        if (esJuridica && string.IsNullOrWhiteSpace(ficha.RazonSocial))
        {
            return TerceroResult.Fail("La razon social es obligatoria para personas juridicas.");
        }
        if (!esJuridica && (string.IsNullOrWhiteSpace(ficha.Nombre) || string.IsNullOrWhiteSpace(ficha.Apellidos)))
        {
            return TerceroResult.Fail("Nombres y apellidos son obligatorios para personas naturales.");
        }
        if (string.IsNullOrWhiteSpace(ficha.Email))
        {
            return TerceroResult.Fail("El correo electronico principal es obligatorio.");
        }

        // Dedup: no puede existir OTRO tercero con el mismo (tipo, numero) en el tenant.
        var dupId = await _db.Terceros.AsNoTracking()
            .Where(t => t.TipoDocumento == tipoDoc && t.NumeroDocumento == numDoc && t.Id != ficha.Id)
            .Select(t => (long?)t.Id).FirstOrDefaultAsync(ct);
        if (dupId is not null)
        {
            return TerceroResult.Fail($"Ya existe un tercero con documento {tipoDoc} {numDoc}.");
        }

        Tercero t;
        if (ficha.Id > 0)
        {
            t = await _db.Terceros.FirstOrDefaultAsync(x => x.Id == ficha.Id, ct)
                ?? throw new InvalidOperationException("Tercero no encontrado.");
        }
        else
        {
            t = new Tercero { TenantId = tenantId.Value, Origen = "Directorio" };
            _db.Terceros.Add(t);
        }

        t.Subtipo = ficha.Subtipo;
        t.TipoDocumento = tipoDoc;
        t.NumeroDocumento = numDoc;
        t.DigitoVerificador = ficha.Subtipo is TerceroSubtipo.JuridicaPrivada or TerceroSubtipo.JuridicaPublica
            or TerceroSubtipo.JuridicaMixta ? ficha.DigitoVerificador : null;
        t.RazonSocial = esJuridica ? ficha.RazonSocial?.Trim() : null;
        t.NombreComercial = esJuridica ? ficha.NombreComercial?.Trim() : null;
        t.Nombre = esJuridica ? null : ficha.Nombre?.Trim();
        t.Apellidos = esJuridica ? null : ficha.Apellidos?.Trim();
        t.Email = ficha.Email?.Trim();
        t.Telefono = ficha.Telefono?.Trim();
        t.Direccion = ficha.Direccion?.Trim();
        t.MunicipioId = ficha.MunicipioId;
        t.PaisId = ficha.PaisId;
        t.SitioWeb = ficha.SitioWeb?.Trim();
        t.SectorEconomico = ficha.SectorEconomico?.Trim();
        t.RegimenTributario = ficha.RegimenTributario?.Trim();
        t.SectorAdministrativo = ficha.SectorAdministrativo?.Trim();
        t.OrdenEntidad = ficha.OrdenEntidad;
        t.NaturalezaJuridica = ficha.NaturalezaJuridica?.Trim();
        t.RepresentanteLegalId = ficha.RepresentanteLegalId;
        t.Observaciones = ficha.Observaciones?.Trim();
        t.PerfilCompleto = true; // guardado desde la ficha completa

        await _db.SaveChangesAsync(ct);
        return TerceroResult.Success(t.Id);
    }

    public async Task<TerceroResult> InactivarAsync(long id, string motivo, CancellationToken ct = default)
        => await CambiarEstadoAsync(id, TerceroEstado.Inactivo, motivo, ct);

    public async Task<TerceroResult> ReactivarAsync(long id, string motivo, CancellationToken ct = default)
        => await CambiarEstadoAsync(id, TerceroEstado.Activo, motivo, ct);

    private async Task<TerceroResult> CambiarEstadoAsync(long id, TerceroEstado estado, string motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo)) { return TerceroResult.Fail("El motivo es obligatorio."); }
        var t = await _db.Terceros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) { return TerceroResult.Fail("Tercero no encontrado."); }
        t.Estado = estado;
        t.MotivoInactivacion = motivo.Trim();
        await _db.SaveChangesAsync(ct);
        return TerceroResult.Success(t.Id);
    }

    public async Task<TerceroSugerenciaDto?> BuscarPorDocumentoAsync(string tipoDocumento, string numeroDocumento, CancellationToken ct = default)
    {
        var tipo = (tipoDocumento ?? "").Trim();
        var num = (numeroDocumento ?? "").Trim();
        if (num.Length < 3) { return null; }
        return await _db.Terceros.AsNoTracking()
            .Where(t => t.NumeroDocumento == num && (tipo == "" || t.TipoDocumento == tipo) && t.Estado == TerceroEstado.Activo)
            .OrderByDescending(t => t.Id)
            .Select(t => new TerceroSugerenciaDto(t.Id, t.Subtipo, t.TipoDocumento, t.NumeroDocumento,
                t.Nombre, t.Apellidos, t.RazonSocial, t.Email, t.Telefono, t.MunicipioId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TerceroResult> CrearRapidoAsync(CrearRapidoRequest req, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) { return TerceroResult.Fail("Sesion no valida."); }
        var tipoDoc = (req.TipoDocumento ?? "").Trim();
        var numDoc = (req.NumeroDocumento ?? "").Trim();
        if (string.IsNullOrEmpty(tipoDoc) || string.IsNullOrEmpty(numDoc))
        {
            return TerceroResult.Fail("Tipo y numero de documento son obligatorios.");
        }

        var esJuridica = EsJuridica(req.Subtipo);
        // Upsert por (tenant, tipo, numero): si existe se enriquecen los vacios, si no se crea Incompleto.
        var t = await _db.Terceros.FirstOrDefaultAsync(
            x => x.TipoDocumento == tipoDoc && x.NumeroDocumento == numDoc, ct);
        if (t is null)
        {
            t = new Tercero
            {
                TenantId = tenantId.Value,
                Subtipo = req.Subtipo,
                TipoDocumento = tipoDoc,
                NumeroDocumento = numDoc,
                RazonSocial = esJuridica ? req.RazonSocial?.Trim() : null,
                Nombre = esJuridica ? null : req.Nombre?.Trim(),
                Apellidos = esJuridica ? null : req.Apellidos?.Trim(),
                Email = req.Email?.Trim(),
                Telefono = req.Telefono?.Trim(),
                MunicipioId = req.MunicipioId,
                Estado = TerceroEstado.Activo,
                PerfilCompleto = false,
                Origen = string.IsNullOrWhiteSpace(req.Origen) ? "Radicacion" : req.Origen
            };
            _db.Terceros.Add(t);
        }
        else
        {
            // Enriquece solo lo que este vacio (no pisa lo que ya tiene el directorio).
            if (esJuridica && string.IsNullOrWhiteSpace(t.RazonSocial)) { t.RazonSocial = req.RazonSocial?.Trim(); }
            if (!esJuridica && string.IsNullOrWhiteSpace(t.Nombre)) { t.Nombre = req.Nombre?.Trim(); }
            if (!esJuridica && string.IsNullOrWhiteSpace(t.Apellidos)) { t.Apellidos = req.Apellidos?.Trim(); }
            if (string.IsNullOrWhiteSpace(t.Email)) { t.Email = req.Email?.Trim(); }
            if (string.IsNullOrWhiteSpace(t.Telefono)) { t.Telefono = req.Telefono?.Trim(); }
            if (t.MunicipioId is null) { t.MunicipioId = req.MunicipioId; }
        }

        await _db.SaveChangesAsync(ct);
        return TerceroResult.Success(t.Id);
    }
}
