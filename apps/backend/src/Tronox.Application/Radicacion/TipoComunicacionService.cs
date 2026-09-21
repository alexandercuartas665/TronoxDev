using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Catalogo de tipos de comunicacion radicables (RQ09 RF01-2). Los 13 tipos base normativos se
/// siembran por tenant (ver RadicacionConfigService): no se eliminan, solo se editan o inactivan.
/// Tenant-scoped: el filtro global de EF acota por tenant.
/// </summary>
public sealed class TipoComunicacionService : ITipoComunicacionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public TipoComunicacionService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<TipoComunicacionDto>> ListAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _db.TiposComunicacion.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(t => t.Activo);
        }

        var tipos = await query
            .OrderBy(t => t.Direccion)
            .ThenBy(t => t.Nombre)
            .ToListAsync(cancellationToken);

        var nivelIds = tipos.Where(t => t.NivelReservaDefaultId.HasValue)
            .Select(t => t.NivelReservaDefaultId!.Value)
            .Distinct()
            .ToList();

        var niveles = nivelIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.NivelesClasificacion.AsNoTracking()
                .Where(n => nivelIds.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id, n => n.Nombre, cancellationToken);

        return tipos.Select(t => Map(t, LookupNivel(t.NivelReservaDefaultId, niveles))).ToList();
    }

    public async Task<RadicacionResult<TipoComunicacionDto>> CreateAsync(SaveTipoComunicacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null)
        {
            return RadicacionResult<TipoComunicacionDto>.Invalid("No hay tenant activo.");
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return RadicacionResult<TipoComunicacionDto>.Invalid(validation);
        }

        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var exists = await _db.TiposComunicacion.AnyAsync(t => t.Codigo == codigo, cancellationToken);
        if (exists)
        {
            return RadicacionResult<TipoComunicacionDto>.Conflict($"Ya existe un tipo con el codigo '{codigo}'.");
        }

        var tipo = new TipoComunicacion
        {
            TenantId = _tenantContext.TenantId.Value,
            Codigo = codigo,
            EsBase = false
        };
        ApplyRequest(tipo, request);

        _db.TiposComunicacion.Add(tipo);
        _audit.Write(actorUserId, "radicacion.tipo.create", nameof(TipoComunicacion), tipo,
            previousValue: null, newValue: new { tipo.Codigo, tipo.Nombre, tipo.Direccion });

        await _db.SaveChangesAsync(cancellationToken);

        var nivelNombre = await ResolveNivelNombreAsync(tipo.NivelReservaDefaultId, cancellationToken);
        return RadicacionResult<TipoComunicacionDto>.Ok(Map(tipo, nivelNombre));
    }

    public async Task<RadicacionResult<TipoComunicacionDto>> UpdateAsync(long id, SaveTipoComunicacionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tipo = await _db.TiposComunicacion.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tipo is null)
        {
            return RadicacionResult<TipoComunicacionDto>.NotFound("El tipo de comunicacion no existe.");
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return RadicacionResult<TipoComunicacionDto>.Invalid(validation);
        }

        // El codigo de un tipo base no cambia; para los demas se valida unicidad si se altera.
        if (!tipo.EsBase)
        {
            var codigo = request.Codigo.Trim().ToUpperInvariant();
            if (!string.Equals(codigo, tipo.Codigo, StringComparison.Ordinal))
            {
                var exists = await _db.TiposComunicacion.AnyAsync(t => t.Id != id && t.Codigo == codigo, cancellationToken);
                if (exists)
                {
                    return RadicacionResult<TipoComunicacionDto>.Conflict($"Ya existe un tipo con el codigo '{codigo}'.");
                }
                tipo.Codigo = codigo;
            }
        }

        ApplyRequest(tipo, request);
        // EsBase se conserva sin cambios.

        _audit.Write(actorUserId, "radicacion.tipo.update", nameof(TipoComunicacion), tipo,
            previousValue: null, newValue: new { tipo.Codigo, tipo.Nombre, tipo.Activo });

        await _db.SaveChangesAsync(cancellationToken);

        var nivelNombre = await ResolveNivelNombreAsync(tipo.NivelReservaDefaultId, cancellationToken);
        return RadicacionResult<TipoComunicacionDto>.Ok(Map(tipo, nivelNombre));
    }

    public async Task<RadicacionResult<bool>> SetActivoAsync(long id, bool activo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tipo = await _db.TiposComunicacion.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tipo is null)
        {
            return RadicacionResult<bool>.NotFound("El tipo de comunicacion no existe.");
        }

        // Los base pueden inactivarse (no eliminarse).
        tipo.Activo = activo;
        _audit.Write(actorUserId, activo ? "radicacion.tipo.activar" : "radicacion.tipo.inactivar",
            nameof(TipoComunicacion), tipo, previousValue: null, newValue: new { tipo.Codigo, tipo.Activo });

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<bool>.Ok(true);
    }

    public async Task<RadicacionResult<bool>> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var tipo = await _db.TiposComunicacion.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tipo is null)
        {
            return RadicacionResult<bool>.NotFound("El tipo de comunicacion no existe.");
        }

        if (tipo.EsBase)
        {
            return RadicacionResult<bool>.Invalid("Los tipos base no se eliminan, solo se inactivan.");
        }

        _db.TiposComunicacion.Remove(tipo);
        _audit.Write(actorUserId, "radicacion.tipo.delete", nameof(TipoComunicacion), tipo,
            previousValue: new { tipo.Codigo, tipo.Nombre }, newValue: null);

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<bool>.Ok(true);
    }

    // ---- Validacion y mapeo ----

    private static string? ValidateRequest(SaveTipoComunicacionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return "El codigo es obligatorio.";
        }
        if (request.Codigo.Trim().Contains(' '))
        {
            return "El codigo no puede contener espacios.";
        }
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return "El nombre es obligatorio.";
        }
        if (request.RequiereRespuesta && (request.DiasRespuesta is null || request.DiasRespuesta <= 0))
        {
            return "Los dias de respuesta son obligatorios cuando el tipo requiere respuesta.";
        }
        return null;
    }

    private static void ApplyRequest(TipoComunicacion tipo, SaveTipoComunicacionRequest request)
    {
        tipo.Nombre = request.Nombre.Trim();
        tipo.Direccion = request.Direccion;
        tipo.EsPqrsd = request.EsPqrsd;
        tipo.EsTutela = request.EsTutela;
        tipo.EsRecurso = request.EsRecurso;
        tipo.RequiereRespuesta = request.RequiereRespuesta;
        tipo.DiasRespuesta = request.RequiereRespuesta ? request.DiasRespuesta : null;
        tipo.TipoDia = request.TipoDia;
        tipo.InicioTermino = request.InicioTermino;
        // Una tutela nunca es prorrogable.
        tipo.Prorrogable = !request.EsTutela && request.Prorrogable;
        tipo.DiasProrroga = tipo.Prorrogable ? request.DiasProrroga : null;
        tipo.PermiteAnonimo = request.PermiteAnonimo;
        tipo.HabilitadoWeb = request.HabilitadoWeb;
        tipo.NivelReservaDefaultId = request.NivelReservaDefaultId;
        tipo.Icono = request.Icono?.Trim();
        tipo.Color = request.Color?.Trim();
        tipo.PalabrasClave = request.PalabrasClave?.Trim();
        tipo.OrdenPortal = request.OrdenPortal;
        tipo.DescripcionCiudadano = request.DescripcionCiudadano;
        tipo.Activo = request.Activo;
    }

    private async Task<string?> ResolveNivelNombreAsync(long? nivelId, CancellationToken cancellationToken)
    {
        if (nivelId is null)
        {
            return null;
        }
        var nivel = await _db.NivelesClasificacion.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nivelId.Value, cancellationToken);
        return nivel?.Nombre;
    }

    private static string? LookupNivel(long? nivelId, IReadOnlyDictionary<long, string> niveles) =>
        nivelId.HasValue && niveles.TryGetValue(nivelId.Value, out var nombre) ? nombre : null;

    private static TipoComunicacionDto Map(TipoComunicacion t, string? nivelNombre) => new(
        t.Id, t.Codigo, t.Nombre, t.Direccion,
        t.EsPqrsd, t.EsTutela, t.EsRecurso, t.RequiereRespuesta,
        t.DiasRespuesta, t.TipoDia, t.InicioTermino,
        t.Prorrogable, t.DiasProrroga, t.PermiteAnonimo, t.HabilitadoWeb,
        t.NivelReservaDefaultId, nivelNombre,
        t.Icono, t.Color, t.PalabrasClave, t.OrdenPortal,
        t.DescripcionCiudadano, t.Activo, t.EsBase);
}
