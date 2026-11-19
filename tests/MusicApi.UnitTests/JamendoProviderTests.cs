using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MusicApi.Application.Providers;
using MusicApi.Application.Settings;
using MusicApi.Domain.Enums;
using MusicApi.Infrastructure.Providers.Jamendo;
using Xunit;

namespace MusicApi.UnitTests;

public class JamendoProviderTests
{
    private const string TracksJson = """
        {"headers":{"status":"success","code":0,"error_message":"","warnings":"","results_count":2},"results":[
        {"id":"1848357","name":"Manana sera tarde","duration":272,"artist_name":"fankel","album_name":"Manana","image":"https://usercontent.jamendo.com?type=album&id=368084&width=300","album_image":"https://usercontent.jamendo.com?type=album&id=368084&width=300","audio":"https://prod-1.storage.jamendo.com/?trackid=1848357&format=mp32","audiodownload":"https://prod-1.storage.jamendo.com/download/track/1848357/mp32/","audiodownload_allowed":true,"license_ccurl":"http://creativecommons.org/licenses/by-nc-nd/3.0/","shareurl":"https://www.jamendo.com/track/1848357"},
        {"id":"1880336","name":"Queensland","duration":"147","artist_name":"Lofi Lopez","album_name":"","album_image":"","image":"https://usercontent.jamendo.com?type=album&id=452525&width=300","audio":"https://prod-1.storage.jamendo.com/?trackid=1880336&format=mp32","audiodownload":"","audiodownload_allowed":false,"license_ccurl":"http://creativecommons.org/licenses/by-nc-nd/3.0/","shareurl":"https://www.jamendo.com/track/1880336"}]}
        """;

    private const string AlbumsJson = """
        {"headers":{"status":"success","code":0,"results_count":1},"results":[
        {"id":"104336","name":"Season One","releasedate":"2011-12-29","artist_id":"376782","artist_name":"WE ARE FM","image":"https://usercontent.jamendo.com?type=album&id=104336&width=300","shareurl":"https://www.jamendo.com/list/a104336","zip_allowed":true}]}
        """;

    private const string ArtistsJson = """
        {"headers":{"status":"success","code":0,"results_count":1},"results":[
        {"id":"376782","name":"WE ARE FM","image":"https://usercontent.jamendo.com?type=artist&id=376782&width=300","shareurl":"https://www.jamendo.com/artist/376782"}]}
        """;

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public ConcurrentQueue<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Enqueue(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Answer(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        var path when path.EndsWith("/tracks/") => Json(TracksJson),
        var path when path.EndsWith("/albums/") => Json(AlbumsJson),
        var path when path.EndsWith("/artists/") => Json(ArtistsJson),
        _ => Json("{}", HttpStatusCode.NotFound)
    };

    private static (JamendoProvider Provider, StubHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string clientId = "cid-123")
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.jamendo.com/v3.0/") };
        var provider = new JamendoProvider(
            http,
            new JamendoOptions { ClientId = clientId },
            NullLogger<JamendoProvider>.Instance);

        return (provider, handler);
    }

    [Fact]
    public async Task Search_maps_tracks_albums_and_artists()
    {
        var (provider, _) = Create(Answer);

        var result = await provider.SearchAsync("rock", 20);

        Assert.Equal(ExternalProvider.Jamendo, result.Provider);
        Assert.Equal(2, result.Tracks.Count);

        var first = result.Tracks[0];
        Assert.Equal("1848357", first.ExternalId);
        Assert.Equal("Manana sera tarde", first.Title);
        Assert.Equal(new[] { "fankel" }, first.Artists);
        Assert.Equal("Manana", first.Album);
        Assert.Equal(272_000, first.DurationMs);
        Assert.True(first.DownloadAllowed);
        Assert.StartsWith("https://prod-1.storage.jamendo.com/download/", first.DownloadUrl);
        Assert.StartsWith("https://prod-1.storage.jamendo.com/?trackid=1848357", first.StreamUrl);
        Assert.Equal("http://creativecommons.org/licenses/by-nc-nd/3.0/", first.License);
        Assert.Equal("https://www.jamendo.com/track/1848357", first.ExternalUrl);

        var album = Assert.Single(result.Albums);
        Assert.Equal("Season One", album.Title);
        Assert.Equal(new DateOnly(2011, 12, 29), album.ReleaseDate);
        Assert.Equal(new[] { "WE ARE FM" }, album.Artists);

        Assert.Equal("WE ARE FM", Assert.Single(result.Artists).Name);
    }

    [Fact]
    public async Task Search_sends_the_documented_parameters()
    {
        var (provider, handler) = Create(Answer);

        await provider.SearchAsync("rock & roll", 20);

        Assert.Equal(3, handler.Requests.Count);

        var tracks = handler.Requests.Single(u => u.AbsolutePath.EndsWith("/tracks/"));
        Assert.Contains("client_id=cid-123", tracks.Query);
        Assert.Contains("format=json", tracks.Query);
        Assert.Contains("audioformat=mp32", tracks.Query);
        Assert.Contains("search=rock+%26+roll", tracks.Query);
        Assert.Contains("type=single+albumtrack", tracks.Query);
        Assert.Contains("limit=20", tracks.Query);

        var albums = handler.Requests.Single(u => u.AbsolutePath.EndsWith("/albums/"));
        Assert.Contains("namesearch=rock+%26+roll", albums.Query);
        Assert.Contains("limit=10", albums.Query);
    }

    [Fact]
    public async Task A_track_without_download_permission_has_no_download_url()
    {
        var (provider, _) = Create(Answer);

        var restricted = (await provider.SearchAsync("rock", 20)).Tracks[1];

        Assert.False(restricted.DownloadAllowed);
        Assert.Null(restricted.DownloadUrl);
        Assert.NotNull(restricted.StreamUrl);
    }

    [Fact]
    public async Task A_single_has_no_album_and_text_numbers_are_accepted()
    {
        var (provider, _) = Create(Answer);

        var single = (await provider.SearchAsync("rock", 20)).Tracks[1];

        Assert.Null(single.Album);
        Assert.Contains("id=452525", single.CoverUrl);
        Assert.Equal(147_000, single.DurationMs);
    }

    [Fact]
    public async Task Unsafe_urls_are_discarded()
    {
        const string body = """{"headers":{"status":"success","code":0},"results":[{"id":"1","name":"X","audio":"javascript:alert(1)","audiodownload":"file:///c:/secreto","audiodownload_allowed":true,"image":"ftp://servidor/portada.jpg"}]}""";
        var (provider, _) = Create(_ => Json(body));

        var track = Assert.Single((await provider.SearchAsync("x", 5)).Tracks);

        Assert.Null(track.StreamUrl);
        Assert.Null(track.DownloadUrl);
        Assert.False(track.DownloadAllowed);
        Assert.Null(track.CoverUrl);
    }

    [Fact]
    public async Task A_failure_reported_in_the_body_means_the_provider_is_unavailable()
    {
        const string body = """{"headers":{"status":"failed","code":5,"error_message":"invalid client_id"},"results":[]}""";
        var (provider, _) = Create(_ => Json(body));

        await Assert.ThrowsAsync<ProviderUnavailableException>(() => provider.SearchAsync("x", 5));
    }

    [Fact]
    public async Task An_http_error_means_the_provider_is_unavailable()
    {
        var (provider, _) = Create(_ => Json("{}", HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<ProviderUnavailableException>(() => provider.SearchAsync("x", 5));
    }

    [Fact]
    public async Task GetTrack_returns_null_when_nothing_matches()
    {
        const string body = """{"headers":{"status":"success","code":0},"results":[]}""";
        var (provider, _) = Create(_ => Json(body));

        Assert.Null(await provider.GetTrackAsync("123"));
    }

    [Fact]
    public async Task GetTrack_ignores_ids_that_are_not_numbers_without_calling_jamendo()
    {
        var (provider, handler) = Create(Answer);

        Assert.Null(await provider.GetTrackAsync("abc"));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Without_a_client_id_the_provider_is_not_configured()
    {
        var (provider, _) = Create(Answer, clientId: "  ");

        Assert.False(provider.IsConfigured);
    }
}

public class JamendoOptionsTests
{
    [Fact]
    public void Known_audio_formats_are_accepted()
    {
        foreach (var format in new[] { "mp31", "mp32", "ogg", "FLAC" })
        {
            var options = new JamendoOptions { AudioFormat = format };

            Assert.Null(Record.Exception(() => options.Validate()));
        }
    }

    [Fact]
    public void An_unknown_audio_format_is_rejected()
    {
        var options = new JamendoOptions { AudioFormat = "wav" };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }
}