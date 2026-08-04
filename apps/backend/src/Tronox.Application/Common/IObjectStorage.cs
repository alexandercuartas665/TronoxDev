namespace Tronox.Application.Common;

/// <summary>
/// Almacenamiento de binarios en object storage (invariante #9: nunca BLOB en base de datos). El
/// proveedor concreto (Azure Blob Storage, ver ADR-009) vive en Infrastructure; los casos de uso solo
/// conocen esta abstraccion (intercambiable). La KEY que se persiste en el documento
/// (ruta_almacenamiento) es opaca para el dominio.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Sube el contenido bajo la key indicada (sobrescribe si existe).</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Descarga el contenido de la key, o null si no existe.</summary>
    Task<Stream?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Elimina el objeto (best-effort; no falla si ya no existe).</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
