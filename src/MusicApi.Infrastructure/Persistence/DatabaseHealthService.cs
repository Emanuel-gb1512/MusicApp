using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Interfaces;

namespace MusicApi.Infrastructure.Persistence;

/// <summary>
/// Comprueba la conexión con SQL Server y lee unos pocos datos de prueba.
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
                return new DatabaseHealthResult(false, null, null, null, 0, 0);
            }

            var latest = await db.SchemaVersions
                .AsNoTracking()
                .OrderByDescending(x => x.ScriptNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var providerCount = await db.ConnectedProviders.AsNoTracking().CountAsync(cancellationToken);
            var genreCount = await db.Genres.AsNoTracking().CountAsync(cancellationToken);

            return new DatabaseHealthResult(
                Connected: true,
                DatabaseName: db.Database.GetDbConnection().Database,
                LatestScriptNumber: latest?.ScriptNumber,
                LatestScriptName: latest?.ScriptName,
                ProviderCount: providerCount,
                GenreCount: genreCount);
        }
        catch (Exception ex)
        {
            // Por ejemplo: faltan tablas porque no se ejecutaron todos los scripts.
            logger.LogError(ex, "Error al comprobar la base de datos.");
            return new DatabaseHealthResult(false, null, null, null, 0, 0);
        }
    }
}
