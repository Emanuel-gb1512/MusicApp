using Microsoft.Extensions.Logging;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Messaging;

namespace MusicApi.Infrastructure.Messaging;

/// <summary>
/// Solo para desarrollo: escribe el correo completo (con el código) en el log.
/// Está bloqueado fuera de Development por EmailOptions.Validate.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CORREO (modo desarrollo, no se envía)\nPara: {To}\nAsunto: {Subject}\n\n{Body}",
            message.To,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Descarta los correos. No registra el cuerpo, así que es seguro en cualquier entorno.
/// </summary>
public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "El envío de correo está desactivado (Email:Provider = Disabled). Se descartó un correo con asunto {Subject}",
            message.Subject);

        return Task.CompletedTask;
    }
}