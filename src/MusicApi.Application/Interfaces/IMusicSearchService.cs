using MusicApi.Application.Music;
using MusicApi.Application.Providers;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Búsqueda y detalle de música a través de los proveedores. Respeta lo que cada proveedor
/// permite (según ConnectedProviders) y qué adaptadores existen y están configurados.
/// </summary>
public interface IMusicSearchService
{
    /// <summary>
    /// Busca en el proveedor indicado, o en todos los que puedan buscar y estén listos.
    /// Un proveedor que falla no tumba una búsqueda en varios: aparece en Errors.
    /// </summary>
    Task<MusicResult<MusicSearchResponse>> SearchAsync(
        string query,
        string? providerCode,
        int limit,
        CancellationToken cancellationToken = default);

    Task<MusicResult<ProviderTrack>> GetTrackAsync(
        string providerCode,
        string externalId,
        CancellationToken cancellationToken = default);
}