using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Plantillas;

/// <summary>
/// Plantillas documentales (RQ04 - RF09). El aislamiento por tenant lo da el filtro global. La
/// asociacion a tipologias es N:N; la primera tipologia se copia como "representante" para la galeria.
/// </summary>
public sealed class PlantillaService : IPlantillaService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public PlantillaService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<PlantillaItemDto>> ListarAsync(
        string? texto = null, bool incluirInactivas = true, CancellationToken cancellationToken = default)
    {
        var query = _db.Plantillas.AsNoTracking()
            .Include(p => p.TrdTipologia)
            .Include(p => p.Tipos)
            .AsQueryable();
        if (!incluirInactivas) { query = query.Where(p => p.Estado == PlantillaEstado.Activa); }
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            query = query.Where(p => p.Nombre.ToLower().Contains(t)
                                     || (p.TrdTipologia != null && p.TrdTipologia.Nombre.ToLower().Contains(t)));
        }
        return await query.OrderByDescending(p => p.Estado == PlantillaEstado.Activa).ThenBy(p => p.Nombre)
            .Select(p => new PlantillaItemDto(
                p.Id, p.Nombre, p.TrdTipologia != null ? p.TrdTipologia.Nombre : null,
                p.Estado, p.VariablesNum, p.UsoContador, p.Tipos.Count, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlantillaDetalleDto?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var p = await _db.Plantillas.AsNoTracking()
            .Include(x => x.Tipos!).ThenInclude(t => t.TrdTipologia)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return p is null ? null : ToDetalle(p);
    }

    public async Task<PlantillaResult<PlantillaDetalleDto>> CrearAsync(
        SavePlantillaRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var err = PlantillaRules.ValidateNombre(request.Nombre);
        if (err is not null) { return PlantillaResult<PlantillaDetalleDto>.Invalid(err); }

        var tenantId = _tenantContext.TenantId!.Value;
        var tipos = await CargarTiposAsync(request.TipologiaIds, cancellationToken);

        var plantilla = new Plantilla
        {
            TenantId = tenantId,
            Nombre = request.Nombre.Trim(),
            Descripcion = Trim(request.Descripcion),
            ContenidoHtml = request.ContenidoHtml,
            TrdTipologiaId = tipos.Count > 0 ? tipos[0].Id : null,
            FormatoPapel = request.FormatoPapel,
            Orientacion = request.Orientacion,
            Margenes = request.Margenes,
            Encabezado = Trim(request.Encabezado),
            PiePagina = Trim(request.PiePagina),
            VariablesNum = PlantillaRules.ContarVariables(request.ContenidoHtml),
            Estado = PlantillaEstado.Activa,
            UsoContador = 0
        };
        foreach (var (id, nombre) in tipos)
        {
            plantilla.Tipos.Add(new PlantillaTipo { TenantId = tenantId, TrdTipologiaId = id, TipologiaNombre = nombre });
        }
        _db.Plantillas.Add(plantilla);
        _audit.Write(actorUserId, "plantilla.crear", nameof(Plantilla), plantilla,
            previousValue: null, newValue: new { plantilla.Nombre, Tipos = tipos.Count }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return PlantillaResult<PlantillaDetalleDto>.Ok((await GetAsync(plantilla.Id, cancellationToken))!);
    }

    public async Task<PlantillaResult<PlantillaDetalleDto>> ActualizarAsync(
        long id, SavePlantillaRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var err = PlantillaRules.ValidateNombre(request.Nombre);
        if (err is not null) { return PlantillaResult<PlantillaDetalleDto>.Invalid(err); }

        var plantilla = await _db.Plantillas.Include(p => p.Tipos)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plantilla is null) { return PlantillaResult<PlantillaDetalleDto>.NotFound("La plantilla no existe."); }

        var tipos = await CargarTiposAsync(request.TipologiaIds, cancellationToken);
        plantilla.Nombre = request.Nombre.Trim();
        plantilla.Descripcion = Trim(request.Descripcion);
        plantilla.ContenidoHtml = request.ContenidoHtml;
        plantilla.TrdTipologiaId = tipos.Count > 0 ? tipos[0].Id : null;
        plantilla.FormatoPapel = request.FormatoPapel;
        plantilla.Orientacion = request.Orientacion;
        plantilla.Margenes = request.Margenes;
        plantilla.Encabezado = Trim(request.Encabezado);
        plantilla.PiePagina = Trim(request.PiePagina);
        plantilla.VariablesNum = PlantillaRules.ContarVariables(request.ContenidoHtml);

        // Reemplazo completo de asociaciones (DELETE + reinsert), como el legacy.
        plantilla.Tipos.Clear();
        foreach (var (tid, nombre) in tipos)
        {
            plantilla.Tipos.Add(new PlantillaTipo { TenantId = plantilla.TenantId, TrdTipologiaId = tid, TipologiaNombre = nombre });
        }
        _audit.Write(actorUserId, "plantilla.editar", nameof(Plantilla), plantilla,
            previousValue: null, newValue: new { plantilla.Nombre, Tipos = tipos.Count }, tenantId: plantilla.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return PlantillaResult<PlantillaDetalleDto>.Ok((await GetAsync(plantilla.Id, cancellationToken))!);
    }

    public async Task<PlantillaResult<bool>> CambiarEstadoAsync(long id, long actorUserId, CancellationToken cancellationToken = default)
    {
        var plantilla = await _db.Plantillas.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (plantilla is null) { return PlantillaResult<bool>.NotFound("La plantilla no existe."); }
        var previo = plantilla.Estado;
        plantilla.Estado = plantilla.Estado == PlantillaEstado.Activa ? PlantillaEstado.Inactiva : PlantillaEstado.Activa;
        _audit.Write(actorUserId, "plantilla.cambiar_estado", nameof(Plantilla), plantilla,
            previousValue: new { Estado = previo.ToString() }, newValue: new { Estado = plantilla.Estado.ToString() },
            tenantId: plantilla.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return PlantillaResult<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<TipologiaOpcionDto>> GetTipologiasAsync(CancellationToken cancellationToken = default)
        => await _db.TrdTipologias.AsNoTracking()
            .Where(t => !t.IsArchived)
            .OrderBy(t => t.Nombre)
            .Select(t => new TipologiaOpcionDto(t.Id, t.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VariableDto>> GetVariablesAsync(
        IReadOnlyList<long> tipologiaIds, CancellationToken cancellationToken = default)
    {
        var vars = new List<VariableDto>(PlantillaRules.VariablesBase());
        if (tipologiaIds.Count > 0)
        {
            var metas = await _db.TrdMetadatos.AsNoTracking()
                .Where(m => m.TrdTipologiaId != null && tipologiaIds.Contains(m.TrdTipologiaId.Value)
                            && m.Contexto == ContextoMetadato.Documento && !m.IsArchived)
                .Select(m => m.Nombre)
                .Distinct()
                .ToListAsync(cancellationToken);
            vars.AddRange(metas.Select(n => new VariableDto("Metadatos", $"{{{{{n}}}}}", n, true)));
        }
        return vars;
    }

    // ---- Helpers ----

    private async Task<List<(long Id, string? Nombre)>> CargarTiposAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        var distintos = ids.Distinct().ToList();
        if (distintos.Count == 0) { return []; }
        var tipologias = await _db.TrdTipologias.AsNoTracking()
            .Where(t => distintos.Contains(t.Id))
            .Select(t => new { t.Id, t.Nombre })
            .ToListAsync(cancellationToken);
        // Preserva el orden de seleccion (la primera es la representante).
        return distintos
            .Select(id => (id, tipologias.FirstOrDefault(t => t.Id == id)?.Nombre))
            .Where(x => x.Item2 is not null)
            .ToList();
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static PlantillaDetalleDto ToDetalle(Plantilla p) => new(
        p.Id, p.Nombre, p.Descripcion, p.ContenidoHtml, p.FormatoPapel, p.Orientacion, p.Margenes,
        p.Encabezado, p.PiePagina, p.Estado, p.VariablesNum, p.UsoContador,
        p.Tipos.Select(t => t.TrdTipologiaId).ToList(),
        p.Tipos.Select(t => new TipologiaOpcionDto(t.TrdTipologiaId, t.TipologiaNombre ?? t.TrdTipologia?.Nombre ?? "")).ToList());
}
