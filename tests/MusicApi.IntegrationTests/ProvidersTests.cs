using System.Net;
using MusicApi.Application.Providers;
using MusicApi.Domain.Enums;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ProvidersTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task The_provider_catalog_requires_authentication()
    {
        var response = await Client.SendAsync(HttpMethod.Get, "/api/providers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_catalog_lists_every_provider_with_its_official_limits()
    {
        var auth = await Client.RegisterOkAsync(ApiClient.NewEmail());

        var response = await Client.SendAsync(HttpMethod.Get, "/api/providers", auth.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var providers = await ApiClient.ReadAsync<List<ProviderInfo>>(response);
        Assert.Equal(7, providers.Count);

        // Regla legal: Spotify, YouTube y YouTube Music NUNCA permiten descargas en TONYX.
        foreach (var code in new[] { "spotify", "youtube", "youtube_music" })
        {
            Assert.False(providers.Single(p => p.Code == code).Capabilities.Downloads, code);
        }

        // YouTube no permite reproducción en segundo plano ni control de reproducción desde la API.
        Assert.False(providers.Single(p => p.Code == "youtube").Capabilities.PlaybackControl);

        // YouTube Music no tiene API pública: solo se puede abrir en su app oficial.
        var youtubeMusic = providers.Single(p => p.Code == "youtube_music");
        Assert.True(youtubeMusic.Capabilities.OpenInApp);
        Assert.False(youtubeMusic.Capabilities.Search);

        // Jamendo e Internet Archive son las fuentes que admiten descargas (según la licencia de cada pista).
        Assert.True(providers.Single(p => p.Code == "jamendo").Capabilities.Downloads);
        Assert.True(providers.Single(p => p.Code == "internet_archive").Capabilities.Downloads);

        // Google solo sirve para iniciar sesión.
        Assert.Equal(ProviderKind.Identity, providers.Single(p => p.Code == "google").Kind);

        // Todavía no hay ningún adaptador implementado.
        Assert.All(providers, p => Assert.False(p.AdapterAvailable));
    }

    [Fact]
    public async Task A_provider_can_be_fetched_by_code_without_caring_about_the_case()
    {
        var auth = await Client.RegisterOkAsync(ApiClient.NewEmail());

        var found = await Client.SendAsync(HttpMethod.Get, "/api/providers/JAMENDO", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var provider = await ApiClient.ReadAsync<ProviderInfo>(found);
        Assert.Equal("jamendo", provider.Code);

        var missing = await Client.SendAsync(HttpMethod.Get, "/api/providers/no-existe", auth.AccessToken);
        await ApiClient.AssertErrorAsync(missing, HttpStatusCode.NotFound, "provider_not_found");
    }
}