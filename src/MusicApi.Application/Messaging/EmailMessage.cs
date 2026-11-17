namespace MusicApi.Application.Messaging;

/// <summary>Un correo listo para enviar. HtmlBody es opcional.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);