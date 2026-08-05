using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Buzones de correo de recepcion (RQ09 RF01-4). La contrasena se cifra con ISecretProtector (AES-256)
/// y nunca sale en claro en los DTOs (solo TieneClave). El worker de captura de correos es integracion
/// posterior. Tenant-scoped: el filtro global de EF acota por tenant.
/// </summary>
public sealed class BuzonCorreoService : IBuzonCorreoService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditWriter _audit;

    public BuzonCorreoService(IApplicationDbContext db, ITenantContext tenantContext, ISecretProtector secretProtector, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _secretProtector = secretProtector;
        _audit = audit;
    }

    public async Task<IReadOnlyList<BuzonCorreoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var buzones = await _db.BuzonesCorreo.AsNoTracking()
            .OrderBy(b => b.NombreBuzon)
            .ToListAsync(cancellationToken);

        var tipoIds = buzones.Where(b => b.TipoComunicacionDefaultId.HasValue)
            .Select(b => b.TipoComunicacionDefaultId!.Value).Distinct().ToList();
        var depIds = buzones.Where(b => b.DependenciaDefaultId.HasValue)
            .Select(b => b.DependenciaDefaultId!.Value).Distinct().ToList();

        var tipos = tipoIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.TiposComunicacion.AsNoTracking()
                .Where(t => tipoIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nombre, cancellationToken);

        var deps = depIds.Count == 0
            ? new Dictionary<long, string>()
            : await _db.OrgUnits.AsNoTracking()
                .Where(o => depIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        return buzones.Select(b => Map(b,
            Lookup(b.TipoComunicacionDefaultId, tipos),
            Lookup(b.DependenciaDefaultId, deps))).ToList();
    }

    public async Task<RadicacionResult<BuzonCorreoDto>> CreateAsync(SaveBuzonCorreoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is null)
        {
            return RadicacionResult<BuzonCorreoDto>.Invalid("No hay tenant activo.");
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return RadicacionResult<BuzonCorreoDto>.Invalid(validation);
        }

        var buzon = new BuzonCorreo { TenantId = _tenantContext.TenantId.Value };
        ApplyRequest(buzon, request);
        // En alta, si no llega contrasena queda sin clave.
        if (!string.IsNullOrWhiteSpace(request.Contrasena))
        {
            buzon.ContrasenaEncrypted = _secretProtector.Protect(request.Contrasena.Trim());
        }

        _db.BuzonesCorreo.Add(buzon);
        _audit.Write(actorUserId, "radicacion.buzon.create", nameof(BuzonCorreo), buzon,
            previousValue: null, newValue: new { buzon.NombreBuzon, buzon.DireccionEmail, buzon.Protocolo });

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<BuzonCorreoDto>.Ok(await MapWithNamesAsync(buzon, cancellationToken));
    }

    public async Task<RadicacionResult<BuzonCorreoDto>> UpdateAsync(long id, SaveBuzonCorreoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var buzon = await _db.BuzonesCorreo.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (buzon is null)
        {
            return RadicacionResult<BuzonCorreoDto>.NotFound("El buzon no existe.");
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return RadicacionResult<BuzonCorreoDto>.Invalid(validation);
        }

        ApplyRequest(buzon, request);
        // Contrasena vacia = conservar la cifrada existente; nueva = re-cifrar.
        if (!string.IsNullOrWhiteSpace(request.Contrasena))
        {
            buzon.ContrasenaEncrypted = _secretProtector.Protect(request.Contrasena.Trim());
        }

        _audit.Write(actorUserId, "radicacion.buzon.update", nameof(BuzonCorreo), buzon,
            previousValue: null, newValue: new { buzon.NombreBuzon, buzon.DireccionEmail, buzon.Activo });

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<BuzonCorreoDto>.Ok(await MapWithNamesAsync(buzon, cancellationToken));
    }

    public async Task<RadicacionResult<bool>> SetActivoAsync(long id, bool activo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var buzon = await _db.BuzonesCorreo.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (buzon is null)
        {
            return RadicacionResult<bool>.NotFound("El buzon no existe.");
        }

        buzon.Activo = activo;
        _audit.Write(actorUserId, activo ? "radicacion.buzon.activar" : "radicacion.buzon.inactivar",
            nameof(BuzonCorreo), buzon, previousValue: null, newValue: new { buzon.NombreBuzon, buzon.Activo });

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<bool>.Ok(true);
    }

    public async Task<RadicacionResult<bool>> DeleteAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var buzon = await _db.BuzonesCorreo.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (buzon is null)
        {
            return RadicacionResult<bool>.NotFound("El buzon no existe.");
        }

        _db.BuzonesCorreo.Remove(buzon);
        _audit.Write(actorUserId, "radicacion.buzon.delete", nameof(BuzonCorreo), buzon,
            previousValue: new { buzon.NombreBuzon, buzon.DireccionEmail }, newValue: null);

        await _db.SaveChangesAsync(cancellationToken);
        return RadicacionResult<bool>.Ok(true);
    }

    // ---- Validacion y mapeo ----

    private static string? ValidateRequest(SaveBuzonCorreoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NombreBuzon))
        {
            return "El nombre del buzon es obligatorio.";
        }
        if (string.IsNullOrWhiteSpace(request.DireccionEmail))
        {
            return "La direccion de correo es obligatoria.";
        }
        if (string.IsNullOrWhiteSpace(request.Usuario))
        {
            return "El usuario es obligatorio.";
        }
        if (request.Protocolo == BuzonProtocolo.Imap)
        {
            if (string.IsNullOrWhiteSpace(request.Servidor) || request.Puerto is null || request.Puerto <= 0)
            {
                return "El protocolo IMAP requiere servidor y puerto.";
            }
        }
        return null;
    }

    private static void ApplyRequest(BuzonCorreo buzon, SaveBuzonCorreoRequest request)
    {
        buzon.NombreBuzon = request.NombreBuzon.Trim();
        buzon.DireccionEmail = request.DireccionEmail.Trim();
        buzon.Protocolo = request.Protocolo;
        buzon.Servidor = request.Servidor?.Trim();
        buzon.Puerto = request.Puerto;
        buzon.Seguridad = request.Seguridad;
        buzon.Usuario = request.Usuario.Trim();
        buzon.Carpeta = string.IsNullOrWhiteSpace(request.Carpeta) ? "INBOX" : request.Carpeta.Trim();
        buzon.FrecuenciaRevision = request.FrecuenciaRevision;
        buzon.ModoRadicacion = request.ModoRadicacion;
        buzon.TiempoEsperaMinutos = request.TiempoEsperaMinutos;
        buzon.TipoComunicacionDefaultId = request.TipoComunicacionDefaultId;
        buzon.DependenciaDefaultId = request.DependenciaDefaultId;
        buzon.Activo = request.Activo;
    }

    private async Task<BuzonCorreoDto> MapWithNamesAsync(BuzonCorreo buzon, CancellationToken cancellationToken)
    {
        string? tipoNombre = null;
        if (buzon.TipoComunicacionDefaultId is not null)
        {
            tipoNombre = (await _db.TiposComunicacion.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == buzon.TipoComunicacionDefaultId.Value, cancellationToken))?.Nombre;
        }

        string? depNombre = null;
        if (buzon.DependenciaDefaultId is not null)
        {
            depNombre = (await _db.OrgUnits.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == buzon.DependenciaDefaultId.Value, cancellationToken))?.Name;
        }

        return Map(buzon, tipoNombre, depNombre);
    }

    private static string? Lookup(long? id, IReadOnlyDictionary<long, string> names) =>
        id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

    private static BuzonCorreoDto Map(BuzonCorreo b, string? tipoNombre, string? depNombre) => new(
        b.Id, b.NombreBuzon, b.DireccionEmail, b.Protocolo,
        b.Servidor, b.Puerto, b.Seguridad, b.Usuario,
        !string.IsNullOrEmpty(b.ContrasenaEncrypted),
        b.Carpeta, b.FrecuenciaRevision, b.ModoRadicacion,
        b.TiempoEsperaMinutos, b.TipoComunicacionDefaultId, tipoNombre,
        b.DependenciaDefaultId, depNombre, b.Activo);
}
