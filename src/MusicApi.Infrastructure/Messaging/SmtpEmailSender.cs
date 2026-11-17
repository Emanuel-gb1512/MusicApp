using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Messaging;
using MusicApi.Application.Settings;

namespace MusicApi.Infrastructure.Messaging;

/// <summary>
/// Envío real por SMTP con MailKit. Puerto 465 usa TLS directo; los demás usan STARTTLS.
/// </summary>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        var security = options.SmtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();
        await client.ConnectAsync(options.SmtpHost, options.SmtpPort, security, cancellationToken);
        await client.AuthenticateAsync(options.SmtpUsername, options.SmtpPassword, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}