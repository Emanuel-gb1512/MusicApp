using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Interfaces;

namespace MusicApi.Infrastructure.Messaging;

/// <summary>
/// Servicio en segundo plano: toma los correos de la cola y los envía uno a uno.
/// Un fallo de envío se registra, pero nunca detiene el servicio.
/// </summary>
public sealed class EmailDispatchService(
    EmailQueue queue,
    IEmailSender sender,
    ILogger<EmailDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await sender.SendAsync(message, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Solo se registra el asunto: nunca el cuerpo, que contiene el código.
                    logger.LogError(ex, "No se pudo enviar un correo con asunto {Subject}", message.Subject);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal de la aplicación.
        }
    }
}