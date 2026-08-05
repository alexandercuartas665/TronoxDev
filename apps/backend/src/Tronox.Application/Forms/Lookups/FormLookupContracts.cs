using Tronox.Domain.Enums;

namespace Tronox.Application.Forms.Lookups;

// ---- Contratos del framework de origenes de datos / lookup de un campo de formulario (RQ08, ola F1).
// En TRONOX las fuentes concretas estan DIFERIDAS: Tercero -> RQ07 (aun sin construir); Item y
// DataContainer son modulos propios de ECOREX, podados. Por eso el registro no tiene fuentes aun y
// SearchAsync/ResolveAsync degradan a vacio. El diseñador igual permite configurar el origen (se
// guarda en la pregunta) y el renderer avisa "fuente no disponible" hasta que exista el modulo. ----

/// <summary>Fila devuelta por una fuente de lookup: el valor guardado (id) + su etiqueta + campos extra
/// (para el autollenado). </summary>
public sealed record FormLookupRow(string Value, string Display, IReadOnlyDictionary<string, string?> Fields);

/// <summary>Descriptor de un campo de la fuente (para el editor de autollenado del diseñador).</summary>
public sealed record FormLookupField(string Name, string Label);

/// <summary>Fuente de datos disponible para un campo (Tercero, un contenedor, etc.).</summary>
public sealed record FormLookupSourceInfo(FormSourceKind Kind, string Ref, string Name);

/// <summary>
/// Adaptador de una FUENTE de lookup concreta (ej. Terceros). Tenant-scoped y parametrizado (nunca
/// SQL concatenado). En TRONOX no hay implementaciones registradas todavia (fuentes diferidas).
/// </summary>
public interface IFormLookupSource
{
    FormSourceKind Kind { get; }

    /// <summary>Fuentes concretas de este tipo disponibles para el tenant (para el selector del diseñador).</summary>
    Task<IReadOnlyList<FormLookupSourceInfo>> ListSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Campos de una fuente (para el editor de display/value/autofill).</summary>
    Task<IReadOnlyList<FormLookupField>> DescribeFieldsAsync(string sourceRef, CancellationToken cancellationToken = default);

    /// <summary>Busca filas por texto (autocompletar / modal). Acotado al tenant y al filtro (JSON).</summary>
    Task<IReadOnlyList<FormLookupRow>> SearchAsync(string sourceRef, string? query, string? filterJson, int take, CancellationToken cancellationToken = default);

    /// <summary>Resuelve una fila por su valor (id) para pintar la etiqueta / rehidratar el autollenado.</summary>
    Task<FormLookupRow?> ResolveAsync(string sourceRef, string value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registro central de fuentes de lookup por <see cref="FormSourceKind"/>. Elige la fuente y delega.
/// Sin fuentes registradas devuelve vacio (estado actual de TRONOX: fuentes diferidas).
/// </summary>
public interface IFormLookupService
{
    Task<IReadOnlyList<FormLookupSourceInfo>> ListSourcesAsync(FormSourceKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormLookupField>> DescribeFieldsAsync(FormSourceKind kind, string sourceRef, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormLookupRow>> SearchAsync(FormSourceKind kind, string sourceRef, string? query, string? filterJson, int take = 20, CancellationToken cancellationToken = default);
    Task<FormLookupRow?> ResolveAsync(FormSourceKind kind, string sourceRef, string value, CancellationToken cancellationToken = default);

    /// <summary>True si hay una fuente registrada para el tipo (el renderer lo usa para avisar cuando no).</summary>
    bool HasSource(FormSourceKind kind);
}
