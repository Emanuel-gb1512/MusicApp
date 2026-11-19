using MusicApi.Application.Providers;
using MusicApi.Domain.Enums;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Adaptador de un servicio de música. Cada servicio tiene el suyo y SOLO implementa lo que su
/// API permite oficialmente: nada de extraer audio, saltarse DRM ni hacer scraping.
/// Qué capacidades tiene cada proveedor lo dice la tabla ConnectedProviders.
/// </summary>
public interface IMusicProvider
{
    ExternalProvider Id { get; }

    /// <summary>False si faltan claves o datos de configuración (por ejemplo, un client_id).</summary>
    bool IsConfigured { get; }

    Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}