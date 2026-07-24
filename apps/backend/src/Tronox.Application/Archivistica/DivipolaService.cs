using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;

namespace Tronox.Application.Archivistica;

/// <summary>
/// Lectura de los catalogos territoriales DIVIPOLA. Solo lectura: el catalogo lo siembra la
/// migracion y ningun tenant lo edita, asi que no hay Save ni auditoria que escribir.
/// </summary>
public sealed class DivipolaService : IDivipolaService
{
    private readonly IApplicationDbContext _db;

    public DivipolaService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaisDto>> ListPaisesAsync(CancellationToken cancellationToken = default)
        => await _db.Paises.AsNoTracking()
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new PaisDto(p.Id, p.CodigoIso2, p.CodigoIso3, p.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DepartamentoDto>> ListDepartamentosAsync(
        long paisId, CancellationToken cancellationToken = default)
        => await _db.Departamentos.AsNoTracking()
            .Where(d => d.PaisId == paisId && d.Activo)
            .OrderBy(d => d.Nombre)
            .Select(d => new DepartamentoDto(d.Id, d.PaisId, d.CodigoDane, d.Nombre))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MunicipioDto>> ListMunicipiosAsync(
        long departamentoId, CancellationToken cancellationToken = default)
        => await _db.Municipios.AsNoTracking()
            .Where(m => m.DepartamentoId == departamentoId && m.Activo)
            // La capital primero: es la opcion mas probable y ahorra buscarla en la lista.
            .OrderByDescending(m => m.EsCapital)
            .ThenBy(m => m.Nombre)
            .Select(m => new MunicipioDto(m.Id, m.DepartamentoId, m.CodigoDivipola, m.Nombre, m.EsCapital))
            .ToListAsync(cancellationToken);

    public async Task<MunicipioDto?> GetMunicipioAsync(
        long municipioId, CancellationToken cancellationToken = default)
        => await _db.Municipios.AsNoTracking()
            .Where(m => m.Id == municipioId)
            .Select(m => new MunicipioDto(m.Id, m.DepartamentoId, m.CodigoDivipola, m.Nombre, m.EsCapital))
            .FirstOrDefaultAsync(cancellationToken);
}
