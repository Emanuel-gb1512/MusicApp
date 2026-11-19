using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MusicApi.Application.Interfaces;
using MusicApi.Application.Providers;
using MusicApi.Application.Settings;
using MusicApi.Domain.Enums;

namespace MusicApi.Infrastructure.Providers.Jamendo;

/// <summary>
/// Adaptador de Jamendo (API v3). Solo usa lo que la API permite oficialmente.
/// - Streaming: el campo "audio" (formato según Jamendo:AudioFormat).
/// - Descarga: el campo "audiodownload", SOLO si el artista la permite (audiodownload_allowed).
/// - Licencia: la URL Creative Commons de cada pista.
/// Nunca se registran en el log las direcciones con el client_id.
/// </summary>
public sealed class JamendoProvider(
    HttpClient http,
    JamendoOptions options,
    ILogger<JamendoProvider> logger) : IMusicProvider
{
    private const string CoverSize = "300";
    private const int MaxSecondaryResults = 10;

    // Jamendo usa snake_case y a veces entrega los números como texto ("duration":"192").
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public ExternalProvider Id => ExternalProvider.Jamendo;

    public bool IsConfigured => options.IsConfigured;

    public async Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var secondaryLimit = Math.Min(limit, MaxSecondaryResults);

        // Sin type=single albumtrack, Jamendo solo devuelve pistas de álbum y no los sencillos.
        var tracksTask = GetAsync<JamendoTrack>(
            "tracks/",
            [
                ("search", query),
                ("type", "single albumtrack"),
                ("limit", Number(limit)),
                ("imagesize", CoverSize),
                ("audioformat", options.AudioFormat)
            ],
            cancellationToken);

        var albumsTask = GetAsync<JamendoAlbum>(
            "albums/",
            [("namesearch", query), ("limit", Number(secondaryLimit)), ("imagesize", CoverSize)],
            cancellationToken);

        var artistsTask = GetAsync<JamendoArtist>(
            "artists/",
            [("namesearch", query), ("limit", Number(secondaryLimit))],
            cancellationToken);

        await Task.WhenAll(tracksTask, albumsTask, artistsTask);

        return new ProviderSearchResult(
            Id,
            (await tracksTask).Select(ToTrack).OfType<ProviderTrack>().ToList(),
            (await albumsTask).Select(ToAlbum).OfType<ProviderAlbum>().ToList(),
            (await artistsTask).Select(ToArtist).OfType<ProviderArtist>().ToList());
    }

    public async Task<ProviderTrack?> GetTrackAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        // Los identificadores de Jamendo son números: cualquier otra cosa no existe.
        if (string.IsNullOrEmpty(externalId) || !externalId.All(char.IsAsciiDigit))
        {
            return null;
        }

        var rows = await GetAsync<JamendoTrack>(
            "tracks/",
            [
                ("id", externalId),
                ("type", "single albumtrack"),
                ("imagesize", CoverSize),
                ("audioformat", options.AudioFormat)
            ],
            cancellationToken);

        return rows.Select(ToTrack).OfType<ProviderTrack>().FirstOrDefault();
    }

    private async Task<List<T>> GetAsync<T>(
        string path,
        (string Key, string Value)[] query,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Jamendo no está configurado (falta Jamendo:ClientId).");
        }

        try
        {
            using var response = await http.GetAsync(BuildUrl(path, query), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Jamendo respondió con HTTP {Status} en {Path}", (int)response.StatusCode, path);
                throw new ProviderUnavailableException(Id, "Jamendo no está disponible en este momento.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var envelope = await JsonSerializer.DeserializeAsync<JamendoEnvelope<T>>(
                stream, JsonOptions, cancellationToken);

            if (envelope?.Headers is null
                || !string.Equals(envelope.Headers.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Jamendo devolvió un error en {Path} (código {Code})",
                    path,
                    envelope?.Headers?.Code);

                throw new ProviderUnavailableException(Id, "Jamendo devolvió un error.");
            }

            return envelope.Results ?? [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("No se pudo conectar con Jamendo en {Path}: {Reason}", path, ex.GetType().Name);
            throw new ProviderUnavailableException(Id, "No se pudo conectar con Jamendo.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Jamendo tardó demasiado en responder en {Path}", path);
            throw new ProviderUnavailableException(Id, "Jamendo tardó demasiado en responder.", ex);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Jamendo devolvió una respuesta ilegible en {Path}", path);
            throw new ProviderUnavailableException(Id, "Jamendo devolvió una respuesta ilegible.", ex);
        }
    }

    private string BuildUrl(string path, (string Key, string Value)[] query)
    {
        var parts = new List<string>
        {
            $"client_id={WebUtility.UrlEncode(options.ClientId.Trim())}",
            "format=json"
        };

        parts.AddRange(query.Select(p => $"{p.Key}={WebUtility.UrlEncode(p.Value)}"));

        return $"{path}?{string.Join('&', parts)}";
    }

    private ProviderTrack? ToTrack(JamendoTrack track)
    {
        if (string.IsNullOrWhiteSpace(track.Id) || string.IsNullOrWhiteSpace(track.Name))
        {
            return null;
        }

        // La descarga solo se ofrece si el artista la permite Y hay una dirección válida.
        var downloadUrl = track.AudiodownloadAllowed == true ? SafeUrl(track.Audiodownload) : null;

        IReadOnlyList<string> artists = string.IsNullOrWhiteSpace(track.ArtistName)
            ? Array.Empty<string>()
            : [track.ArtistName.Trim()];

        int? durationMs = track.Duration is > 0 ? track.Duration.Value * 1000 : null;

        return new ProviderTrack(
            Provider: Id,
            ExternalId: track.Id.Trim(),
            Title: track.Name.Trim(),
            Artists: artists,
            // En los sencillos, el álbum viene vacío.
            Album: string.IsNullOrWhiteSpace(track.AlbumName) ? null : track.AlbumName.Trim(),
            DurationMs: durationMs,
            CoverUrl: SafeUrl(track.Image) ?? SafeUrl(track.AlbumImage),
            ExternalUrl: SafeUrl(track.Shareurl),
            DownloadAllowed: downloadUrl is not null,
            License: SafeUrl(track.LicenseCcurl),
            StreamUrl: SafeUrl(track.Audio),
            DownloadUrl: downloadUrl);
    }

    private ProviderAlbum? ToAlbum(JamendoAlbum album)
    {
        if (string.IsNullOrWhiteSpace(album.Id) || string.IsNullOrWhiteSpace(album.Name))
        {
            return null;
        }

        IReadOnlyList<string> artists = string.IsNullOrWhiteSpace(album.ArtistName)
            ? Array.Empty<string>()
            : [album.ArtistName.Trim()];

        DateOnly? release = DateOnly.TryParse(
            album.Releasedate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

        return new ProviderAlbum(
            Id,
            album.Id.Trim(),
            album.Name.Trim(),
            artists,
            SafeUrl(album.Image),
            release,
            SafeUrl(album.Shareurl));
    }

    private ProviderArtist? ToArtist(JamendoArtist artist)
    {
        if (string.IsNullOrWhiteSpace(artist.Id) || string.IsNullOrWhiteSpace(artist.Name))
        {
            return null;
        }

        return new ProviderArtist(
            Id,
            artist.Id.Trim(),
            artist.Name.Trim(),
            SafeUrl(artist.Image),
            SafeUrl(artist.Shareurl));
    }

    /// <summary>Solo se aceptan direcciones http o https. Lo demás (javascript:, file:...) se descarta.</summary>
    private static string? SafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? text
            : null;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}

// Formas de las respuestas de Jamendo. Solo se leen los campos que usamos.
internal sealed record JamendoEnvelope<T>(JamendoHeaders? Headers, List<T>? Results);

internal sealed record JamendoHeaders(string? Status, int? Code);

internal sealed record JamendoTrack(
    string? Id,
    string? Name,
    int? Duration,
    string? ArtistName,
    string? AlbumName,
    string? Image,
    string? AlbumImage,
    string? Audio,
    string? Audiodownload,
    bool? AudiodownloadAllowed,
    string? LicenseCcurl,
    string? Shareurl);

internal sealed record JamendoAlbum(
    string? Id,
    string? Name,
    string? ArtistName,
    string? Image,
    string? Releasedate,
    string? Shareurl);

internal sealed record JamendoArtist(
    string? Id,
    string? Name,
    string? Image,
    string? Shareurl);