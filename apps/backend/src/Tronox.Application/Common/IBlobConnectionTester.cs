namespace Tronox.Application.Common;

/// <summary>
/// Prueba de conectividad contra un Azure Blob Storage (ADR-012). La implementacion concreta (Azure SDK)
/// vive en Infrastructure; el caso de uso solo conoce esta abstraccion.
/// </summary>
public interface IBlobConnectionTester
{
    /// <summary>Intenta conectar/verificar el contenedor. Devuelve null si OK, o el mensaje de error.</summary>
    Task<string?> TestAsync(string connectionString, string container, CancellationToken cancellationToken = default);
}
