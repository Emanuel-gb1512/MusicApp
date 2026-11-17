using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Messaging;
using MusicApi.Domain.Entities;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Messaging;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Auth;

/// <summary>
/// Verificación de correo y recuperación de contraseña con códigos de un solo uso.
/// En la base solo se guarda el hash de cada código. Nunca se registran códigos ni contraseñas.
/// </summary>
public sealed class AccountSecurityService(
    AppDbContext db,
    ISecureTokenService tokens,
    IPasswordService passwords,
    IEmailQueue emails,
    TimeProvider clock,
    ILogger<AccountSecurityService> logger) : IAccountSecurityService
{
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    public async Task<SendVerificationOutcome> SendVerificationEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status == UserStatus.Active, cancellationToken);

        if (user is null)
        {
            return SendVerificationOutcome.UserNotFound;
        }

        if (user.EmailConfirmed)
        {
            return SendVerificationOutcome.AlreadyVerified;
        }

        var cutoff = now - ResendCooldown;
        var tooSoon = await db.EmailVerificationTokens
            .AnyAsync(t => t.UserId == userId && t.CreatedAtUtc > cutoff, cancellationToken);

        if (tooSoon)
        {
            return SendVerificationOutcome.TooSoon;
        }

        var generated = tokens.Generate();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Un código nuevo invalida los anteriores.
        await db.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, (DateTime?)now), cancellationToken);

        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = generated.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + VerificationLifetime
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Enqueue(EmailTemplates.VerifyEmail(user.Email, user.Profile?.DisplayName ?? user.Email, generated.Token));

        logger.LogInformation("Correo de verificación encolado para {UserId}", userId);
        return SendVerificationOutcome.Queued;
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = tokens.Hash(token.Trim());

        var record = await db.EmailVerificationTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (record is null || record.UsedAtUtc is not null || record.ExpiresAtUtc <= now)
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Gasta el código. La condición hace que solo UNA petición gane si llegan dos a la vez.
        var consumed = await db.EmailVerificationTokens
            .Where(t => t.Id == record.Id && t.UsedAtUtc == null && t.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, (DateTime?)now), cancellationToken);

        if (consumed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await db.Users
            .Where(u => u.Id == record.UserId && u.Status != UserStatus.Deleted)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.EmailConfirmed, true)
                      .SetProperty(u => u.UpdatedAtUtc, now),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Correo verificado para {UserId}", record.UserId);
        return true;
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var normalized = email.Trim().ToLowerInvariant();

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email == normalized && u.Status == UserStatus.Active, cancellationToken);

        if (user is null)
        {
            // No se revela que el correo no existe: la respuesta es la misma.
            return;
        }

        var cutoff = now - ResendCooldown;
        var tooSoon = await db.PasswordResetTokens
            .AnyAsync(t => t.UserId == user.Id && t.CreatedAtUtc > cutoff, cancellationToken);

        if (tooSoon)
        {
            return;
        }

        var generated = tokens.Generate();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Un código nuevo invalida los anteriores.
        await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, (DateTime?)now), cancellationToken);

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = generated.TokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + ResetLifetime
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        Enqueue(EmailTemplates.PasswordReset(user.Email, user.Profile?.DisplayName ?? user.Email, generated.Token));

        logger.LogInformation("Código de recuperación encolado para {UserId}", user.Id);
    }

    public async Task<bool> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = tokens.Hash(token.Trim());

        var record = await db.PasswordResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (record is null || record.UsedAtUtc is not null || record.ExpiresAtUtc <= now)
        {
            return false;
        }

        // El hash es costoso (a propósito), así que se calcula antes de abrir la transacción.
        var newHash = passwords.Hash(newPassword);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var consumed = await db.PasswordResetTokens
            .Where(t => t.Id == record.Id && t.UsedAtUtc == null && t.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, (DateTime?)now), cancellationToken);

        if (consumed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        // Quien usa el código demuestra que controla el correo, así que también queda verificado.
        var updated = await db.Users
            .Where(u => u.Id == record.UserId && u.Status == UserStatus.Active)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.PasswordHash, newHash)
                      .SetProperty(u => u.FailedLoginCount, 0)
                      .SetProperty(u => u.LockoutEndUtc, (DateTime?)null)
                      .SetProperty(u => u.EmailConfirmed, true)
                      .SetProperty(u => u.UpdatedAtUtc, now),
                cancellationToken);

        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        // Cualquier otro código pendiente queda inválido.
        await db.PasswordResetTokens
            .Where(t => t.UserId == record.UserId && t.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, (DateTime?)now), cancellationToken);

        // Se cierran TODAS las sesiones: si alguien más tenía acceso, lo pierde.
        await db.UserSessions
            .Where(s => s.UserId == record.UserId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now)
                      .SetProperty(x => x.RevokedReason, "password_reset"),
                cancellationToken);

        await db.RefreshTokens
            .Where(t => t.UserId == record.UserId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Contraseña restablecida para {UserId}", record.UserId);
        return true;
    }

    private void Enqueue(EmailMessage message)
    {
        if (!emails.Enqueue(message))
        {
            logger.LogWarning("La cola de correos está llena: se descartó un correo.");
        }
    }
}