using Microsoft.EntityFrameworkCore;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Auth;

/// <summary>
/// Cierra una sesión y revoca todos sus refresh tokens.
/// Lo comparten el servicio de renovación y el de cuenta.
/// </summary>
internal static class SessionRevoker
{
    public static async Task RevokeAsync(
        AppDbContext db,
        Guid sessionId,
        string reason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await db.UserSessions
            .Where(s => s.Id == sessionId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now)
                      .SetProperty(x => x.RevokedReason, reason),
                cancellationToken);

        await db.RefreshTokens
            .Where(t => t.SessionId == sessionId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now),
                cancellationToken);
    }
}