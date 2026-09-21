namespace Tronox.Application.Common;

/// <summary>Resultado del envio de un correo (sin exponer detalles sensibles).</summary>
public sealed record EmailSendResult(bool Ok, string? Error);

/// <summary>
/// Envio de correo transaccional de la plataforma. La implementacion lee la configuracion
/// SMTP global (cifrada) y nunca loggea credenciales. Si el correo no esta habilitado, devuelve Ok=false.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia a uno o varios destinatarios con un adjunto (RQ04 RF05 "Enviar por correo"). Calca el
    /// envio del legacy que adjunta el binario del documento. Los correos vacios se ignoran; si no
    /// queda ninguno valido devuelve Ok=false.
    /// </summary>
    Task<EmailSendResult> SendWithAttachmentAsync(
        IReadOnlyList<string> toEmails, string subject, string htmlBody,
        byte[] attachmentBytes, string attachmentFileName, string attachmentContentType,
        CancellationToken cancellationToken = default);
}
