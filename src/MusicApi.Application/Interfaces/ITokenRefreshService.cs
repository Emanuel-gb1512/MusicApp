using MusicApi.Application.Auth;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Cambia un refresh token por un par nuevo de tokens (rotación).
/// </summary>
public interface ITokenRefreshService
{
    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}