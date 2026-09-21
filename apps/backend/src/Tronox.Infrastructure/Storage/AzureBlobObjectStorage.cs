using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Storage;

/// <summary>Opciones GLOBALES del object storage (ADR-009). Fallback cuando el tenant no configura la
/// suya. El connection string vive fuera del repo.</summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Connection string de Azure Blob. En dev: "UseDevelopmentStorage=true" (Azurite).</summary>
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    /// <summary>Contenedor donde viven los binarios de documentos.</summary>
    public string Container { get; set; } = "tronox-documentos";
}

/// <summary>
/// Implementacion de <see cref="IObjectStorage"/> sobre Azure Blob Storage (ADR-009 + ADR-012). Resuelve
/// la cuenta POR TENANT: si la entidad tiene un <c>AlmacenamientoConfig</c> activo, usa su cadena de
/// conexion (descifrada) y contenedor; si no, cae al proveedor GLOBAL (Azurite/env). SCOPED: lee la config
/// del tenant de la peticion. El contenedor se crea de forma perezosa e idempotente.
/// </summary>
public sealed class AzureBlobObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _global;
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _protector;

    private BlobContainerClient? _container;
    private string _prefix = "";

    public AzureBlobObjectStorage(
        IOptions<ObjectStorageOptions> options, IApplicationDbContext db, ISecretProtector protector)
    {
        _global = options.Value;
        _db = db;
        _protector = protector;
    }

    private async Task<BlobContainerClient> ResolveAsync(CancellationToken ct)
    {
        if (_container is not null) { return _container; }

        var conn = _global.ConnectionString;
        var container = _global.Container;
        // AlmacenamientosConfig es tenant-scoped: el filtro global devuelve solo la del tenant actual.
        var cfg = await _db.AlmacenamientosConfig.AsNoTracking().FirstOrDefaultAsync(ct);
        if (cfg is not null && cfg.Activo && !string.IsNullOrWhiteSpace(cfg.ConnectionStringCifrada))
        {
            conn = _protector.Unprotect(cfg.ConnectionStringCifrada);
            if (!string.IsNullOrWhiteSpace(cfg.Contenedor)) { container = cfg.Contenedor; }
            _prefix = string.IsNullOrWhiteSpace(cfg.Prefijo) ? "" : cfg.Prefijo.Trim().Trim('/');
        }

        _container = new BlobServiceClient(conn).GetBlobContainerClient(container);
        return _container;
    }

    private string FullKey(string key) => string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}/{key}";

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var container = await ResolveAsync(cancellationToken);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(FullKey(key));
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    public async Task<Stream?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var container = await ResolveAsync(cancellationToken);
        var blob = container.GetBlobClient(FullKey(key));
        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return response.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var container = await ResolveAsync(cancellationToken);
        var blob = container.GetBlobClient(FullKey(key));
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}

/// <summary>Prueba de conectividad contra Azure Blob (ADR-012). Stateless.</summary>
public sealed class BlobConnectionTester : IBlobConnectionTester
{
    public async Task<string?> TestAsync(string connectionString, string container, CancellationToken cancellationToken = default)
    {
        try
        {
            var svc = new BlobServiceClient(connectionString);
            var c = svc.GetBlobContainerClient(container);
            await c.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _ = await c.ExistsAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
