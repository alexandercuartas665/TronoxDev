namespace Tronox.Application.Documentos;

/// <summary>Conexion SFTP para importar documentos (RQ04 - RF20/RF21). La clave es de un solo uso (no se persiste).</summary>
public sealed record SftpConexion(string Host, int Puerto, string Usuario, string Password, string RutaBase);

/// <summary>Un archivo listado en la fuente externa.</summary>
public sealed record ArchivoExternoDto(string Nombre, string Ruta, long Tamano);

/// <summary>
/// Importacion de documentos desde una fuente externa (RQ04 - RF20/RF21). Primera fuente: SFTP (host +
/// usuario + clave de un solo uso). Los proveedores cloud (OneDrive/Google Drive/SharePoint) requieren
/// registro OAuth y quedan diferidos. Best-effort con resultado tipado; nunca persiste credenciales.
/// </summary>
public interface IFuenteExternaService
{
    /// <summary>Lista los archivos de la ruta indicada por SFTP.</summary>
    Task<DocumentoResult<IReadOnlyList<ArchivoExternoDto>>> ListarSftpAsync(SftpConexion conexion, CancellationToken cancellationToken = default);

    /// <summary>Descarga un archivo por SFTP y devuelve sus bytes.</summary>
    Task<DocumentoResult<byte[]>> DescargarSftpAsync(SftpConexion conexion, string ruta, CancellationToken cancellationToken = default);
}
