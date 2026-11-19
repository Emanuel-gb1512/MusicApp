using Microsoft.Extensions.Logging;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Music;
using MusicApi.Application.Providers;
using MusicApi.Domain.Enums;

namespace MusicApi.Infrastructure.Providers;

/// <summary>
/// Búsqueda y detalle de música. Antes de llamar a un proveedor comprueba que exista, esté activo,
/// permita esa función (según ConnectedProviders) y tenga un adaptador configurado.
/// </summary>
public sealed class MusicSearchService(
    IProviderCatalogService catalog,
    IEnumerable<IMusicProvider> adapters,
    ILogger<MusicSearchService> logger) : IMusicSearchService
{
    public async Task<MusicResult<MusicSearchResponse>> SearchAsync(
        string query,
        string? providerCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var text = query.Trim();

        // Un proveedor concreto: cualquier problema se informa tal cual.
        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            var info = await catalog.GetProviderAsync(providerCode, cancellationToken);

            if (info is null)
            {
                return MusicResult<MusicSearchResponse>.Failure(MusicOutcome.ProviderNotFound);
            }

            if (!CanSearch(info))
            {
                return MusicResult<MusicSearchResponse>.Failure(MusicOutcome.ProviderNotSupported);
            }

            var adapter = FindReadyAdapter(info);

            if (adapter is null)
            {
                return MusicResult<MusicSearchResponse>.Failure(MusicOutcome.ProviderNotConfigured);
            }

            var single = await SearchOneAsync(info.Id, adapter, text, limit, cancellationToken);

            return single.Result is not null
                ? MusicResult<MusicSearchResponse>.Success(new MusicSearchResponse(text, [single.Result], []))
                : MusicResult<MusicSearchResponse>.Failure(MusicOutcome.ProviderUnavailable);
        }

        // Todos los proveedores listos, en paralelo. Los que no tienen adaptador o clave se omiten.
        var searchable = (await catalog.GetProvidersAsync(cancellationToken)).Where(CanSearch);

        var jobs = searchable
            .Select(info => (Info: info, Adapter: FindReadyAdapter(info)))
            .Where(x => x.Adapter is not null)
            .Select(x => SearchOneAsync(x.Info.Id, x.Adapter!, text, limit, cancellationToken))
            .ToList();

        var outcomes = await Task.WhenAll(jobs);

        var results = outcomes.Where(o => o.Result is not null).Select(o => o.Result!).ToList();
        var errors = outcomes.Where(o => o.Error is not null).Select(o => o.Error!).ToList();

        return MusicResult<MusicSearchResponse>.Success(new MusicSearchResponse(text, results, errors));
    }

    public async Task<MusicResult<ProviderTrack>> GetTrackAsync(
        string providerCode,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var info = await catalog.GetProviderAsync(providerCode, cancellationToken);

        if (info is null)
        {
            return MusicResult<ProviderTrack>.Failure(MusicOutcome.ProviderNotFound);
        }

        if (info.Kind != ProviderKind.Music || !info.IsEnabled)
        {
            return MusicResult<ProviderTrack>.Failure(MusicOutcome.ProviderNotSupported);
        }

        var adapter = FindReadyAdapter(info);

        if (adapter is null)
        {
            return MusicResult<ProviderTrack>.Failure(MusicOutcome.ProviderNotConfigured);
        }

        try
        {
            var track = await adapter.GetTrackAsync(externalId, cancellationToken);

            return track is null
                ? MusicResult<ProviderTrack>.Failure(MusicOutcome.TrackNotFound)
                : MusicResult<ProviderTrack>.Success(track);
        }
        catch (ProviderUnavailableException ex)
        {
            logger.LogWarning("El proveedor {Provider} no está disponible: {Reason}", ex.Provider, ex.Message);
            return MusicResult<ProviderTrack>.Failure(MusicOutcome.ProviderUnavailable);
        }
    }

    private static bool CanSearch(ProviderInfo info) =>
        info.Kind == ProviderKind.Music && info.IsEnabled && info.Capabilities.Search;

    private IMusicProvider? FindReadyAdapter(ProviderInfo info) =>
        adapters.FirstOrDefault(a => a.Id == info.Id && a.IsConfigured);

    private async Task<(ProviderSearchResult? Result, ProviderSearchError? Error)> SearchOneAsync(
        ExternalProvider provider,
        IMusicProvider adapter,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await adapter.SearchAsync(query, limit, cancellationToken), null);
        }
        catch (ProviderUnavailableException ex)
        {
            logger.LogWarning("El proveedor {Provider} no está disponible: {Reason}", provider, ex.Message);
            return (null, new ProviderSearchError(provider, "provider_unavailable"));
        }
    }
}