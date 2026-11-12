using System.Text;

namespace MusicApi.Application.Settings;

/// <summary>
/// Configuración de los tokens JWT. Se lee de la sección "Jwt" de la configuración.
/// SigningKey es un secreto: va en user-secrets, nunca en el código.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Duración del access token, en minutos.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Duración del refresh token, en días.</summary>
    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>Bytes de la clave de firma. Se usan igual al crear y al validar tokens.</summary>
    public byte[] GetSigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey);

    /// <summary>Lanza un error claro si la configuración no es utilizable.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Falta Jwt:Issuer en la configuración.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Falta Jwt:Audience en la configuración.");
        }

        if (GetSigningKeyBytes().Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey no está configurada o es demasiado corta (mínimo 32 caracteres). " +
                "Genérala y guárdala con 'dotnet user-secrets set'.");
        }

        if (AccessTokenMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes debe estar entre 1 y 1440.");
        }

        if (RefreshTokenDays is < 1 or > 365)
        {
            throw new InvalidOperationException("Jwt:RefreshTokenDays debe estar entre 1 y 365.");
        }
    }
}
