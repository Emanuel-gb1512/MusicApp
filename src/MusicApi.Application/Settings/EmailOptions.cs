namespace MusicApi.Application.Settings;

/// <summary>
/// Configuración del envío de correo (sección "Email").
/// Provider: "Log" (solo Development), "Smtp" (envío real) o "Disabled" (descarta los correos).
/// SmtpPassword es un secreto: va en user-secrets, nunca en el código.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Disabled";
    public string FromName { get; set; } = "TONYX";
    public string FromAddress { get; set; } = "no-reply@tonyx.local";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;

    public bool IsProvider(string name) =>
        string.Equals(Provider, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lanza un error claro si la configuración no es utilizable.</summary>
    public void Validate(bool isDevelopment)
    {
        if (!IsProvider("Log") && !IsProvider("Smtp") && !IsProvider("Disabled"))
        {
            throw new InvalidOperationException(
                "Email:Provider debe ser 'Log', 'Smtp' o 'Disabled'.");
        }

        if (IsProvider("Log") && !isDevelopment)
        {
            throw new InvalidOperationException(
                "El proveedor de correo 'Log' escribe los códigos en los logs y solo se permite en " +
                "Development. Usa 'Smtp' o 'Disabled'.");
        }

        if (IsProvider("Smtp"))
        {
            if (string.IsNullOrWhiteSpace(SmtpHost)
                || string.IsNullOrWhiteSpace(FromAddress)
                || string.IsNullOrWhiteSpace(SmtpUsername)
                || string.IsNullOrWhiteSpace(SmtpPassword))
            {
                throw new InvalidOperationException(
                    "Con Email:Provider = 'Smtp' hacen falta Email:SmtpHost, Email:FromAddress, " +
                    "Email:SmtpUsername y Email:SmtpPassword.");
            }

            if (SmtpPort is < 1 or > 65535)
            {
                throw new InvalidOperationException("Email:SmtpPort debe estar entre 1 y 65535.");
            }
        }
    }
}