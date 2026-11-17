using MusicApi.Application.Auth;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Verificación de correo y recuperación de contraseña.
/// </summary>
public interface IAccountSecurityService
{
    Task<SendVerificationOutcome> SendVerificationEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Devuelve false si el código no existe, caducó o ya se usó.</summary>
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Si el correo existe, encola un código de recuperación. Nunca revela si existe.</summary>
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Devuelve false si el código no existe, caducó o ya se usó.</summary>
    Task<bool> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}