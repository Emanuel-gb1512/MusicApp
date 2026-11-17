using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Settings;
using MusicApi.Domain.Entities;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Auth;

/// <summary>
/// Rotación de refresh tokens: cada token sirve una sola vez. Si se presenta uno ya gastado,
/// se asume que fue robado y se cierra toda la sesión.
/// </summary>
public sealed class TokenRefreshService(
    AppDbContext db,
    IJwtTokenService jwt,
    ISecureTokenService tokens,
    JwtOptions jwtOptions,
    TimeProvider clock,
    ILogger<TokenRefreshService> logger) : ITokenRefreshService
{
    public async Task<RefreshResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = tokens.Hash(refreshToken.Trim());

        var current = await db.RefreshTokens
            .AsNoTracking()
            .Include(t => t.Session)
            .Include(t => t.User).ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (current is null)
        {
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);
        }

        // Token ya gastado: posible robo. Se cierra toda la sesión.
        if (current.UsedAtUtc is not null)
        {
            await SessionRevoker.RevokeAsync(db, current.SessionId, "refresh_token_reuse", now, cancellationToken);
            logger.LogWarning("Reutilización de refresh token detectada. Sesión {SessionId} cerrada", current.SessionId);
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);
        }

        if (current.RevokedAtUtc is not null
            || current.ExpiresAtUtc <= now
            || current.Session.RevokedAtUtc is not null
            || current.Session.ExpiresAtUtc <= now)
        {
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);
        }

        if (current.User.Status == UserStatus.Suspended)
        {
            return RefreshResult.Failure(RefreshError.AccountSuspended);
        }

        if (current.User.Status != UserStatus.Active)
        {
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);
        }

        var newExpiry = now.AddDays(jwtOptions.RefreshTokenDays);
        var next = tokens.Generate();
        var nextId = Guid.NewGuid();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = nextId,
            SessionId = current.SessionId,
            UserId = current.UserId,
            TokenHash = next.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = newExpiry
        });

        await db.SaveChangesAsync(cancellationToken);

        // Gasta el token anterior. La condición hace que solo UNA petición gane si dos
        // llegan a la vez con el mismo token.
        var consumed = await db.RefreshTokens
            .Where(t => t.Id == current.Id && t.UsedAtUtc == null && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.UsedAtUtc, (DateTime?)now)
                      .SetProperty(x => x.ReplacedByTokenId, (Guid?)nextId),
                cancellationToken);

        if (consumed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            await SessionRevoker.RevokeAsync(db, current.SessionId, "refresh_token_reuse", now, cancellationToken);
            logger.LogWarning("Uso simultáneo de un refresh token. Sesión {SessionId} cerrada", current.SessionId);
            return RefreshResult.Failure(RefreshError.InvalidRefreshToken);
        }

        // Sesión deslizante: cada renovación la extiende.
        await db.UserSessions
            .Where(s => s.Id == current.SessionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastActivityAtUtc, now)
                      .SetProperty(x => x.ExpiresAtUtc, newExpiry),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var access = jwt.CreateAccessToken(current.UserId, current.SessionId, current.User.Email);
        var displayName = current.User.Profile?.DisplayName ?? current.User.Email;

        return RefreshResult.Success(new AuthResponse(
            AccessToken: access.Token,
            AccessTokenExpiresAtUtc: access.ExpiresAtUtc,
            RefreshToken: next.Token,
            RefreshTokenExpiresAtUtc: newExpiry,
            SessionId: current.SessionId,
            DeviceId: current.Session.DeviceId,
            User: new UserSummary(current.UserId, current.User.Email, displayName, current.User.EmailConfirmed)));
    }
}