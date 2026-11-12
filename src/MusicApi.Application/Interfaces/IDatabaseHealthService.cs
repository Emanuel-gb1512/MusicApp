namespace MusicApi.Application.Interfaces;

/// <summary>
/// Comprueba que la API puede hablar con la base de datos.
/// </summary>
public interface IDatabaseHealthService
{
    Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de la comprobación de la base de datos.
/// </summary>
public sealed record DatabaseHealthResult(
    bool Connected,
    string? DatabaseName,
    int? LatestScriptNumber,
    string? LatestScriptName,
    int ProviderCount,
    int GenreCount);
