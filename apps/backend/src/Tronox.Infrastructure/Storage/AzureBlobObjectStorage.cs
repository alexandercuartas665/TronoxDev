using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Tronox.Application.Common;

namespace Tronox.Infrastructure.Storage;

/// <summary>Opciones del object storage (ADR-009). El connection string vive fuera del repo.</summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Connection string de Azure Blob. En dev: "UseDevelopmentStorage=true" (Azurite).</summary>
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";

    /// <summary>Contenedor donde viven los binarios de documentos.</summary>
    public string Container { get; set; } = "tronox-documentos";
}

/// <summary>
/// Implementacion de <see cref="IObjectStorage"/> sobre Azure Blob Storage (ADR-009). En local apunta
/// a Azurite via connection string de desarrollo. El contenedor se crea de forma perezosa e idempotente.
/// </summary>
public sealed class AzureBlobObjectStorage : IObjectStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        var opts = options.Value;
        var service = new BlobServiceClient(opts.ConnectionString);
        _container = service.GetBlobContainerClient(opts.Container);
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = _container.GetBlobClient(key);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    public async Task<Stream?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(key);
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
        var blob = _container.GetBlobClient(key);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
