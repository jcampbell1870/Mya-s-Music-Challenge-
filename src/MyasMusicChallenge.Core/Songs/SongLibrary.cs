using System.Text.Json;
using System.Text.Json.Serialization;
using MyasMusicChallenge.Core.Lyrics;

namespace MyasMusicChallenge.Core.Songs;

/// <summary>A reference melody note used for pitch scoring (times in seconds, pitch as a MIDI note number).</summary>
public sealed record ReferenceNote(
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("duration")] double Duration,
    [property: JsonPropertyName("midi")] double Midi)
{
    public double End => Start + Duration;
}

/// <summary>A playable song plus any karaoke assets the player has installed for it.</summary>
public sealed record SongEntry(
    Song Song,
    string? LyricsPath = null,
    string? BackingTrackPath = null,
    string? MelodyPath = null)
{
    public bool HasLyrics => LyricsPath is not null;

    public bool HasBackingTrack => BackingTrackPath is not null;

    public bool HasMelody => MelodyPath is not null;

    public bool HasSpotify => Song.SpotifyTrackId is not null;
}

/// <summary>
/// Builds the playable song list from the built-in catalog, Spotify, and the player's <c>Songs</c> folder.
/// </summary>
/// <remarks>
/// Folder layout: <c>Songs/&lt;song-id&gt;/</c> containing any of <c>lyrics.lrc</c>, <c>backing.mp3|wav|m4a|wma|aac</c>,
/// <c>melody.json</c> (array of <see cref="ReferenceNote"/>) and, for songs not already in the catalog,
/// <c>song.json</c> (<c>{"title": "...", "album": "...", "year": 2024, "credit": "feat. ..."}</c>).
/// </remarks>
public static class SongLibrary
{
    public const string LyricsFileName = "lyrics.lrc";
    public const string MelodyFileName = "melody.json";
    public const string SongInfoFileName = "song.json";

    public static readonly IReadOnlyList<string> BackingTrackExtensions = [".mp3", ".wav", ".m4a", ".wma", ".aac"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Merges catalogs; later sources fill in missing details (Spotify id, duration) of matching titles.</summary>
    public static IReadOnlyList<Song> Merge(IEnumerable<Song> primary, IEnumerable<Song> additional)
    {
        var merged = new List<Song>();
        var byId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var song in primary.Concat(additional))
        {
            if (string.IsNullOrWhiteSpace(song.Id))
            {
                continue;
            }

            if (byId.TryGetValue(song.Id, out var index))
            {
                var existing = merged[index];
                merged[index] = existing with
                {
                    SpotifyTrackId = existing.SpotifyTrackId ?? song.SpotifyTrackId,
                    DurationSeconds = existing.DurationSeconds ?? song.DurationSeconds,
                    Year = existing.Year ?? song.Year,
                    Album = string.IsNullOrWhiteSpace(existing.Album) ? song.Album : existing.Album,
                    Credit = existing.Credit ?? song.Credit,
                };
                continue;
            }

            byId[song.Id] = merged.Count;
            merged.Add(song);
        }

        return merged;
    }

    public static IReadOnlyList<SongEntry> Build(IEnumerable<Song> catalog, string? songsDirectory)
    {
        var songs = catalog.ToList();
        var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(songsDirectory) && Directory.Exists(songsDirectory))
        {
            foreach (var folder in Directory.EnumerateDirectories(songsDirectory))
            {
                var folderId = Song.CreateId(Path.GetFileName(folder));
                if (string.IsNullOrEmpty(folderId))
                {
                    continue;
                }

                folders[folderId] = folder;

                if (songs.Any(s => string.Equals(s.Id, folderId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var custom = TryLoadSongInfo(folder, folderId);
                if (custom is not null)
                {
                    songs.Add(custom);
                }
            }
        }

        return songs
            .Select(song => folders.TryGetValue(song.Id, out var folder) ? CreateEntry(song, folder) : new SongEntry(song))
            .OrderByDescending(entry => entry.HasLyrics || entry.HasBackingTrack)
            .ThenBy(entry => entry.Song.Year ?? int.MaxValue)
            .ThenBy(entry => entry.Song.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ReferenceNote> LoadMelody(string path)
    {
        var notes = JsonSerializer.Deserialize<List<ReferenceNote>>(File.ReadAllText(path), JsonOptions) ?? [];
        return notes
            .Where(n => n.Duration > 0 && n.Start >= 0 && n.Midi is > 0 and < 128)
            .OrderBy(n => n.Start)
            .ToList();
    }

    public static IReadOnlyList<LyricLine> LoadLyrics(SongEntry entry) =>
        entry.LyricsPath is null ? [] : LrcParser.Load(entry.LyricsPath);

    private static SongEntry CreateEntry(Song song, string folder)
    {
        var lyrics = Path.Combine(folder, LyricsFileName);
        var melody = Path.Combine(folder, MelodyFileName);
        var backing = BackingTrackExtensions
            .Select(ext => Path.Combine(folder, "backing" + ext))
            .FirstOrDefault(File.Exists);

        return new SongEntry(
            song,
            File.Exists(lyrics) ? lyrics : null,
            backing,
            File.Exists(melody) ? melody : null);
    }

    private static Song? TryLoadSongInfo(string folder, string folderId)
    {
        var path = Path.Combine(folder, SongInfoFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var info = JsonSerializer.Deserialize<SongInfo>(File.ReadAllText(path), JsonOptions);
            if (info is null || string.IsNullOrWhiteSpace(info.Title))
            {
                return null;
            }

            return new Song
            {
                Id = folderId,
                Title = info.Title.Trim(),
                Album = info.Album?.Trim() ?? string.Empty,
                Year = info.Year,
                Credit = string.IsNullOrWhiteSpace(info.Credit) ? null : info.Credit.Trim(),
                SpotifyTrackId = string.IsNullOrWhiteSpace(info.SpotifyTrackId) ? null : info.SpotifyTrackId.Trim(),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record SongInfo(string? Title, string? Album, int? Year, string? Credit, string? SpotifyTrackId);
}
