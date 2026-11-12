namespace MusicApi.Application.Interfaces;

/// <summary>
/// Crea los access tokens (JWT) que la app envía en cada petición.
/// </summary>
public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(Guid userId, Guid sessionId, string email);
}

/// <summary>Un access token ya firmado y el momento en que caduca (UTC).</summary>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
