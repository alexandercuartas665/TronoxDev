using System.Text;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Tronox.Application.Radicacion.Correos;

namespace Tronox.Infrastructure.Email;

/// <summary>
/// Lector IMAP con MailKit (port de MailKitImapReader de VISAL). Conecta por operacion, autentica con la
/// App Password ya descifrada, lee no-leidos desde el watermark y extrae remitente/asunto/cuerpo/adjuntos.
/// Best-effort en cuerpo HTML->texto y adjuntos (limites: 15 MB por adjunto, 10 por correo).
/// </summary>
public sealed class MailKitImapCorreoReader : IImapCorreoReader
{
    private const long MaxAdjuntoBytes = 15L * 1024 * 1024;
    private const int MaxAdjuntos = 10;

    private static async Task<ImapClient> ConnectAsync(ImapParams p, CancellationToken ct)
    {
        var client = new ImapClient();
        await client.ConnectAsync(p.Host, p.Port,
            p.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(p.Username, p.Password, ct);
        return client;
    }

    private static async Task<IMailFolder> OpenFolderAsync(ImapClient client, string folder, FolderAccess access, CancellationToken ct)
    {
        var f = string.Equals(folder, "INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(folder, ct);
        await f.OpenAsync(access, ct);
        return f;
    }

    public async Task<ImapTestResult> TestConnectionAsync(ImapParams p, CancellationToken ct = default)
    {
        try
        {
            using var client = await ConnectAsync(p, ct);
            var folder = await OpenFolderAsync(client, p.Folder, FolderAccess.ReadOnly, ct);
            var count = folder.Count;
            await client.DisconnectAsync(true, ct);
            return new ImapTestResult(true, count, null);
        }
        catch (AuthenticationException)
        {
            return new ImapTestResult(false, 0,
                "Autenticacion fallida. Gmail requiere 2FA activo y una App Password (la clave normal no sirve para IMAP).");
        }
        catch (Exception ex)
        {
            return new ImapTestResult(false, 0, ex.Message);
        }
    }

    public async Task<IReadOnlyList<CorreoEntrante>> FetchAsync(ImapFetchParams p, CancellationToken ct = default)
    {
        using var client = await ConnectAsync(p, ct);
        var folder = await OpenFolderAsync(client, p.Folder, FolderAccess.ReadOnly, ct);

        SearchQuery query = p.OnlyUnread ? SearchQuery.NotSeen : SearchQuery.All;
        if (p.Since is DateTimeOffset since)
        {
            query = query.And(SearchQuery.DeliveredAfter(since.UtcDateTime));
        }

        var uids = await folder.SearchAsync(query, ct);
        var elegidos = uids.OrderByDescending(u => u.Id).Take(Math.Max(1, p.MaxMessages)).OrderBy(u => u.Id).ToList();

        var lista = new List<CorreoEntrante>(elegidos.Count);
        foreach (var uid in elegidos)
        {
            ct.ThrowIfCancellationRequested();
            var msg = await folder.GetMessageAsync(uid, ct);
            var from = msg.From.Mailboxes.FirstOrDefault();
            var body = msg.TextBody;
            if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(msg.HtmlBody))
            {
                body = HtmlToText(msg.HtmlBody);
            }
            var messageId = string.IsNullOrWhiteSpace(msg.MessageId) ? $"uid:{uid.Id}" : msg.MessageId;
            lista.Add(new CorreoEntrante(
                messageId, uid.Id, from?.Address, from?.Name, msg.Subject ?? "(sin asunto)",
                msg.Date, body ?? "", msg.InReplyTo, ExtraerAdjuntos(msg)));
        }
        await client.DisconnectAsync(true, ct);
        return lista;
    }

    public async Task MarkSeenAsync(ImapParams p, IReadOnlyList<long> uids, CancellationToken ct = default)
    {
        if (uids is not { Count: > 0 }) { return; }
        try
        {
            using var client = await ConnectAsync(p, ct);
            var folder = await OpenFolderAsync(client, p.Folder, FolderAccess.ReadWrite, ct);
            var ids = uids.Select(u => new UniqueId((uint)u)).ToList();
            await folder.AddFlagsAsync(ids, MessageFlags.Seen, true, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch { /* best-effort: no romper la corrida por el marcado */ }
    }

    private static IReadOnlyList<CorreoAdjuntoEntrante> ExtraerAdjuntos(MimeKit.MimeMessage msg)
    {
        var res = new List<CorreoAdjuntoEntrante>();
        foreach (var att in msg.Attachments)
        {
            if (res.Count >= MaxAdjuntos) { break; }
            if (att is not MimeKit.MimePart part) { continue; }
            try
            {
                using var ms = new MemoryStream();
                part.Content.DecodeTo(ms);
                var bytes = ms.ToArray();
                if (bytes.LongLength is 0 or > MaxAdjuntoBytes) { continue; }
                var nombre = part.FileName ?? $"adjunto-{res.Count + 1}";
                res.Add(new CorreoAdjuntoEntrante(nombre, bytes, part.ContentType?.MimeType));
            }
            catch { /* adjunto ilegible: se omite sin romper */ }
        }
        return res;
    }

    /// <summary>HTML -> texto plano (quita script/style, tags, decodifica entidades). Calca HtmlToText de VISAL.</summary>
    private static string HtmlToText(string html)
    {
        var s = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        s = Regex.Replace(s, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "</(p|div|tr|li|h[1-6])>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", " ");
        s = System.Net.WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, "[ \\t]+", " ");
        s = Regex.Replace(s, "\\n{3,}", "\n\n");
        return s.Trim();
    }
}
