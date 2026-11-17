using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Interfaces;

namespace MusicApi.Infrastructure.Persistence;

/// <summary>
/// Comprueba la conexión con SQL Server y que los mapeos de EF coinciden con las tablas.
/// </summary>
public sealed class DatabaseHealthService(
    AppDbContext db,
    ILogger<DatabaseHealthService> logger) : IDatabaseHealthService
{
    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                logger.LogWarning("No se pudo conectar con la base de datos.");
                return Failed();
            }

            var latest = await db.SchemaVersions
                .AsNoTracking()
                .OrderByDescending(x => x.ScriptNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var providerCount = await db.ConnectedProviders.AsNoTracking().CountAsync(cancellationToken);
            var genreCount = await db.Genres.AsNoTracking().CountAsync(cancellationToken);
            var userCount = await db.Users.AsNoTracking().CountAsync(cancellationToken);

            // Lee una fila (o ninguna) de cada tabla de identidad. Si un mapeo no coincide
            // con una columna real, SQL Server lanza "Invalid column name" y queda en el log.
            await db.Users.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.UserProfiles.AsNoTracking().OrderBy(x => x.UserId).FirstOrDefaultAsync(cancellationToken);
            await db.UserDevices.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.UserSessions.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.RefreshTokens.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.EmailVerificationTokens.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.PasswordResetTokens.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            await db.ExternalAccounts.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

            return new DatabaseHealthResult(
                Connected: true,
                DatabaseName: db.Database.GetDbConnection().Database,
                LatestScriptNumber: latest?.ScriptNumber,
                LatestScriptName: latest?.ScriptName,
                ProviderCount: providerCount,
                GenreCount: genreCount,
                UserCount: userCount,
                IdentityTablesChecked: 8);
        }
        catch (Exception ex)
        {
            // Por ejemplo: faltan tablas o un mapeo no coincide con una columna.
            logger.LogError(ex, "Error al comprobar la base de datos.");
            return Failed();
        }
    }

    private static DatabaseHealthResult Failed() =>
        new(false, null, null, null, 0, 0, 0, 0);
}