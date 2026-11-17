using MusicApi.Application.Messaging;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Cola de correos pendientes. Encolar es inmediato: el envío ocurre en segundo plano,
/// así una petición no espera al servidor de correo ni se rompe si este falla.
/// </summary>
public interface IEmailQueue
{
    /// <summary>Devuelve false si la cola está llena y el correo se descartó.</summary>
    bool Enqueue(EmailMessage message);
}