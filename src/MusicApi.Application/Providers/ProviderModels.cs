using MusicApi.Domain.Enums;

namespace MusicApi.Application.Providers;

/// <summary>
/// Lo que un proveedor permite OFICIALMENTE. Los valores salen de la tabla ConnectedProviders:
/// si un servicio cambia sus reglas, se corrige un dato, no el código.
/// </summary>
public sealed record ProviderCapabilities(
    bool Search,
    bool Library,
    bool Playlists,
    bool PlaybackControl,
    bool Downloads,
    bool OpenInApp);

/// <summary>Un proveedor tal como lo ve la app: sus capacidades y si está listo para usarse.</summary>
public sealed record ProviderInfo(
    ExternalProvider Id,
    string Code,
    string DisplayName,
    ProviderKind Kind,
    bool RequiresOAuth,
    ProviderCapabilities Capabilities,
    bool IsEnabled,
    bool AdapterAvailable,
    bool IsConfigured);

/// <summary>Una canción tal como la describe un proveedor externo.</summary>
public sealed record ProviderTrack(
    ExternalProvider Provider,
    string ExternalId,
    string Title,
    IReadOnlyList<string> Artists,
    string? Album,
    int? DurationMs,
    string? CoverUrl,
    string? ExternalUrl,
    bool DownloadAllowed,
    string? License);

/// <summary>Un álbum tal como lo describe un proveedor externo.</summary>
public sealed record ProviderAlbum(
    ExternalProvider Provider,
    string ExternalId,
    string Title,
    IReadOnlyList<string> Artists,
    string? CoverUrl,
    DateOnly? ReleaseDate,
    string? ExternalUrl);

/// <summary>Un artista tal como lo describe un proveedor externo.</summary>
public sealed record ProviderArtist(
    ExternalProvider Provider,
    string ExternalId,
    string Name,
    string? ImageUrl,
    string? ExternalUrl);

/// <summary>Resultado de una búsqueda en un proveedor.</summary>
public sealed record ProviderSearchResult(
    ExternalProvider Provider,
    IReadOnlyList<ProviderTrack> Tracks,
    IReadOnlyList<ProviderAlbum> Albums,
    IReadOnlyList<ProviderArtist> Artists)
{
    public static ProviderSearchResult Empty(ExternalProvider provider) => new(provider, [], [], []);
}