using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Messaging;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>
/// Sustituye al envío real de correo: guarda los mensajes en memoria para que las pruebas
/// puedan leer los códigos de verificación y de recuperación.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public int CountFor(string to) => _messages.Count(m => IsTo(m, to));

    /// <summary>Espera (hasta 10 segundos) a que llegue un correo nuevo para esa dirección.</summary>
    public async Task<EmailMessage> WaitForNextAsync(string to, int alreadySeen, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            var found = _messages.Where(m => IsTo(m, to)).Skip(alreadySeen).FirstOrDefault();
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No llegó ningún correo nuevo para {to}.");
    }

    /// <summary>Los códigos son tokens de 43 caracteres (Base64Url).</summary>
    public static string ExtractCode(EmailMessage message)
    {
        var match = Regex.Match(message.TextBody, "[A-Za-z0-9_-]{43}");

        return match.Success
            ? match.Value
            : throw new InvalidOperationException("El correo no contiene un código.");
    }

    private static bool IsTo(EmailMessage message, string to) =>
        string.Equals(message.To, to, StringComparison.OrdinalIgnoreCase);
}