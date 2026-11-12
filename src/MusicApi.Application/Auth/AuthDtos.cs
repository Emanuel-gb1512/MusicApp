using MusicApi.Domain.Enums;

namespace MusicApi.Application.Auth;

/// <summary>Datos del dispositivo desde el que se inicia sesión.</summary>
public sealed record DeviceInfo(
    string DeviceIdentifier,
    string DeviceName,
    DevicePlatform Platform,
    string? AppVersion);

/// <summary>Solicitud de registro con correo y contraseña.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    DeviceInfo Device);

/// <summary>Solicitud de inicio de sesión con correo y contraseña.</summary>
public sealed record LoginRequest(
    string Email,
    string Password,
    DeviceInfo Device);

/// <summary>Datos públicos básicos del usuario.</summary>
public sealed record UserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed);

/// <summary>Respuesta de registro e inicio de sesión.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid SessionId,
    Guid DeviceId,
    UserSummary User);

/// <summary>Datos de la conexión del cliente (se guardan en la sesión).</summary>
public sealed record ClientInfo(string? IpAddress, string? UserAgent);

/// <summary>Motivos por los que el registro o el login pueden fallar.</summary>
public enum AuthError
{
    None = 0,
    EmailAlreadyRegistered,
    InvalidCredentials,
    AccountLocked,
    AccountSuspended
}

/// <summary>Resultado de una operación de autenticación: éxito con datos, o un error.</summary>
public sealed record AuthResult(AuthResponse? Response, AuthError Error)
{
    public bool Succeeded => Error == AuthError.None;

    public static AuthResult Success(AuthResponse response) => new(response, AuthError.None);

    public static AuthResult Failure(AuthError error) => new(null, error);
}