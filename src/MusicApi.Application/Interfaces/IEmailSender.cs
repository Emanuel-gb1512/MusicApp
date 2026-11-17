using MusicApi.Application.Messaging;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Envía un correo por el medio configurado (consola en desarrollo, SMTP real, o ninguno).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}