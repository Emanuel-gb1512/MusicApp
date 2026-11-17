namespace MusicApi.Application.Auth;

/// <summary>Solicitud de inicio de sesión con Google: el ID token que entregó Google a la app.</summary>
public sealed record GoogleLoginRequest(string IdToken, DeviceInfo Device);

/// <summary>Identidad ya verificada por un proveedor externo.</summary>
public sealed record ExternalIdentity(
    string Subject,
    string Email,
    bool EmailVerified,
    string? DisplayName,
    string? PictureUrl);

/// <summary>Motivos por los que un inicio de sesión externo puede fallar.</summary>
public enum ExternalLoginError
{
    None = 0,
    InvalidToken,
    EmailNotVerified,
    AccountSuspended,
    AccountUnavailable
}

/// <summary>Resultado de un inicio de sesión externo: éxito con datos, o un error.</summary>
public sealed record ExternalLoginResult(AuthResponse? Response, ExternalLoginError Error, bool IsNewUser)
{
    public bool Succeeded => Error == ExternalLoginError.None;

    public static ExternalLoginResult Success(AuthResponse response, bool isNewUser) =>
        new(response, ExternalLoginError.None, isNewUser);

    public static ExternalLoginResult Failure(ExternalLoginError error) => new(null, error, false);
}