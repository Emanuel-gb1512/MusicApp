using MusicApi.Application.Providers;

namespace MusicApi.Application.Interfaces;

/// <summary>
/// Catálogo de proveedores: qué servicios conoce TONYX, qué permite cada uno
/// y cuáles tienen ya un adaptador listo para usarse.
/// </summary>
public interface IProviderCatalogService
{
    Task<IReadOnlyList<ProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default);

    Task<ProviderInfo?> GetProviderAsync(string code, CancellationToken cancellationToken = default);
}