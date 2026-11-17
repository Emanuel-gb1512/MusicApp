using System.Net;
using MusicApi.Application.Messaging;

namespace MusicApi.Infrastructure.Messaging;

/// <summary>
/// Textos de los correos de TONYX, en versión de texto plano y HTML.
/// Los nombres de usuario se codifican en el HTML para evitar inyección de etiquetas.
/// </summary>
public static class EmailTemplates
{
    public static EmailMessage VerifyEmail(string to, string displayName, string token) =>
        Build(
            to,
            "Verifica tu correo en TONYX",
            displayName,
            "Este es tu código para verificar tu correo en TONYX:",
            token,
            "Pégalo en la app. Caduca en 24 horas. Si no creaste una cuenta en TONYX, ignora este mensaje.");

    public static EmailMessage PasswordReset(string to, string displayName, string token) =>
        Build(
            to,
            "Recupera tu contraseña de TONYX",
            displayName,
            "Este es tu código para crear una contraseña nueva en TONYX:",
            token,
            "Pégalo en la app. Caduca en 60 minutos. Si no lo pediste, ignora este mensaje: tu contraseña no cambia.");

    private static EmailMessage Build(
        string to,
        string subject,
        string displayName,
        string intro,
        string token,
        string outro)
    {
        var text =
            $"Hola {displayName},\n\n" +
            $"{intro}\n\n" +
            $"{token}\n\n" +
            $"{outro}\n";

        var html =
            $"<p>Hola {WebUtility.HtmlEncode(displayName)},</p>" +
            $"<p>{WebUtility.HtmlEncode(intro)}</p>" +
            $"<p style=\"font-family:Consolas,monospace;font-size:18px;word-break:break-all;\">{WebUtility.HtmlEncode(token)}</p>" +
            $"<p>{WebUtility.HtmlEncode(outro)}</p>";

        return new EmailMessage(to, subject, text, html);
    }
}