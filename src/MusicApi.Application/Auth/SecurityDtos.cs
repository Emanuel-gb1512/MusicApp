namespace MusicApi.Application.Auth;

/// <summary>Confirma el correo con el código recibido.</summary>
public sealed record VerifyEmailRequest(string Token);

/// <summary>Pide un código para recuperar la contraseña.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Cambia la contraseña con el código de recuperación.</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>Resultado de pedir el correo de verificación.</summary>
public enum SendVerificationOutcome
{
    Queued = 0,
    AlreadyVerified,
    TooSoon,
    UserNotFound
}