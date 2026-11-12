using MusicApi.Application.Auth;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Registro e inicio de sesión con correo y contraseña.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        ClientInfo client,
        CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(
        LoginRequest request,
        ClientInfo client,
        CancellationToken cancellationToken = default);
}