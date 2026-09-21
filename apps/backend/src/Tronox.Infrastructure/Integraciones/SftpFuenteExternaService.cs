using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Tronox.Application.Documentos;

namespace Tronox.Infrastructure.Integraciones;

/// <summary>
/// Fuente externa por SFTP (RQ04 - RF20/RF21) con SSH.NET. Se conecta por host/usuario/clave (de un solo
/// uso), lista los archivos de una ruta y descarga uno. Best-effort con resultado tipado. Timeout corto
/// para no colgar la UI. No persiste credenciales.
/// </summary>
public sealed class SftpFuenteExternaService : IFuenteExternaService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private const long MaxBytes = 100L * 1024 * 1024; // 100 MB por archivo
    private readonly ILogger<SftpFuenteExternaService> _log;

    public SftpFuenteExternaService(ILogger<SftpFuenteExternaService> log) => _log = log;

    public Task<DocumentoResult<IReadOnlyList<ArchivoExternoDto>>> ListarSftpAsync(
        SftpConexion c, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            var err = Validar(c);
            if (err is not null) { return DocumentoResult<IReadOnlyList<ArchivoExternoDto>>.Invalid(err); }
            try
            {
                using var cli = Conectar(c);
                var ruta = string.IsNullOrWhiteSpace(c.RutaBase) ? "." : c.RutaBase.Trim();
                var archivos = cli.ListDirectory(ruta)
                    .Where(f => f.IsRegularFile && !f.Name.StartsWith('.'))
                    .OrderBy(f => f.Name)
                    .Select(f => new ArchivoExternoDto(f.Name, f.FullName, f.Length))
                    .ToList();
                cli.Disconnect();
                return DocumentoResult<IReadOnlyList<ArchivoExternoDto>>.Ok(archivos);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SFTP listar fallo ({Host})", c.Host);
                return DocumentoResult<IReadOnlyList<ArchivoExternoDto>>.Invalid("No se pudo conectar o listar la ruta SFTP: " + ex.Message);
            }
        }, cancellationToken);

    public Task<DocumentoResult<byte[]>> DescargarSftpAsync(
        SftpConexion c, string ruta, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            var err = Validar(c);
            if (err is not null) { return DocumentoResult<byte[]>.Invalid(err); }
            if (string.IsNullOrWhiteSpace(ruta)) { return DocumentoResult<byte[]>.Invalid("Ruta de archivo vacia."); }
            try
            {
                using var cli = Conectar(c);
                var attrs = cli.GetAttributes(ruta);
                if (attrs.Size > MaxBytes) { cli.Disconnect(); return DocumentoResult<byte[]>.Invalid("El archivo supera el limite de 100 MB."); }
                using var ms = new MemoryStream();
                cli.DownloadFile(ruta, ms);
                cli.Disconnect();
                return DocumentoResult<byte[]>.Ok(ms.ToArray());
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SFTP descargar fallo ({Host})", c.Host);
                return DocumentoResult<byte[]>.Invalid("No se pudo descargar el archivo por SFTP: " + ex.Message);
            }
        }, cancellationToken);

    private static string? Validar(SftpConexion c)
    {
        if (string.IsNullOrWhiteSpace(c.Host)) { return "Indica el host SFTP."; }
        if (c.Puerto is <= 0 or > 65535) { return "Puerto SFTP invalido."; }
        if (string.IsNullOrWhiteSpace(c.Usuario)) { return "Indica el usuario."; }
        if (string.IsNullOrWhiteSpace(c.Password)) { return "Indica la clave."; }
        return null;
    }

    private static SftpClient Conectar(SftpConexion c)
    {
        var info = new ConnectionInfo(c.Host.Trim(), c.Puerto, c.Usuario.Trim(),
            new PasswordAuthenticationMethod(c.Usuario.Trim(), c.Password))
        {
            Timeout = Timeout
        };
        var cli = new SftpClient(info) { OperationTimeout = Timeout };
        cli.Connect();
        return cli;
    }
}
