using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Auth;

/// <summary>Sesión recién preparada: sus identificadores y el refresh token en claro (solo para entregarlo).</summary>
public sealed record IssuedSession(
    Guid SessionId,
    Guid DeviceId,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

/// <summary>
/// Crea el dispositivo (nuevo o ya conocido), la sesión y el refresh token de un inicio de sesión.
/// Lo usan los inicios de sesión externos (Google, Apple).
/// </summary>
public sealed class SessionIssuer(
    AppDbContext db,
    IJwtTokenService jwt,
    ISecureTokenService tokens,
    JwtOptions jwtOptions)
{
    /// <summary>
    /// Añade al contexto el dispositivo, la sesión y el refresh token. No guarda:
    /// quien llama decide cuándo hacer SaveChanges (así todo ocurre en una sola transacción).
    /// </summary>
    public async Task<IssuedSession> PrepareAsync(
        User user,
        DeviceInfo info,
        ClientInfo client,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var identifier = info.DeviceIdentifier.Trim();

        var device = await db.UserDevices.FirstOrDefaultAsync(
            d => d.UserId == user.Id && d.DeviceIdentifier == identifier,
            cancellationToken);

        if (device is null)
        {
            device = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                DeviceIdentifier = identifier,
                DeviceName = info.DeviceName.Trim(),
                Platform = info.Platform,
                AppVersion = info.AppVersion?.Trim(),
                LastSeenAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.UserDevices.Add(device);
        }
        else
        {
            device.DeviceName = info.DeviceName.Trim();
            device.Platform = info.Platform;
            device.AppVersion = info.AppVersion?.Trim();
            device.LastSeenAtUtc = now;
            device.UpdatedAtUtc = now;
        }

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = device.Id,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            ExpiresAtUtc = now.AddDays(jwtOptions.RefreshTokenDays),
            IpAddress = Truncate(client.IpAddress, 45),
            UserAgent = Truncate(client.UserAgent, 300)
        };

        db.UserSessions.Add(session);

        var refresh = tokens.Generate();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = session.ExpiresAtUtc
        });

        return new IssuedSession(session.Id, device.Id, refresh.Token, session.ExpiresAtUtc);
    }

    /// <summary>Crea el access token y arma la respuesta que recibe la app.</summary>
    public AuthResponse BuildResponse(User user, string displayName, IssuedSession session)
    {
        var access = jwt.CreateAccessToken(user.Id, session.SessionId, user.Email);

        return new AuthResponse(
            AccessToken: access.Token,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc,
            RefreshToken: session.RefreshToken,
            RefreshTokenExpiresAtUtc: session.RefreshTokenExpiresAtUtc,
            SessionId: session.SessionId,
            DeviceId: session.DeviceId,
            User: new UserSummary(user.Id, user.Email, displayName, user.EmailConfirmed));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}