using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Auth;

/// <summary>
/// Datos de la cuenta y gestión de sesiones. Cada operación se limita al usuario indicado:
/// nadie puede ver ni cerrar sesiones de otro usuario.
/// </summary>
public sealed class AccountService(
    AppDbContext db,
    TimeProvider clock,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status != UserStatus.Deleted, cancellationToken);

        return user is null
            ? null
            : new UserSummary(user.Id, user.Email, user.Profile?.DisplayName ?? user.Email, user.EmailConfirmed);
    }

    public async Task<IReadOnlyList<SessionInfo>> GetSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var rows = await db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .OrderByDescending(s => s.LastActivityAtUtc)
            .Select(s => new
            {
                s.Id,
                s.Device.DeviceName,
                s.Device.Platform,
                s.CreatedAtUtc,
                s.LastActivityAtUtc,
                s.IpAddress
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new SessionInfo(
                r.Id, r.DeviceName, r.Platform, r.CreatedAtUtc, r.LastActivityAtUtc, r.IpAddress,
                IsCurrent: r.Id == currentSessionId))
            .ToList();
    }

    public async Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var active = await db.UserSessions.AnyAsync(
            s => s.Id == sessionId && s.UserId == userId && s.RevokedAtUtc == null,
            cancellationToken);

        if (!active)
        {
            return false;
        }

        await SessionRevoker.RevokeAsync(db, sessionId, reason, clock.GetUtcNow().UtcDateTime, cancellationToken);
        logger.LogInformation("Sesión {SessionId} cerrada ({Reason}) para {UserId}", sessionId, reason, userId);
        return true;
    }

    public async Task<int> RevokeOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var ids = await db.UserSessions
            .Where(s => s.UserId == userId && s.Id != currentSessionId && s.RevokedAtUtc == null)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await SessionRevoker.RevokeAsync(db, id, "logout_others", now, cancellationToken);
        }

        logger.LogInformation("{Count} sesiones cerradas para {UserId}", ids.Count, userId);
        return ids.Count;
    }

    public async Task<bool> IsSessionActiveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        return await db.UserSessions
            .AsNoTracking()
            .AnyAsync(
                s => s.Id == sessionId
                     && s.UserId == userId
                     && s.RevokedAtUtc == null
                     && s.ExpiresAtUtc > now
                     && s.User.Status == UserStatus.Active,
                cancellationToken);
    }
}