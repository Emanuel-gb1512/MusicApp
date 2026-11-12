using Microsoft.Data.SqlClient;
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
/// Registro e inicio de sesión con correo y contraseña.
/// Nunca se registran en el log correos ni contraseñas, solo identificadores.
/// </summary>
public sealed class AuthService(
    AppDbContext db,
    IPasswordService passwords,
    IJwtTokenService jwt,
    ISecureTokenService tokens,
    JwtOptions jwtOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request,
        ClientInfo client,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return AuthResult.Failure(AuthError.EmailAlreadyRegistered);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var displayName = request.DisplayName.Trim();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            EmailConfirmed = false,
            Status = UserStatus.Active,
            LastLoginAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = displayName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Users.Add(user);

        var session = await PrepareSessionAsync(user, request.Device, client, now, cancellationToken);

        try
        {
            // Una sola transacción: se guarda todo o nada.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Otro registro con el mismo correo llegó al mismo tiempo.
            return AuthResult.Failure(AuthError.EmailAlreadyRegistered);
        }

        logger.LogInformation("Usuario registrado {UserId}", user.Id);

        return AuthResult.Success(BuildResponse(user, displayName, session));
    }

    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        ClientInfo client,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var now = clock.GetUtcNow().UtcDateTime;

        var user = await db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.Status == UserStatus.Deleted || user.PasswordHash is null)
        {
            // Se calcula un hash igualmente para que el tiempo de respuesta no delate
            // si el correo existe o no.
            _ = passwords.Hash(request.Password);
            return AuthResult.Failure(AuthError.InvalidCredentials);
        }

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > now)
        {
            return AuthResult.Failure(AuthError.AccountLocked);
        }

        var outcome = passwords.Verify(user.PasswordHash, request.Password);

        if (outcome == PasswordVerificationOutcome.Failed)
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndUtc = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                logger.LogWarning("Cuenta bloqueada por intentos fallidos {UserId}", user.Id);
            }

            user.UpdatedAtUtc = now;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otro intento simultáneo ya actualizó al usuario; no hace falta reintentar.
            }

            return AuthResult.Failure(AuthError.InvalidCredentials);
        }

        if (user.Status == UserStatus.Suspended)
        {
            return AuthResult.Failure(AuthError.AccountSuspended);
        }

        if (outcome == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.PasswordHash = passwords.Hash(request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;

        var session = await PrepareSessionAsync(user, request.Device, client, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inicio de sesión de {UserId}", user.Id);

        return AuthResult.Success(BuildResponse(user, user.Profile?.DisplayName ?? user.Email, session));
    }

    /// <summary>
    /// Añade al contexto el dispositivo (nuevo o ya conocido), la sesión y el refresh token.
    /// No guarda: quien llama decide cuándo hacer SaveChanges.
    /// </summary>
    private async Task<PendingSession> PrepareSessionAsync(
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

        return new PendingSession(session.Id, device.Id, refresh.Token, session.ExpiresAtUtc);
    }

    private AuthResponse BuildResponse(User user, string displayName, PendingSession session)
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

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

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

    private sealed record PendingSession(
        Guid SessionId,
        Guid DeviceId,
        string RefreshToken,
        DateTime RefreshTokenExpiresAtUtc);
}