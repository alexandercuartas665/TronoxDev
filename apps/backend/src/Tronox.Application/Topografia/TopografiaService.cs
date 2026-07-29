using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Topografia;

/// <summary>
/// Topografia fisica del archivo (RQ02 - RF06). El aislamiento por tenant lo garantiza el filtro
/// global; el codigo topografico y la ocupacion son PUROS (TopografiaRules) y se calculan en runtime.
///
/// Reglas (RF06 3.6.6):
/// 1. La config de niveles solo se toca si NO existen elementos.
/// 2. Solo un nivel controla capacidad y es el de mayor orden.
/// 3. El codigo topografico se genera automaticamente (siglas raiz->hoja), no se almacena.
/// 4. Un elemento hijo pertenece a un nivel de orden mayor que su contenedor.
/// 5. Un elemento que controla capacidad pasa a Lleno cuando la alcanza (ocupacion = hijos directos).
///    (Con RQ03, la ocupacion de la hoja contara expedientes; hoy cuenta elementos hijos).
/// 6. Nunca hay borrado fisico: se inactiva. No se borra un elemento con hijos.
/// 7. Auditoria de toda alta/edicion/cambio de estado.
/// </summary>
public sealed class TopografiaService : ITopografiaService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public TopografiaService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Niveles ----

    public async Task<IReadOnlyList<TopografiaNivelDto>> ListNivelesAsync(CancellationToken cancellationToken = default)
        => await _db.TopografiaNiveles.AsNoTracking()
            .OrderBy(n => n.Orden)
            .Select(n => new TopografiaNivelDto(n.Id, n.NombreNivel, n.SiglaBase, n.Orden, n.ControlaCapacidad))
            .ToListAsync(cancellationToken);

    public async Task<bool> HayElementosAsync(CancellationToken cancellationToken = default)
        => await _db.TopografiaElementos.AnyAsync(cancellationToken);

    public async Task<TopografiaResult<TopografiaNivelDto>> CreateNivelAsync(
        SaveNivelRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return TopografiaResult<TopografiaNivelDto>.Invalid("No hay tenant activo.");
        }
        if (await HayElementosAsync(cancellationToken))
        {
            return TopografiaResult<TopografiaNivelDto>.Invalid(
                "La configuracion de niveles esta bloqueada: ya existen elementos fisicos creados.");
        }
        var validation = await ValidateNivelAsync(request, nivelId: null, cancellationToken);
        if (validation is not null) { return validation.To<TopografiaNivelDto>(); }

        var nivel = new TopografiaNivel
        {
            TenantId = tenantId,
            NombreNivel = request.NombreNivel.Trim(),
            SiglaBase = request.SiglaBase.Trim().ToUpperInvariant(),
            Orden = request.Orden,
            ControlaCapacidad = request.ControlaCapacidad
        };
        _db.TopografiaNiveles.Add(nivel);
        _audit.Write(actorUserId, "topografia.nivel.create", nameof(TopografiaNivel), nivel,
            previousValue: null, newValue: SnapshotNivel(nivel), tenantId: nivel.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TopografiaResult<TopografiaNivelDto>.Ok(ToNivelDto(nivel));
    }

    public async Task<TopografiaResult<TopografiaNivelDto>> UpdateNivelAsync(
        long nivelId, SaveNivelRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var nivel = await _db.TopografiaNiveles.FirstOrDefaultAsync(n => n.Id == nivelId, cancellationToken);
        if (nivel is null) { return TopografiaResult<TopografiaNivelDto>.NotFound("El nivel no existe."); }
        if (await HayElementosAsync(cancellationToken))
        {
            return TopografiaResult<TopografiaNivelDto>.Invalid(
                "La configuracion de niveles esta bloqueada: ya existen elementos fisicos creados.");
        }
        var validation = await ValidateNivelAsync(request, nivelId, cancellationToken);
        if (validation is not null) { return validation.To<TopografiaNivelDto>(); }

        var prev = SnapshotNivel(nivel);
        nivel.NombreNivel = request.NombreNivel.Trim();
        nivel.SiglaBase = request.SiglaBase.Trim().ToUpperInvariant();
        nivel.Orden = request.Orden;
        nivel.ControlaCapacidad = request.ControlaCapacidad;
        _audit.Write(actorUserId, "topografia.nivel.update", nameof(TopografiaNivel), nivel,
            previousValue: prev, newValue: SnapshotNivel(nivel), tenantId: nivel.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TopografiaResult<TopografiaNivelDto>.Ok(ToNivelDto(nivel));
    }

    public async Task<TopografiaResult<bool>> DeleteNivelAsync(
        long nivelId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var nivel = await _db.TopografiaNiveles.FirstOrDefaultAsync(n => n.Id == nivelId, cancellationToken);
        if (nivel is null) { return TopografiaResult<bool>.NotFound("El nivel no existe."); }
        if (await HayElementosAsync(cancellationToken))
        {
            return TopografiaResult<bool>.Invalid(
                "No se puede eliminar un nivel: ya existen elementos fisicos. La estructura esta bloqueada.");
        }
        _db.TopografiaNiveles.Remove(nivel);
        _audit.Write(actorUserId, "topografia.nivel.delete", nameof(TopografiaNivel), nivel,
            previousValue: SnapshotNivel(nivel), newValue: null, tenantId: nivel.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TopografiaResult<bool>.Ok(true);
    }

    private async Task<TopografiaResult<bool>?> ValidateNivelAsync(
        SaveNivelRequest request, long? nivelId, CancellationToken cancellationToken)
    {
        var error = TopografiaRules.ValidateNivel(request.NombreNivel, request.SiglaBase, request.Orden);
        if (error is not null) { return TopografiaResult<bool>.Invalid(error); }

        var nombreUpper = request.NombreNivel.Trim().ToUpperInvariant();
        var siglaUpper = request.SiglaBase.Trim().ToUpperInvariant();
        var otros = await _db.TopografiaNiveles.AsNoTracking()
            .Where(n => nivelId == null || n.Id != nivelId)
            .Select(n => new { n.NombreNivel, n.SiglaBase, n.Orden, n.ControlaCapacidad })
            .ToListAsync(cancellationToken);

        if (otros.Any(n => n.NombreNivel.ToUpperInvariant() == nombreUpper))
        {
            return TopografiaResult<bool>.Conflict($"Ya existe un nivel con el nombre '{request.NombreNivel.Trim()}'.");
        }
        if (otros.Any(n => n.SiglaBase.ToUpperInvariant() == siglaUpper))
        {
            return TopografiaResult<bool>.Conflict($"Ya existe un nivel con la sigla '{siglaUpper}'.");
        }
        if (otros.Any(n => n.Orden == request.Orden))
        {
            return TopografiaResult<bool>.Conflict($"Ya existe un nivel con el orden {request.Orden}.");
        }
        if (request.ControlaCapacidad)
        {
            var capError = TopografiaRules.ValidateControlaCapacidad(
                request.Orden, otros.Select(n => (n.Orden, n.ControlaCapacidad)).ToList());
            if (capError is not null) { return TopografiaResult<bool>.Invalid(capError); }
        }
        return null;
    }

    // ---- Elementos ----

    public async Task<IReadOnlyList<TopografiaElementoNodeDto>> GetArbolAsync(
        bool includeInactivos = true, CancellationToken cancellationToken = default)
    {
        var niveles = await _db.TopografiaNiveles.AsNoTracking()
            .ToDictionaryAsync(n => n.Id, n => n, cancellationToken);
        var elementos = await _db.TopografiaElementos.AsNoTracking()
            .Where(e => includeInactivos || e.Estado != TopografiaEstado.Inactivo)
            .OrderBy(e => e.Sigla).ThenBy(e => e.Nombre)
            .ToListAsync(cancellationToken);

        var arbolSiglas = elementos.ToDictionary(e => e.Id, e => ((long?)e.ParentId, e.Sigla));
        var hijosPorPadre = elementos.GroupBy(e => e.ParentId ?? 0)
            .ToDictionary(g => g.Key, g => g.ToList());
        int Ocupacion(long id) => hijosPorPadre.TryGetValue(id, out var h) ? h.Count : 0;

        List<TopografiaElementoNodeDto> Build(long? parentId)
        {
            if (!hijosPorPadre.TryGetValue(parentId ?? 0, out var hijos)) { return []; }
            return hijos.Select(e => ToNode(e, niveles, arbolSiglas, Ocupacion(e.Id), Build(e.Id))).ToList();
        }

        var visibles = elementos.Select(e => e.Id).ToHashSet();
        return elementos
            .Where(e => e.ParentId is null || !visibles.Contains(e.ParentId.Value))
            .Select(e => ToNode(e, niveles, arbolSiglas, Ocupacion(e.Id), Build(e.Id)))
            .ToList();
    }

    public async Task<TopografiaKpisDto> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        var nivelesCount = await _db.TopografiaNiveles.CountAsync(cancellationToken);
        var elementos = await _db.TopografiaElementos.AsNoTracking()
            .Select(e => new { e.Id, e.ParentId, e.Capacidad, e.Estado })
            .ToListAsync(cancellationToken);
        var hijos = elementos.GroupBy(e => e.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Count());
        int Oc(long id) => hijos.TryGetValue(id, out var n) ? n : 0;

        var llenos = elementos.Count(e => e.Estado != TopografiaEstado.Inactivo
            && TopografiaRules.EstaLleno(Oc(e.Id), e.Capacidad));
        var inactivos = elementos.Count(e => e.Estado == TopografiaEstado.Inactivo);
        return new TopografiaKpisDto(nivelesCount, elementos.Count, llenos, inactivos);
    }

    public async Task<TopografiaResult<TopografiaElementoNodeDto>> CreateElementoAsync(
        SaveElementoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return TopografiaResult<TopografiaElementoNodeDto>.Invalid("No hay tenant activo.");
        }
        var error = TopografiaRules.ValidateElemento(request.Nombre, request.Sigla);
        if (error is not null) { return TopografiaResult<TopografiaElementoNodeDto>.Invalid(error); }

        var nivel = await _db.TopografiaNiveles.AsNoTracking().FirstOrDefaultAsync(n => n.Id == request.NivelId, cancellationToken);
        if (nivel is null) { return TopografiaResult<TopografiaElementoNodeDto>.NotFound("El nivel no existe."); }

        int? ordenPadre = null;
        if (request.ParentId is long parentId)
        {
            var padre = await _db.TopografiaElementos.AsNoTracking()
                .Where(e => e.Id == parentId)
                .Select(e => new { e.Id, e.NivelId })
                .FirstOrDefaultAsync(cancellationToken);
            if (padre is null) { return TopografiaResult<TopografiaElementoNodeDto>.NotFound("El contenedor padre no existe."); }
            ordenPadre = await _db.TopografiaNiveles.AsNoTracking()
                .Where(n => n.Id == padre.NivelId).Select(n => (int?)n.Orden).FirstOrDefaultAsync(cancellationToken);
        }
        var jerarquiaError = TopografiaRules.ValidateJerarquia(ordenPadre, nivel.Orden);
        if (jerarquiaError is not null) { return TopografiaResult<TopografiaElementoNodeDto>.Invalid(jerarquiaError); }

        if (nivel.ControlaCapacidad && (request.Capacidad is null || request.Capacidad < 1))
        {
            return TopografiaResult<TopografiaElementoNodeDto>.Invalid(
                "Este nivel controla capacidad: la capacidad maxima es obligatoria y debe ser mayor a 0.");
        }

        var siglaError = await SiglaUnicaAsync(request.ParentId, request.Sigla, elementoId: null, cancellationToken);
        if (siglaError is not null) { return TopografiaResult<TopografiaElementoNodeDto>.Conflict(siglaError); }

        var elemento = new TopografiaElemento
        {
            TenantId = tenantId,
            NivelId = request.NivelId,
            ParentId = request.ParentId,
            Nombre = request.Nombre.Trim(),
            Sigla = request.Sigla.Trim().ToUpperInvariant(),
            Capacidad = nivel.ControlaCapacidad ? request.Capacidad : null,
            Estado = TopografiaEstado.Disponible
        };
        _db.TopografiaElementos.Add(elemento);
        _audit.Write(actorUserId, "topografia.elemento.create", nameof(TopografiaElemento), elemento,
            previousValue: null, newValue: SnapshotElem(elemento), tenantId: elemento.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TopografiaResult<TopografiaElementoNodeDto>.Ok(await GetNodeAsync(elemento.Id, cancellationToken));
    }

    public async Task<TopografiaResult<TopografiaElementoNodeDto>> UpdateElementoAsync(
        long elementoId, SaveElementoRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var elemento = await _db.TopografiaElementos.FirstOrDefaultAsync(e => e.Id == elementoId, cancellationToken);
        if (elemento is null) { return TopografiaResult<TopografiaElementoNodeDto>.NotFound("El elemento no existe."); }
        var error = TopografiaRules.ValidateElemento(request.Nombre, request.Sigla);
        if (error is not null) { return TopografiaResult<TopografiaElementoNodeDto>.Invalid(error); }

        // El tipo (nivel) y el contenedor (padre) quedan fijos en edicion (paridad con el legacy).
        var nivel = await _db.TopografiaNiveles.AsNoTracking().FirstAsync(n => n.Id == elemento.NivelId, cancellationToken);
        if (nivel.ControlaCapacidad && (request.Capacidad is null || request.Capacidad < 1))
        {
            return TopografiaResult<TopografiaElementoNodeDto>.Invalid(
                "Este nivel controla capacidad: la capacidad maxima es obligatoria y debe ser mayor a 0.");
        }
        var siglaError = await SiglaUnicaAsync(elemento.ParentId, request.Sigla, elementoId, cancellationToken);
        if (siglaError is not null) { return TopografiaResult<TopografiaElementoNodeDto>.Conflict(siglaError); }

        var prev = SnapshotElem(elemento);
        elemento.Nombre = request.Nombre.Trim();
        elemento.Sigla = request.Sigla.Trim().ToUpperInvariant();
        elemento.Capacidad = nivel.ControlaCapacidad ? request.Capacidad : null;
        _audit.Write(actorUserId, "topografia.elemento.update", nameof(TopografiaElemento), elemento,
            previousValue: prev, newValue: SnapshotElem(elemento), tenantId: elemento.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return TopografiaResult<TopografiaElementoNodeDto>.Ok(await GetNodeAsync(elemento.Id, cancellationToken));
    }

    public async Task<TopografiaResult<bool>> SetEstadoAsync(
        long elementoId, bool activar, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var elemento = await _db.TopografiaElementos.FirstOrDefaultAsync(e => e.Id == elementoId, cancellationToken);
        if (elemento is null) { return TopografiaResult<bool>.NotFound("El elemento no existe."); }
        var nuevo = activar ? TopografiaEstado.Disponible : TopografiaEstado.Inactivo;
        if (elemento.Estado == nuevo) { return TopografiaResult<bool>.Ok(true); }
        var prev = elemento.Estado;
        elemento.Estado = nuevo;
        _audit.Write(actorUserId, activar ? "topografia.elemento.reactivar" : "topografia.elemento.inactivar",
            nameof(TopografiaElemento), elemento,
            previousValue: new { Estado = prev }, newValue: new { elemento.Estado },
            tenantId: elemento.TenantId, reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return TopografiaResult<bool>.Ok(true);
    }

    // ---- Helpers ----

    private async Task<string?> SiglaUnicaAsync(long? parentId, string sigla, long? elementoId, CancellationToken cancellationToken)
    {
        var siglaUpper = sigla.Trim().ToUpperInvariant();
        var dup = await _db.TopografiaElementos.AsNoTracking().AnyAsync(
            e => e.ParentId == parentId && e.Sigla.ToUpper() == siglaUpper && (elementoId == null || e.Id != elementoId),
            cancellationToken);
        return dup ? $"Ya existe un elemento con la sigla '{siglaUpper}' en el mismo contenedor." : null;
    }

    private async Task<TopografiaElementoNodeDto> GetNodeAsync(long id, CancellationToken cancellationToken)
    {
        var e = await _db.TopografiaElementos.AsNoTracking().FirstAsync(x => x.Id == id, cancellationToken);
        var niveles = await _db.TopografiaNiveles.AsNoTracking().ToDictionaryAsync(n => n.Id, n => n, cancellationToken);
        var arbol = await _db.TopografiaElementos.AsNoTracking()
            .Select(x => new { x.Id, x.ParentId, x.Sigla }).ToListAsync(cancellationToken);
        var arbolSiglas = arbol.ToDictionary(x => x.Id, x => ((long?)x.ParentId, x.Sigla));
        var ocup = arbol.Count(x => x.ParentId == id);
        return ToNode(e, niveles, arbolSiglas, ocup, []);
    }

    private static TopografiaElementoNodeDto ToNode(
        TopografiaElemento e, IReadOnlyDictionary<long, TopografiaNivel> niveles,
        IReadOnlyDictionary<long, (long?, string)> arbolSiglas, int ocupacion,
        IReadOnlyList<TopografiaElementoNodeDto> children)
    {
        var nivel = niveles.GetValueOrDefault(e.NivelId);
        var controla = nivel?.ControlaCapacidad ?? false;
        // Estado efectivo: Inactivo manda; si controla capacidad y esta llena -> Lleno; si no, Disponible.
        var estado = e.Estado == TopografiaEstado.Inactivo
            ? TopografiaEstado.Inactivo
            : (controla && TopografiaRules.EstaLleno(ocupacion, e.Capacidad) ? TopografiaEstado.Lleno : TopografiaEstado.Disponible);
        return new TopografiaElementoNodeDto(
            e.Id, e.NivelId, nivel?.NombreNivel ?? "", e.ParentId, e.Nombre, e.Sigla,
            TopografiaRules.CodigoTopografico(e.Id, arbolSiglas!), e.Capacidad, ocupacion, controla, estado, children);
    }

    private static TopografiaNivelDto ToNivelDto(TopografiaNivel n)
        => new(n.Id, n.NombreNivel, n.SiglaBase, n.Orden, n.ControlaCapacidad);

    private static object SnapshotNivel(TopografiaNivel n) => new { n.NombreNivel, n.SiglaBase, n.Orden, n.ControlaCapacidad };
    private static object SnapshotElem(TopografiaElemento e) => new { e.NivelId, e.ParentId, e.Nombre, e.Sigla, e.Capacidad, e.Estado };
}
