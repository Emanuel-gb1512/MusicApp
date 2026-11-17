using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Auth;
using MusicApi.Application.Interfaces;
using MusicApi.Domain.Entities;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Auth;

/// <summary>
/// Inicio de sesión con un proveedor externo (Google, Apple). La identidad es el identificador
/// estable del proveedor ("sub"), no el correo. Nunca se registran correos ni tokens, solo identificadores.
/// </summary>
public sealed partial class ExternalLoginService(
    AppDbContext db,
    SessionIssuer sessions,
    TimeProvider clock,
    ILogger<ExternalLoginService> logger) : IExternalLoginService
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailShape();

    public async Task<ExternalLoginResult> LoginAsync(
        ExternalProvider provider,
        ExternalIdentity identity,
        DeviceInfo device,
        ClientInfo client,
        CancellationToken cancellationToken = default)
    {
        // El proveedor debe garantizar que el correo es de quien inicia sesión.
        if (!identity.EmailVerified)
        {
            return ExternalLoginResult.Failure(ExternalLoginError.EmailNotVerified);
        }

        // Datos que la base de datos rechazaría con un error 500: se descartan antes como token inválido.
        if (string.IsNullOrWhiteSpace(identity.Subject)
            || identity.Subject.Length > 200
            || string.IsNullOrWhiteSpace(identity.Email)
            || identity.Email.Length > 256
            || !EmailShape().IsMatch(identity.Email.Trim()))
        {
            return ExternalLoginResult.Failure(ExternalLoginError.InvalidToken);
        }

        try
        {
            return await LoginCoreAsync(provider, identity, device, client, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Otra petición creó al mismo tiempo el usuario o el vínculo.
            // Se reintenta una vez, ya con el estado guardado.
            db.ChangeTracker.Clear();
            return await LoginCoreAsync(provider, identity, device, client, cancellationToken);
        }
    }

    private async Task<ExternalLoginResult> LoginCoreAsync(
        ExternalProvider provider,
        ExternalIdentity identity,
        DeviceInfo device,
        ClientInfo client,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var providerId = (short)provider;
        var email = identity.Email.Trim().ToLowerInvariant();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var account = await db.ExternalAccounts
            .Include(a => a.User).ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(
                a => a.ProviderId == providerId && a.ExternalUserId == identity.Subject,
                cancellationToken);

        User user;
        var isNewUser = false;

        if (account is not null)
        {
            user = account.User;
        }
        else
        {
            var existing = await db.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            if (existing is null)
            {
                user = CreateUser(identity, email, now);
                db.Users.Add(user);
                isNewUser = true;
            }
            else
            {
                user = existing;
            }
        }

        if (user.Status == UserStatus.Suspended)
        {
            return ExternalLoginResult.Failure(ExternalLoginError.AccountSuspended);
        }

        if (user.Status == UserStatus.Deleted)
        {
            return ExternalLoginResult.Failure(ExternalLoginError.AccountUnavailable);
        }

        // Una cuenta existente SIN verificar es reclamada por quien demuestra, con el proveedor,
        // ser dueño del correo. Se elimina la contraseña y se cierran sus sesiones: así quien la
        // creó antes con ese correo (un posible suplantador) pierde el acceso.
        if (account is null && !isNewUser && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            user.PasswordHash = null;

            await db.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now)
                          .SetProperty(x => x.RevokedReason, "account_claimed_by_provider"),
                    cancellationToken);

            await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, (DateTime?)now), cancellationToken);

            logger.LogWarning(
                "Cuenta sin verificar reclamada con {Provider}. Contraseña eliminada y sesiones cerradas para {UserId}",
                provider,
                user.Id);
        }

        var providerDisplayName = Truncate(identity.DisplayName?.Trim(), 150);

        if (account is null)
        {
            db.ExternalAccounts.Add(new ExternalAccount
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProviderId = providerId,
                ExternalUserId = identity.Subject,
                DisplayName = providerDisplayName,
                Status = ExternalAccountStatus.Linked,
                LinkedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            if (account.Status != ExternalAccountStatus.Linked)
            {
                // Se vuelve a vincular una cuenta que el usuario había desvinculado.
                account.Status = ExternalAccountStatus.Linked;
                account.LinkedAtUtc = now;
                account.UnlinkedAtUtc = null;
            }

            account.DisplayName = providerDisplayName;
            account.UpdatedAtUtc = now;
        }

        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        var session = await sessions.PrepareAsync(user, device, client, now, cancellationToken);

        // Una sola transacción: se guarda todo o nada.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Inicio de sesión con {Provider} de {UserId} (usuario nuevo: {IsNew})",
            provider,
            user.Id,
            isNewUser);

        var displayName = user.Profile?.DisplayName ?? user.Email;
        return ExternalLoginResult.Success(sessions.BuildResponse(user, displayName, session), isNewUser);
    }

    private static User CreateUser(ExternalIdentity identity, string email, DateTime now)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = null,
            EmailConfirmed = true,
            Status = UserStatus.Active,
            LastLoginAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var avatar = identity.PictureUrl?.Trim();

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = BuildDisplayName(identity.DisplayName, email),
            AvatarUrl = !string.IsNullOrWhiteSpace(avatar) && avatar.Length <= 500 ? avatar : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        return user;
    }

    private static string BuildDisplayName(string? providerName, string email)
    {
        var name = providerName?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            name = email.Split('@')[0];
        }

        return name.Length <= 100 ? name : name[..100];
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>2601 y 2627 son los errores de SQL Server por índice o clave únicos duplicados.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}