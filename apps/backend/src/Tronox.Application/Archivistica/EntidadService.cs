using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Datos de la Entidad (RQ01 - RF01 seccion 4.1.1).
///
/// Cuatro reglas que este servicio hace cumplir y la UI no puede saltarse:
/// 1. UNA sola entidad por tenant: Save crea la primera y despues siempre actualiza esa misma.
/// 2. El digito de verificacion del NIT se comprueba con el algoritmo de la DIAN (EntidadRules).
/// 3. El codigo de fondo AGN se GENERA (CO-{DIVIPOLA}-{SIGLA}); solo se acepta uno manual si el
///    llamador lo envia explicitamente (caso excepcional del Super Admin, resolucion M01).
/// 4. No hay eliminacion: la unica baja es cambiar el estado, con motivo y auditoria.
///
/// Toda modificacion queda en la pista de auditoria con valor anterior y nuevo (criterio 7).
/// </summary>
public sealed class EntidadService : IEntidadService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;

    public EntidadService(IApplicationDbContext db, ITenantContext tenant, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<EntidadDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        // El filtro global ya acota a este tenant: no hace falta (ni debe) filtrar a mano.
        var entidad = await _db.Entidades.AsNoTracking()
            .Include(e => e.Pais)
            .Include(e => e.Departamento)
            .Include(e => e.Ciudad)
            .FirstOrDefaultAsync(cancellationToken);
        return entidad is null ? null : Map(entidad);
    }

    public async Task<ArchivisticaResult<EntidadDto>> SaveAsync(
        SaveEntidadRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var nit = EntidadRules.NormalizarNit(request.Nit);
        var sigla = (request.Sigla ?? string.Empty).Trim().ToUpperInvariant();
        var divipola = (request.CodigoDivipola ?? string.Empty).Trim();

        // El codigo AGN NO se captura: se deriva (resolucion M01). El manual solo entra si el
        // llamador lo envio a proposito, y aun asi pasa por la misma validacion de longitud.
        var codigoAgn = string.IsNullOrWhiteSpace(request.CodigoFondoAgnManual)
            ? EntidadRules.GenerarCodigoFondoAgn(divipola, sigla)
            : request.CodigoFondoAgnManual.Trim().ToUpperInvariant();

        var error = EntidadRules.Validate(
            nit, request.DigitoVerificacion, request.RazonSocial, sigla, request.TipoEntidad,
            request.NaturalezaJuridica, divipola,
            request.PaisId, request.DepartamentoId, request.CiudadId,
            request.DireccionPrincipal, request.Telefono, request.CorreoInstitucional,
            request.PaginaWeb, request.RepresentanteLegal, codigoAgn,
            request.ZonaHoraria, request.IdiomaDefecto);
        if (error is not null)
        {
            return ArchivisticaResult<EntidadDto>.Invalid(error);
        }

        // Coherencia de los selectores encadenados: no basta con que existan, el departamento
        // debe pertenecer al pais y el municipio al departamento (criterio 5 de RF01). Sin esta
        // comprobacion un cliente que no use la UI puede guardar Bogota dentro de Antioquia.
        var ubicacion = await ValidarUbicacionAsync(
            request.PaisId!.Value, request.DepartamentoId!.Value, request.CiudadId!.Value, cancellationToken);
        if (ubicacion is not null)
        {
            return ArchivisticaResult<EntidadDto>.Invalid(ubicacion);
        }

        var entidad = await _db.Entidades.FirstOrDefaultAsync(cancellationToken);

        if (entidad is null)
        {
            if (_tenant.TenantId is not long tenantId)
            {
                return ArchivisticaResult<EntidadDto>.Invalid("No hay tenant activo.");
            }

            entidad = new Entidad { TenantId = tenantId };
            Apply(entidad, request, nit, sigla, divipola, codigoAgn);
            _db.Entidades.Add(entidad);
            _audit.Write(actorUserId, "entidad.create", nameof(Entidad), entidad,
                previousValue: null,
                newValue: Snapshot(entidad),
                tenantId: entidad.TenantId);
        }
        else
        {
            var previo = Snapshot(entidad);
            Apply(entidad, request, nit, sigla, divipola, codigoAgn);
            _audit.Write(actorUserId, "entidad.update", nameof(Entidad), entidad,
                previousValue: previo,
                newValue: Snapshot(entidad),
                tenantId: entidad.TenantId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Se relee para devolver los nombres de pais/departamento/ciudad ya resueltos.
        return ArchivisticaResult<EntidadDto>.Ok((await GetAsync(cancellationToken))!);
    }

    public async Task<ArchivisticaResult<EntidadDto>> CambiarEstadoAsync(
        EntidadEstado estado, string? motivo, long actorUserId, CancellationToken cancellationToken = default)
    {
        var entidad = await _db.Entidades.FirstOrDefaultAsync(cancellationToken);
        if (entidad is null)
        {
            return ArchivisticaResult<EntidadDto>.NotFound("El tenant todavia no tiene registrada su entidad.");
        }
        if (entidad.Estado == estado)
        {
            return ArchivisticaResult<EntidadDto>.Ok((await GetAsync(cancellationToken))!);
        }

        var anterior = entidad.Estado;
        entidad.Estado = estado;
        _audit.Write(actorUserId, "entidad.estado", nameof(Entidad), entidad,
            previousValue: new { Estado = anterior },
            newValue: new { entidad.Estado },
            tenantId: entidad.TenantId,
            reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return ArchivisticaResult<EntidadDto>.Ok((await GetAsync(cancellationToken))!);
    }

    // ---- Internos ----

    private async Task<string?> ValidarUbicacionAsync(
        long paisId, long departamentoId, long ciudadId, CancellationToken cancellationToken)
    {
        var departamento = await _db.Departamentos.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == departamentoId, cancellationToken);
        if (departamento is null) { return "El departamento seleccionado no existe."; }
        if (departamento.PaisId != paisId)
        {
            return "El departamento seleccionado no pertenece al pais indicado.";
        }

        var municipio = await _db.Municipios.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == ciudadId, cancellationToken);
        if (municipio is null) { return "La ciudad o municipio seleccionado no existe."; }
        if (municipio.DepartamentoId != departamentoId)
        {
            return "La ciudad seleccionada no pertenece al departamento indicado.";
        }
        return null;
    }

    private static void Apply(
        Entidad entidad, SaveEntidadRequest request,
        string nit, string sigla, string divipola, string? codigoAgn)
    {
        entidad.Nit = nit;
        entidad.DigitoVerificacion = EntidadRules.CalcularDigitoVerificacion(nit)!.Value.ToString();
        entidad.RazonSocial = request.RazonSocial.Trim();
        entidad.Sigla = sigla;
        entidad.TipoEntidad = request.TipoEntidad;
        entidad.NaturalezaJuridica = Nullable(request.NaturalezaJuridica);
        entidad.CodigoDivipola = divipola.Length == 0 ? null : divipola;
        entidad.PaisId = request.PaisId;
        entidad.DepartamentoId = request.DepartamentoId;
        entidad.CiudadId = request.CiudadId;
        entidad.DireccionPrincipal = request.DireccionPrincipal.Trim();
        entidad.Telefono = Nullable(request.Telefono);
        entidad.CorreoInstitucional = request.CorreoInstitucional.Trim();
        entidad.PaginaWeb = Nullable(request.PaginaWeb);
        entidad.RepresentanteLegal = request.RepresentanteLegal.Trim();
        entidad.LogoUrl = Nullable(request.LogoUrl);
        entidad.CodigoFondoAgn = string.IsNullOrWhiteSpace(codigoAgn) ? null : codigoAgn;
        entidad.ZonaHoraria = request.ZonaHoraria.Trim();
        entidad.IdiomaDefecto = request.IdiomaDefecto.Trim();
        entidad.Estado = request.Estado;
    }

    private static string? Nullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Foto de los campos que la pista de auditoria compara (criterio 7 de RF01).</summary>
    private static object Snapshot(Entidad e) => new
    {
        e.Nit,
        e.DigitoVerificacion,
        e.RazonSocial,
        e.Sigla,
        TipoEntidad = e.TipoEntidad.ToString(),
        e.NaturalezaJuridica,
        e.CodigoDivipola,
        e.PaisId,
        e.DepartamentoId,
        e.CiudadId,
        e.DireccionPrincipal,
        e.Telefono,
        e.CorreoInstitucional,
        e.PaginaWeb,
        e.RepresentanteLegal,
        e.LogoUrl,
        e.CodigoFondoAgn,
        e.ZonaHoraria,
        e.IdiomaDefecto,
        Estado = e.Estado.ToString()
    };

    private static EntidadDto Map(Entidad e) => new(
        e.Id, e.Nit, e.DigitoVerificacion, e.RazonSocial, e.Sigla, e.TipoEntidad,
        e.NaturalezaJuridica, e.CodigoDivipola,
        e.PaisId, e.Pais?.Nombre,
        e.DepartamentoId, e.Departamento?.Nombre,
        e.CiudadId, e.Ciudad?.Nombre,
        e.DireccionPrincipal, e.Telefono, e.CorreoInstitucional, e.PaginaWeb,
        e.RepresentanteLegal, e.LogoUrl, e.CodigoFondoAgn,
        e.ZonaHoraria, e.IdiomaDefecto, e.Estado);
}
