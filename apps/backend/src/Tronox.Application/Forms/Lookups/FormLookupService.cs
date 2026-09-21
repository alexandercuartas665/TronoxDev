using Tronox.Domain.Enums;

namespace Tronox.Application.Forms.Lookups;

/// <summary>
/// Registro central de fuentes de lookup (RQ08, ola F1). Elige la <see cref="IFormLookupSource"/> por
/// <see cref="FormSourceKind"/> y delega. En TRONOX no hay fuentes registradas todavia (Tercero -> RQ07;
/// Item/DataContainer son modulos de ECOREX podados), asi que degrada a vacio: el diseñador puede
/// configurar el origen igualmente y el renderer avisa "fuente no disponible".
/// </summary>
public sealed class FormLookupService : IFormLookupService
{
    private readonly IReadOnlyDictionary<FormSourceKind, IFormLookupSource> _sources;

    // TODO(RQ07): registrar TerceroLookupSource cuando exista el Catalogo de Terceros. Item y
    // DataContainer no aplican a TRONOX (modulos propios de ECOREX, podados).
    public FormLookupService(IEnumerable<IFormLookupSource> sources)
    {
        _sources = sources.ToDictionary(s => s.Kind);
    }

    public bool HasSource(FormSourceKind kind) => _sources.ContainsKey(kind);

    public async Task<IReadOnlyList<FormLookupSourceInfo>> ListSourcesAsync(FormSourceKind kind, CancellationToken cancellationToken = default)
        => _sources.TryGetValue(kind, out var src) ? await src.ListSourcesAsync(cancellationToken) : [];

    public async Task<IReadOnlyList<FormLookupField>> DescribeFieldsAsync(FormSourceKind kind, string sourceRef, CancellationToken cancellationToken = default)
        => _sources.TryGetValue(kind, out var src) ? await src.DescribeFieldsAsync(sourceRef, cancellationToken) : [];

    public async Task<IReadOnlyList<FormLookupRow>> SearchAsync(FormSourceKind kind, string sourceRef, string? query, string? filterJson, int take = 20, CancellationToken cancellationToken = default)
        => _sources.TryGetValue(kind, out var src) ? await src.SearchAsync(sourceRef, query, filterJson, take, cancellationToken) : [];

    public async Task<FormLookupRow?> ResolveAsync(FormSourceKind kind, string sourceRef, string value, CancellationToken cancellationToken = default)
        => _sources.TryGetValue(kind, out var src) ? await src.ResolveAsync(sourceRef, value, cancellationToken) : null;
}
