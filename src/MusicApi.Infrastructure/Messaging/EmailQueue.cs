using System.Threading.Channels;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Messaging;

namespace MusicApi.Infrastructure.Messaging;

/// <summary>
/// Cola en memoria (hasta 100 correos). Si la API se reinicia, los correos pendientes se pierden;
/// para códigos de un solo uso eso es aceptable, el usuario simplemente pide otro.
/// </summary>
public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(100) { SingleReader = true });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public bool Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}