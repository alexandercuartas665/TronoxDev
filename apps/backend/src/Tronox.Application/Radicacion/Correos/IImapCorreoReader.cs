namespace Tronox.Application.Radicacion.Correos;

/// <summary>
/// Lector IMAP de un buzon (RF01-4, port del MailKitImapReader de VISAL). Implementacion en Infrastructure
/// (MailKit). No abre conexiones persistentes: conecta/autentica/lee/desconecta por operacion, y nunca
/// persiste ni loggea la clave (que llega ya descifrada).
/// </summary>
public interface IImapCorreoReader
{
    /// <summary>Prueba la conexion/credenciales y devuelve cuantos correos hay en la carpeta.</summary>
    Task<ImapTestResult> TestConnectionAsync(ImapParams p, CancellationToken ct = default);

    /// <summary>Lee correos (no leidos y/o desde una fecha) hasta un tope, con remitente/asunto/cuerpo/adjuntos.</summary>
    Task<IReadOnlyList<CorreoEntrante>> FetchAsync(ImapFetchParams p, CancellationToken ct = default);

    /// <summary>Marca como leidos (\Seen) los UIDs indicados. Best-effort.</summary>
    Task MarkSeenAsync(ImapParams p, IReadOnlyList<long> uids, CancellationToken ct = default);
}

/// <summary>Conexion IMAP: host, puerto, SSL, usuario (email) y clave descifrada (App Password).</summary>
public record ImapParams(string Host, int Port, bool UseSsl, string Username, string Password, string Folder);

public sealed record ImapFetchParams(string Host, int Port, bool UseSsl, string Username, string Password,
    string Folder, bool OnlyUnread, DateTimeOffset? Since, int MaxMessages)
    : ImapParams(Host, Port, UseSsl, Username, Password, Folder);

public sealed record ImapTestResult(bool Ok, int Count, string? Error);

/// <summary>Un adjunto crudo del correo (bytes en memoria).</summary>
public sealed record CorreoAdjuntoEntrante(string Nombre, byte[] Contenido, string? MimeType);

/// <summary>Correo leido del buzon (aun sin clasificar ni radicar).</summary>
public sealed record CorreoEntrante(
    string MessageId, long Uid, string? FromAddress, string? FromName, string Subject,
    DateTimeOffset? ReceivedAt, string BodyText, string? InReplyTo,
    IReadOnlyList<CorreoAdjuntoEntrante> Attachments);
