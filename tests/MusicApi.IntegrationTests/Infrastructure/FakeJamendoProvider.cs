using MusicApi.Application.Interfaces;
using MusicApi.Application.Providers;
using MusicApi.Domain.Enums;

namespace MusicApi.IntegrationTests.Infrastructure;

/// <summary>
/// Sustituye a Jamendo en las pruebas: sin internet ni client_id.
/// Buscar "boom" (o pedir la pista "boom") simula un proveedor caído; la pista "404" no existe.
/// </summary>
public sealed class FakeJamendoProvider : IMusicProvider
{
    public ExternalProvider Id => ExternalProvider.Jamendo;

    public bool IsConfigured => true;

    public Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (query.Contains("boom", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProviderUnavailableException(Id, "El proveedor simulado falló.");
        }

        return Task.FromResult(new ProviderSearchResult(Id, [CreateTrack("100", $"Cancion de {query}")], [], []));
    }

    public Task<ProviderTrack?> GetTrackAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        if (externalId == "boom")
        {
            throw new ProviderUnavailableException(Id, "El proveedor simulado falló.");
        }

        return Task.FromResult<ProviderTrack?>(
            externalId == "404" ? null : CreateTrack(externalId, $"Pista {externalId}"));
    }

    private ProviderTrack CreateTrack(string externalId, string title) => new(
        Provider: Id,
        ExternalId: externalId,
        Title: title,
        Artists: ["Artista de prueba"],
        Album: "Album de prueba",
        DurationMs: 180_000,
        CoverUrl: "https://example.com/portada.jpg",
        ExternalUrl: $"https://example.com/pista/{externalId}",
        DownloadAllowed: true,
        License: "http://creativecommons.org/licenses/by/3.0/",
        StreamUrl: $"https://example.com/stream/{externalId}",
        DownloadUrl: $"https://example.com/descarga/{externalId}");
}