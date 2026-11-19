using System.Net;
using MusicApi.Application.Music;
using MusicApi.Application.Providers;
using MusicApi.Domain.Enums;
using MusicApi.IntegrationTests.Infrastructure;
using Xunit;

namespace MusicApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public class MusicTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<string> TokenAsync() =>
        (await Client.RegisterOkAsync(ApiClient.NewEmail())).AccessToken;

    private async Task<HttpResponseMessage> GetAsync(string url) =>
        await Client.SendAsync(HttpMethod.Get, url, await TokenAsync());

    [Fact]
    public async Task Search_requires_authentication()
    {
        var response = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=rock");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_validates_the_query()
    {
        var token = await TokenAsync();

        foreach (var url in new[]
                 {
                     "/api/music/search",
                     "/api/music/search?q=",
                     $"/api/music/search?q={new string('a', 101)}",
                     "/api/music/search?q=rock&limit=0",
                     "/api/music/search?q=rock&limit=51"
                 })
        {
            var response = await Client.SendAsync(HttpMethod.Get, url, token);

            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{url} dio {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Search_in_one_provider_returns_its_results()
    {
        var response = await GetAsync("/api/music/search?q=rock&provider=jamendo&limit=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiClient.ReadAsync<MusicSearchResponse>(response);
        Assert.Equal("rock", body.Query);
        Assert.Empty(body.Errors);

        var result = Assert.Single(body.Results);
        Assert.Equal(ExternalProvider.Jamendo, result.Provider);

        var track = Assert.Single(result.Tracks);
        Assert.Equal("Cancion de rock", track.Title);
        Assert.True(track.DownloadAllowed);
        Assert.NotNull(track.StreamUrl);
    }

    [Fact]
    public async Task Search_without_a_provider_uses_every_ready_adapter()
    {
        var response = await GetAsync("/api/music/search?q=jazz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiClient.ReadAsync<MusicSearchResponse>(response);
        Assert.Empty(body.Errors);
        Assert.Equal(ExternalProvider.Jamendo, Assert.Single(body.Results).Provider);
    }

    [Fact]
    public async Task A_failing_provider_does_not_break_a_search_across_providers()
    {
        var token = await TokenAsync();

        // En una búsqueda general, el fallo se informa pero la respuesta es correcta.
        var general = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=boom", token);
        Assert.Equal(HttpStatusCode.OK, general.StatusCode);
        var body = await ApiClient.ReadAsync<MusicSearchResponse>(general);
        Assert.Empty(body.Results);
        var error = Assert.Single(body.Errors);
        Assert.Equal(ExternalProvider.Jamendo, error.Provider);
        Assert.Equal("provider_unavailable", error.Code);

        // Pidiendo ese proveedor concreto, el fallo es un error de la respuesta.
        var specific = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=boom&provider=jamendo", token);
        await ApiClient.AssertErrorAsync(specific, HttpStatusCode.BadGateway, "provider_unavailable");
    }

    [Fact]
    public async Task Search_rejects_providers_that_cannot_be_used()
    {
        var token = await TokenAsync();

        var unknown = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=rock&provider=no-existe", token);
        await ApiClient.AssertErrorAsync(unknown, HttpStatusCode.NotFound, "provider_not_found");

        // Spotify puede buscar, pero todavía no tiene adaptador.
        var spotify = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=rock&provider=spotify", token);
        await ApiClient.AssertErrorAsync(spotify, HttpStatusCode.ServiceUnavailable, "provider_not_configured");

        // YouTube Music no tiene API de búsqueda, y Google solo sirve para iniciar sesión.
        var youtubeMusic = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=rock&provider=youtube_music", token);
        await ApiClient.AssertErrorAsync(youtubeMusic, HttpStatusCode.BadRequest, "provider_not_supported");

        var google = await Client.SendAsync(HttpMethod.Get, "/api/music/search?q=rock&provider=google", token);
        await ApiClient.AssertErrorAsync(google, HttpStatusCode.BadRequest, "provider_not_supported");
    }

    [Fact]
    public async Task Track_detail_returns_the_data_needed_to_play_it()
    {
        var response = await GetAsync("/api/music/tracks/jamendo/12345");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var track = await ApiClient.ReadAsync<ProviderTrack>(response);
        Assert.Equal("12345", track.ExternalId);
        Assert.Equal(ExternalProvider.Jamendo, track.Provider);
        Assert.NotNull(track.StreamUrl);
        Assert.NotNull(track.License);
    }

    [Fact]
    public async Task Track_detail_handles_missing_failing_and_invalid_requests()
    {
        var token = await TokenAsync();

        var missing = await Client.SendAsync(HttpMethod.Get, "/api/music/tracks/jamendo/404", token);
        await ApiClient.AssertErrorAsync(missing, HttpStatusCode.NotFound, "track_not_found");

        var failing = await Client.SendAsync(HttpMethod.Get, "/api/music/tracks/jamendo/boom", token);
        await ApiClient.AssertErrorAsync(failing, HttpStatusCode.BadGateway, "provider_unavailable");

        var unknown = await Client.SendAsync(HttpMethod.Get, "/api/music/tracks/no-existe/1", token);
        await ApiClient.AssertErrorAsync(unknown, HttpStatusCode.NotFound, "provider_not_found");

        var invalid = await Client.SendAsync(HttpMethod.Get, "/api/music/tracks/jamendo/a%20b", token);
        await ApiClient.AssertErrorAsync(invalid, HttpStatusCode.BadRequest, "invalid_external_id");

        var notConfigured = await Client.SendAsync(HttpMethod.Get, "/api/music/tracks/spotify/1", token);
        await ApiClient.AssertErrorAsync(notConfigured, HttpStatusCode.ServiceUnavailable, "provider_not_configured");
    }
}