using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Radicacion;

/// <summary>
/// Resolucion de la visibilidad de radicados por usuario (RF11-8). Adaptacion FAIL-CLOSED del helper
/// legacy (que era fail-open): el gate real del modulo es el permiso; esto es un tightening ADITIVO.
/// - Sin fila de visibilidad -> Todos (dentro del tenant, ya aislado por el filtro global).
/// - Con fila -> el nivel configurado.
/// - Error de resolucion -> Propios (lo mas restrictivo); NUNCA Todos.
/// La parte PURA (construir el predicado dado el nivel) es testeable sin BD.
/// </summary>
public sealed class RadicacionVisibilidadService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenant;

    public RadicacionVisibilidadService(IApplicationDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Predicado PURO de visibilidad segun nivel, usuario y dependencias del usuario.</summary>
    public static Expression<Func<Radicado, bool>> Filtro(VisibilidadNivel nivel, long? userId, IReadOnlyCollection<long> depIds)
        => nivel switch
        {
            VisibilidadNivel.Todos => _ => true,
            VisibilidadNivel.Dependencia => r =>
                r.FuncionarioAsignadoId == userId || r.FuncionarioOrigenId == userId || r.UsuarioRadicaId == userId
                || (r.DependenciaDestinoId != null && depIds.Contains(r.DependenciaDestinoId.Value))
                || (r.DependenciaOrigenId != null && depIds.Contains(r.DependenciaOrigenId.Value)),
            _ => r =>
                r.FuncionarioAsignadoId == userId || r.FuncionarioOrigenId == userId || r.UsuarioRadicaId == userId
        };

    /// <summary>Resuelve el predicado de visibilidad del usuario ACTUAL (fail-closed ante error).</summary>
    public async Task<Expression<Func<Radicado, bool>>> FiltroActualAsync(CancellationToken ct = default)
    {
        var userId = _tenant.UserId;
        try
        {
            var permiso = userId is null ? null : await _db.RadicadosVisibilidad.AsNoTracking()
                .FirstOrDefaultAsync(v => v.TenantUserId == userId && v.Activo, ct);

            // Sin config -> Todos (el permiso del modulo es el gate). Con config -> el nivel guardado.
            var nivel = permiso?.Nivel ?? VisibilidadNivel.Todos;
            if (nivel == VisibilidadNivel.Todos) { return Filtro(nivel, userId, Array.Empty<long>()); }

            var deps = nivel == VisibilidadNivel.Dependencia ? await DependenciasDelUsuarioAsync(userId, ct) : Array.Empty<long>();
            return Filtro(nivel, userId, deps);
        }
        catch
        {
            // Fail-closed: ante cualquier error de resolucion, lo mas restrictivo. NUNCA Todos.
            return Filtro(VisibilidadNivel.Propios, userId, Array.Empty<long>());
        }
    }

    /// <summary>Puede el usuario actual ver este radicado (gate de acceso directo al detalle).</summary>
    public async Task<bool> PuedeVerAsync(long radicadoId, CancellationToken ct = default)
    {
        var filtro = await FiltroActualAsync(ct);
        return await _db.Radicados.AsNoTracking().Where(filtro).AnyAsync(r => r.Id == radicadoId, ct);
    }

    // Dependencias (OrgUnit classifier Dependencia) del usuario: sube el arbol desde su cargo hasta la
    // dependencia mas cercana. Traduce la CTE recursiva del legacy sobre DOC_ENTREVISTAS_ORG a OrgUnit.
    private async Task<IReadOnlyCollection<long>> DependenciasDelUsuarioAsync(long? userId, CancellationToken ct)
    {
        if (userId is null) { return Array.Empty<long>(); }
        var cargoId = await _db.TenantUsers.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.CargoOrgUnitId).FirstOrDefaultAsync(ct);
        if (cargoId is null) { return Array.Empty<long>(); }

        var nodos = await _db.OrgUnits.AsNoTracking()
            .Select(o => new { o.Id, o.ParentId, o.Classifier }).ToListAsync(ct);
        var porId = nodos.ToDictionary(n => n.Id);
        var deps = new List<long>();
        var actual = cargoId;
        var guard = 0;
        while (actual is long id && porId.TryGetValue(id, out var n) && guard++ < 50)
        {
            if (n.Classifier == OrgUnitClassifier.Dependencia) { deps.Add(id); break; }
            actual = n.ParentId;
        }
        return deps;
    }
}
