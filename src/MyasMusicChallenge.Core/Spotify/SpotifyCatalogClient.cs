using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyasMusicChallenge.Core.Songs;

namespace MyasMusicChallenge.Core.Spotify;

/// <summary>Loads Mýa's discography from the Spotify Web API (client-credentials flow).</summary>
public sealed class SpotifyCatalogClient
{
    public const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    public const string ApiBase = "https://api.spotify.com/v1/";

    private const int PageLimit = 10;
    private const int MaxPages = 60;

    private readonly HttpClient _httpClient;
    private readonly SpotifyOptions _options;

    public SpotifyCatalogClient(HttpClient httpClient, SpotifyOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<IReadOnlyList<Song>> GetDiscographyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Spotify is not configured. Add your Client ID and Client Secret to appsettings.json.");
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        var artistId = Song.IsValidSpotifyId(_options.ArtistId)
            ? _options.ArtistId
            : await FindArtistIdAsync(token, cancellationToken)
              ?? throw new InvalidOperationException("Could not find Mýa on Spotify.");

        var market = Uri.EscapeDataString(string.IsNullOrWhiteSpace(_options.Market) ? "US" : _options.Market);
        var albums = new List<(string Id, string Name, string Type, int? Year)>();
        await foreach (var album in PageAsync(
                           $"{ApiBase}artists/{artistId}/albums?include_groups=album,single&market={market}&limit={PageLimit}",
                           token,
                           cancellationToken))
        {
            var id = GetString(album, "id");
            if (!Song.IsValidSpotifyId(id))
            {
                continue;
            }

            albums.Add((id!, GetString(album, "name") ?? string.Empty, GetString(album, "album_type") ?? "album", ParseYear(GetString(album, "release_date"))));
        }

        // Albums first (oldest to newest) so a song is credited to its album rather than a later single.
        albums = albums
            .OrderBy(a => a.Type == "album" ? 0 : 1)
            .ThenBy(a => a.Year ?? int.MaxValue)
            .ToList();

        var songs = new List<Song>();
        foreach (var album in albums)
        {
            await foreach (var track in PageAsync(
                               $"{ApiBase}albums/{album.Id}/tracks?market={market}&limit={PageLimit}",
                               token,
                               cancellationToken))
            {
                var song = ToSong(track, artistId, album.Name, album.Year);
                if (song is not null)
                {
                    songs.Add(song);
                }
            }
        }

        return SongLibrary.Merge(songs, []);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]),
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Spotify login failed ({(int)response.StatusCode}). Check your Client ID and Client Secret.");
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return GetString(document.RootElement, "access_token")
               ?? throw new HttpRequestException("Spotify did not return an access token.");
    }

    public async Task<string?> FindArtistIdAsync(string token, CancellationToken cancellationToken = default)
    {
        using var document = await GetJsonAsync(
            $"{ApiBase}search?q={Uri.EscapeDataString("Mýa")}&type=artist&limit={PageLimit}",
            token,
            cancellationToken);

        if (!document.RootElement.TryGetProperty("artists", out var artists)
            || !artists.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // Search results are ordered by relevance; take the first exact name match.
        foreach (var artist in items.EnumerateArray())
        {
            var id = GetString(artist, "id");
            if (NormalizeName(GetString(artist, "name")) == "mya" && Song.IsValidSpotifyId(id))
            {
                return id;
            }
        }

        return null;
    }

    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(character) && CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static Song? ToSong(JsonElement track, string artistId, string album, int? year)
    {
        var id = GetString(track, "id");
        var title = GetString(track, "name");
        if (!Song.IsValidSpotifyId(id) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var others = new List<string>();
        var includesMya = false;
        if (track.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
        {
            foreach (var artist in artists.EnumerateArray())
            {
                var name = GetString(artist, "name");
                if (GetString(artist, "id") == artistId || NormalizeName(name) == "mya")
                {
                    includesMya = true;
                }
                else if (!string.IsNullOrWhiteSpace(name))
                {
                    others.Add(name);
                }
            }
        }

        if (!includesMya)
        {
            return null;
        }

        double? duration = track.TryGetProperty("duration_ms", out var ms) && ms.TryGetDouble(out var value)
            ? value / 1000.0
            : null;

        return new Song
        {
            Id = Song.CreateId(title),
            Title = title,
            Album = album,
            Year = year,
            Credit = others.Count == 0 ? null : "feat. " + JoinNames(others),
            SpotifyTrackId = id,
            DurationSeconds = duration,
        };
    }

    private static string JoinNames(IReadOnlyList<string> names) =>
        names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} & {names[^1]}";

    private async IAsyncEnumerable<JsonElement> PageAsync(
        string url,
        string token,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? next = url;
        for (var page = 0; next is not null && page < MaxPages; page++)
        {
            using var document = await GetJsonAsync(next, token, cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    yield return item.Clone();
                }
            }

            next = GetString(root, "next");
            if (next is not null && !next.StartsWith(ApiBase, StringComparison.Ordinal))
            {
                // Never send the access token anywhere other than the Spotify API.
                next = null;
            }
        }
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var hint = (int)response.StatusCode == 403
                ? " Spotify requires the developer app owner to have an active Premium subscription."
                : string.Empty;
            throw new HttpRequestException(
                $"Spotify request failed ({(int)response.StatusCode}).{hint} {Truncate(body, 200)}".Trim());
        }

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ParseYear(string? releaseDate) =>
        releaseDate is { Length: >= 4 } && int.TryParse(releaseDate.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            ? year
            : null;

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
