using Microsoft.AspNetCore.Identity;
using MusicApi.Application.Interfaces;

namespace MusicApi.Infrastructure.Security;

/// <summary>
/// Hash de contraseñas con el hasher de ASP.NET Core Identity (PBKDF2, sal aleatoria).
/// </summary>
public sealed class PasswordService : IPasswordService
{
    // El hasher de Identity pide un "usuario", pero no lo usa para calcular el hash.
    private static readonly object Subject = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _hasher.HashPassword(Subject, password);
    }

    public PasswordVerificationOutcome Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
        {
            return PasswordVerificationOutcome.Failed;
        }

        try
        {
            var result = _hasher.VerifyHashedPassword(Subject, passwordHash, password);

            return result switch
            {
                PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
                _ => PasswordVerificationOutcome.Failed
            };
        }
        catch (FormatException)
        {
            // Un hash dañado o con formato desconocido nunca debe romper el login:
            // se trata como una contraseña incorrecta.
            return PasswordVerificationOutcome.Failed;
        }
    }
}