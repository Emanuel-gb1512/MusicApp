using Microsoft.EntityFrameworkCore;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Providers;
using MusicApi.Domain.Entities;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Persistence;

namespace MusicApi.Infrastructure.Providers;

/// <summary>
/// Combina lo que dice la base de datos (capacidades oficiales de cada servicio) con los adaptadores
/// registrados (cuáles existen y cuáles están configurados).
/// </summary>
public sealed class ProviderCatalogService(
    AppDbContext db,
    IEnumerable<IMusicProvider> adapters) : IProviderCatalogService
{
    public async Task<IReadOnlyList<ProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.ConnectedProviders
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToInfo).ToList();
    }

    public async Task<ProviderInfo?> GetProviderAsync(string code, CancellationToken cancellationToken = default)
    {
        // La base de datos no distingue mayúsculas, pero se normaliza igualmente.
        var normalized = code.Trim().ToLowerInvariant();

        var row = await db.ConnectedProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized, cancellationToken);

        return row is null ? null : ToInfo(row);
    }

    private ProviderInfo ToInfo(ConnectedProvider provider)
    {
        var adapter = adapters.FirstOrDefault(a => (short)a.Id == provider.Id);

        return new ProviderInfo(
            Id: (ExternalProvider)provider.Id,
            Code: provider.Code,
            DisplayName: provider.DisplayName,
            Kind: (ProviderKind)provider.Kind,
            RequiresOAuth: provider.RequiresOAuth,
            Capabilities: new ProviderCapabilities(
                provider.SupportsSearch,
                provider.SupportsLibrary,
                provider.SupportsPlaylists,
                provider.SupportsPlaybackControl,
                provider.SupportsDownloads,
                provider.SupportsOpenInApp),
            IsEnabled: provider.IsEnabled,
            AdapterAvailable: adapter is not null,
            IsConfigured: adapter?.IsConfigured ?? false);
    }
}