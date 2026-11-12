using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;

namespace MusicApi.Infrastructure.Security;

/// <summary>
/// Crea access tokens JWT firmados con HMAC-SHA256.
/// Claims: sub (usuario), sid (sesión), email y jti (identificador único del token).
/// </summary>
public sealed class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(options.GetSigningKeyBytes()),
        SecurityAlgorithms.HmacSha256);

    public AccessTokenResult CreateAccessToken(Guid userId, Guid sessionId, string email)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Sid, sessionId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = _credentials
        };

        return new AccessTokenResult(_handler.CreateToken(descriptor), expires);
    }
}
