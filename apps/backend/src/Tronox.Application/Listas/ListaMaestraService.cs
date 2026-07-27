using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Listas;

/// <summary>
/// Administrador de Listas (RQ02 - RF03). Maestro-detalle Lista -> Opciones, tenant-scoped.
///
/// Reglas de negocio implementadas aqui:
/// 1. nombre_lista UNICO POR TENANT (RF03 3.3.4-1), case-insensitive.
/// 2. clave de opcion UNICA DENTRO DE LA LISTA. La opcion separa CLAVE (valor interno estable) de
///    VALOR (etiqueta visible): mejora sobre el legacy, que solo tenia el texto visible y ataba el
///    historico al literal.
/// 3. Nunca hay borrado fisico (invariante 8, RF03 3.3.4-3): listas y opciones se INACTIVAN.
/// 4. Orden de opciones editable (drag and drop, RF03 3.3.4-5): reordenar reescribe el orden 1..N.
/// 5. Usabilidad (RF03 3.3.4-2): una lista es usable si esta Activa y tiene >= 2 opciones activas.
/// 6. Toda alta/edicion/cambio de estado queda en la pista de auditoria (auditando la ENTIDAD).
/// </summary>
public sealed class ListaMaestraService : IListaMaestraService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public ListaMaestraService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Consulta ----

    public async Task<IReadOnlyList<ListaMaestraDto>> ListAsync(
        bool includeInactivas = true, CancellationToken cancellationToken = default)
    {
        var listas = await _db.ListasMaestras.AsNoTracking()
            .Where(l => includeInactivas || l.Estado == ListaEstado.Activo)
            .OrderBy(l => l.NombreLista)
            .ToListAsync(cancellationToken);

        var listaIds = listas.Select(l => l.Id).ToList();
        var opciones = await _db.ListaOpciones.AsNoTracking()
            .Where(o => listaIds.Contains(o.ListaMaestraId))
            .OrderBy(o => o.Orden)
            .ToListAsync(cancellationToken);
        var porLista = opciones.GroupBy(o => o.ListaMaestraId).ToDictionary(g => g.Key, g => g.ToList());

        return listas.Select(l => ToDto(l,
            porLista.TryGetValue(l.Id, out var ops) ? ops : [])).ToList();
    }

    public async Task<ListaMaestraDto?> GetAsync(long listaId, CancellationToken cancellationToken = default)
    {
        var lista = await _db.ListasMaestras.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listaId, cancellationToken);
        if (lista is null) { return null; }
        var opciones = await _db.ListaOpciones.AsNoTracking()
            .Where(o => o.ListaMaestraId == listaId)
            .OrderBy(o => o.Orden)
            .ToListAsync(cancellationToken);
        return ToDto(lista, opciones);
    }

    public async Task<ListaKpisDto> GetKpisAsync(CancellationToken cancellationToken = default)
    {
        var listas = await _db.ListasMaestras.AsNoTracking()
            .Select(l => new { l.Id, l.Estado })
            .ToListAsync(cancellationToken);
        var activasPorLista = await _db.ListaOpciones.AsNoTracking()
            .Where(o => o.Estado == ListaEstado.Activo)
            .GroupBy(o => o.ListaMaestraId)
            .Select(g => new { ListaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ListaId, x => x.Count, cancellationToken);

        var usables = listas.Count(l => ListaRules.EsUsable(
            l.Estado == ListaEstado.Activo,
            activasPorLista.TryGetValue(l.Id, out var n) ? n : 0));

        return new ListaKpisDto(
            Total: listas.Count,
            Usables: usables,
            Inactivas: listas.Count(l => l.Estado == ListaEstado.Inactivo));
    }

    // ---- Lista ----

    public async Task<ListaResult<ListaMaestraDto>> CreateListaAsync(
        SaveListaRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return ListaResult<ListaMaestraDto>.Invalid("No hay tenant activo.");
        }
        var validation = await ValidateListaAsync(request, listaId: null, cancellationToken);
        if (validation is not null) { return validation.To<ListaMaestraDto>(); }

        var lista = new ListaMaestra
        {
            TenantId = tenantId,
            NombreLista = request.NombreLista.Trim(),
            Descripcion = Normalize(request.Descripcion),
            Estado = ListaEstado.Activo
        };
        _db.ListasMaestras.Add(lista);
        _audit.Write(actorUserId, "lista.create", nameof(ListaMaestra), lista,
            previousValue: null, newValue: new { lista.NombreLista, lista.Descripcion, lista.Estado },
            tenantId: lista.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<ListaMaestraDto>.Ok(ToDto(lista, []));
    }

    public async Task<ListaResult<ListaMaestraDto>> UpdateListaAsync(
        long listaId, SaveListaRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var lista = await _db.ListasMaestras.FirstOrDefaultAsync(l => l.Id == listaId, cancellationToken);
        if (lista is null) { return ListaResult<ListaMaestraDto>.NotFound("La lista no existe."); }
        var validation = await ValidateListaAsync(request, listaId, cancellationToken);
        if (validation is not null) { return validation.To<ListaMaestraDto>(); }

        var prev = new { lista.NombreLista, lista.Descripcion };
        lista.NombreLista = request.NombreLista.Trim();
        lista.Descripcion = Normalize(request.Descripcion);
        _audit.Write(actorUserId, "lista.update", nameof(ListaMaestra), lista,
            previousValue: prev, newValue: new { lista.NombreLista, lista.Descripcion },
            tenantId: lista.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<ListaMaestraDto>.Ok((await GetAsync(listaId, cancellationToken))!);
    }

    public async Task<ListaResult<bool>> SetListaEstadoAsync(
        long listaId, bool activar, long actorUserId, string? motivo = null,
        CancellationToken cancellationToken = default)
    {
        var lista = await _db.ListasMaestras.FirstOrDefaultAsync(l => l.Id == listaId, cancellationToken);
        if (lista is null) { return ListaResult<bool>.NotFound("La lista no existe."); }

        var nuevo = activar ? ListaEstado.Activo : ListaEstado.Inactivo;
        if (lista.Estado == nuevo) { return ListaResult<bool>.Ok(true); }
        var prev = lista.Estado;
        lista.Estado = nuevo;
        _audit.Write(actorUserId, activar ? "lista.reactivar" : "lista.inactivar", nameof(ListaMaestra), lista,
            previousValue: new { Estado = prev }, newValue: new { lista.Estado },
            tenantId: lista.TenantId, reason: motivo);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<bool>.Ok(true);
    }

    // ---- Opciones ----

    public async Task<ListaResult<ListaOpcionDto>> AddOpcionAsync(
        long listaId, SaveOpcionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var lista = await _db.ListasMaestras.FirstOrDefaultAsync(l => l.Id == listaId, cancellationToken);
        if (lista is null) { return ListaResult<ListaOpcionDto>.NotFound("La lista no existe."); }

        var validation = await ValidateOpcionAsync(listaId, request, opcionId: null, cancellationToken);
        if (validation is not null) { return validation.To<ListaOpcionDto>(); }

        var maxOrden = await _db.ListaOpciones
            .Where(o => o.ListaMaestraId == listaId)
            .Select(o => (int?)o.Orden)
            .MaxAsync(cancellationToken) ?? 0;

        var opcion = new ListaOpcion
        {
            TenantId = lista.TenantId,
            ListaMaestraId = listaId,
            Clave = request.Clave.Trim(),
            Valor = request.Valor.Trim(),
            Orden = maxOrden + 1,
            Estado = ListaEstado.Activo
        };
        _db.ListaOpciones.Add(opcion);
        _audit.Write(actorUserId, "lista.opcion.create", nameof(ListaOpcion), opcion,
            previousValue: null, newValue: Snapshot(opcion), tenantId: opcion.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<ListaOpcionDto>.Ok(ToOpcionDto(opcion));
    }

    public async Task<ListaResult<ListaOpcionDto>> UpdateOpcionAsync(
        long opcionId, SaveOpcionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var opcion = await _db.ListaOpciones.FirstOrDefaultAsync(o => o.Id == opcionId, cancellationToken);
        if (opcion is null) { return ListaResult<ListaOpcionDto>.NotFound("La opcion no existe."); }

        var validation = await ValidateOpcionAsync(opcion.ListaMaestraId, request, opcionId, cancellationToken);
        if (validation is not null) { return validation.To<ListaOpcionDto>(); }

        var prev = Snapshot(opcion);
        opcion.Clave = request.Clave.Trim();
        opcion.Valor = request.Valor.Trim();
        _audit.Write(actorUserId, "lista.opcion.update", nameof(ListaOpcion), opcion,
            previousValue: prev, newValue: Snapshot(opcion), tenantId: opcion.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<ListaOpcionDto>.Ok(ToOpcionDto(opcion));
    }

    public async Task<ListaResult<bool>> SetOpcionEstadoAsync(
        long opcionId, bool activar, long actorUserId, CancellationToken cancellationToken = default)
    {
        var opcion = await _db.ListaOpciones.FirstOrDefaultAsync(o => o.Id == opcionId, cancellationToken);
        if (opcion is null) { return ListaResult<bool>.NotFound("La opcion no existe."); }

        var nuevo = activar ? ListaEstado.Activo : ListaEstado.Inactivo;
        if (opcion.Estado == nuevo) { return ListaResult<bool>.Ok(true); }
        var prev = opcion.Estado;
        opcion.Estado = nuevo;
        // RF03 3.3.4-4: inactivar una opcion no borra los valores ya guardados; solo deja de ofrecerse.
        _audit.Write(actorUserId, activar ? "lista.opcion.reactivar" : "lista.opcion.inactivar",
            nameof(ListaOpcion), opcion,
            previousValue: new { Estado = prev }, newValue: new { opcion.Estado },
            tenantId: opcion.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<bool>.Ok(true);
    }

    public async Task<ListaResult<bool>> ReordenarOpcionesAsync(
        long listaId, IReadOnlyList<long> opcionIdsEnOrden, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        var opciones = await _db.ListaOpciones
            .Where(o => o.ListaMaestraId == listaId)
            .ToListAsync(cancellationToken);
        if (opciones.Count == 0) { return ListaResult<bool>.NotFound("La lista no tiene opciones."); }

        // El conjunto de ids recibido debe ser EXACTAMENTE el de la lista: ni faltantes ni ajenos.
        var actuales = opciones.Select(o => o.Id).ToHashSet();
        if (opcionIdsEnOrden.Count != actuales.Count || !opcionIdsEnOrden.All(actuales.Contains))
        {
            return ListaResult<bool>.Invalid(
                "El reordenamiento debe incluir exactamente las opciones de la lista.");
        }

        var porId = opciones.ToDictionary(o => o.Id);
        for (var i = 0; i < opcionIdsEnOrden.Count; i++)
        {
            porId[opcionIdsEnOrden[i]].Orden = i + 1; // orden 1..N
        }
        _audit.Write(actorUserId, "lista.opcion.reordenar", nameof(ListaMaestra), listaId,
            previousValue: null, newValue: new { Orden = opcionIdsEnOrden },
            tenantId: _tenantContext.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return ListaResult<bool>.Ok(true);
    }

    // ---- Validacion / mapeo ----

    private async Task<ListaResult<bool>?> ValidateListaAsync(
        SaveListaRequest request, long? listaId, CancellationToken cancellationToken)
    {
        var error = ListaRules.ValidateLista(request.NombreLista, request.Descripcion);
        if (error is not null) { return ListaResult<bool>.Invalid(error); }

        var nombreUpper = request.NombreLista.Trim().ToUpperInvariant();
        var dup = await _db.ListasMaestras.AsNoTracking().AnyAsync(
            l => l.NombreLista.ToUpper() == nombreUpper && (listaId == null || l.Id != listaId),
            cancellationToken);
        if (dup)
        {
            return ListaResult<bool>.Conflict($"Ya existe una lista con el nombre '{request.NombreLista.Trim()}'.");
        }
        return null;
    }

    private async Task<ListaResult<bool>?> ValidateOpcionAsync(
        long listaId, SaveOpcionRequest request, long? opcionId, CancellationToken cancellationToken)
    {
        var error = ListaRules.ValidateOpcion(request.Clave, request.Valor);
        if (error is not null) { return ListaResult<bool>.Invalid(error); }

        // clave UNICA DENTRO DE LA LISTA (case-insensitive).
        var claveUpper = request.Clave.Trim().ToUpperInvariant();
        var dup = await _db.ListaOpciones.AsNoTracking().AnyAsync(
            o => o.ListaMaestraId == listaId && o.Clave.ToUpper() == claveUpper
                 && (opcionId == null || o.Id != opcionId),
            cancellationToken);
        if (dup)
        {
            return ListaResult<bool>.Conflict($"Ya existe una opcion con la clave '{request.Clave.Trim()}' en la lista.");
        }
        return null;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ListaMaestraDto ToDto(ListaMaestra l, IReadOnlyList<ListaOpcion> opciones)
        => new(l.Id, l.NombreLista, l.Descripcion, l.Estado,
            opciones.OrderBy(o => o.Orden).Select(ToOpcionDto).ToList());

    private static ListaOpcionDto ToOpcionDto(ListaOpcion o)
        => new(o.Id, o.ListaMaestraId, o.Clave, o.Valor, o.Orden, o.Estado);

    private static object Snapshot(ListaOpcion o) => new { o.Clave, o.Valor, o.Orden, o.Estado };
}
