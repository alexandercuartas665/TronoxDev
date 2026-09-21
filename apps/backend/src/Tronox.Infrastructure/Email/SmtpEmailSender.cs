using System.Net;
using System.Net.Mail;
using Tronox.Application.Common;
using Tronox.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Infrastructure.Email;

/// <summary>
/// Envio de correo via SMTP usando la configuracion global (cifrada) del Super Admin.
/// Compatible con SendGrid (SMTP), Gmail, Mailgun, etc. No persiste ni loggea la clave.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly TronoxDbContext _db;
    private readonly ISecretProtector _secretProtector;

    public SmtpEmailSender(TronoxDbContext db, ISecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

    public Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => EnviarAsync([toEmail], subject, htmlBody, adjunto: null, cancellationToken);

    public Task<EmailSendResult> SendWithAttachmentAsync(
        IReadOnlyList<string> toEmails, string subject, string htmlBody,
        byte[] attachmentBytes, string attachmentFileName, string attachmentContentType,
        CancellationToken cancellationToken = default)
        => EnviarAsync(toEmails, subject, htmlBody,
            new Adjunto(attachmentBytes, attachmentFileName, attachmentContentType), cancellationToken);

    private sealed record Adjunto(byte[] Bytes, string FileName, string ContentType);

    private async Task<EmailSendResult> EnviarAsync(
        IReadOnlyList<string> toEmails, string subject, string htmlBody, Adjunto? adjunto, CancellationToken cancellationToken)
    {
        var destinos = (toEmails ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (destinos.Count == 0)
        {
            return new EmailSendResult(false, "Indica al menos un destinatario.");
        }

        var cfg = await _db.EmailConfigs.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (cfg is null || !cfg.IsEnabled)
        {
            return new EmailSendResult(false, "El correo saliente no esta configurado/habilitado en la plataforma.");
        }
        if (string.IsNullOrWhiteSpace(cfg.SmtpHost) || string.IsNullOrWhiteSpace(cfg.FromEmail))
        {
            return new EmailSendResult(false, "Falta configurar el host SMTP o la direccion remitente.");
        }

        string? password = null;
        if (!string.IsNullOrEmpty(cfg.SmtpPasswordEncrypted))
        {
            try { password = _secretProtector.Unprotect(cfg.SmtpPasswordEncrypted); }
            catch { return new EmailSendResult(false, "La clave SMTP esta cifrada con una version anterior. Vuelve a guardarla."); }
        }

        try
        {
            using var client = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort)
            {
                EnableSsl = cfg.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(cfg.SmtpUser))
            {
                client.Credentials = new NetworkCredential(cfg.SmtpUser, password ?? string.Empty);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(cfg.FromEmail, cfg.FromName ?? cfg.FromEmail),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            foreach (var d in destinos) { message.To.Add(new MailAddress(d)); }

            MemoryStream? ms = null;
            if (adjunto is not null)
            {
                ms = new MemoryStream(adjunto.Bytes, writable: false);
                var att = new Attachment(ms, adjunto.FileName,
                    string.IsNullOrWhiteSpace(adjunto.ContentType) ? "application/octet-stream" : adjunto.ContentType);
                message.Attachments.Add(att);
            }

            try
            {
                await client.SendMailAsync(message, cancellationToken);
            }
            finally
            {
                ms?.Dispose();
            }
            return new EmailSendResult(true, null);
        }
        catch (Exception ex)
        {
            // No exponer la clave; solo el tipo/mensaje del error de envio.
            return new EmailSendResult(false, $"No se pudo enviar el correo: {ex.Message}");
        }
    }
}
