using System.Text.Json;
using MyasMusicChallenge.Core.Rewards;
using MyasMusicChallenge.Core.Songs;
using MyasMusicChallenge.Core.Spotify;

namespace MyasMusicChallenge.Core.Configuration;

/// <summary>Game configuration loaded from <c>appsettings.json</c> next to the executable.</summary>
public sealed class GameSettings
{
    public const string FileName = "appsettings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public RewardTreasuryOptions Rewards { get; set; } = new();

    public SpotifyOptions Spotify { get; set; } = new();

    /// <summary>Folder containing per-song karaoke assets; relative paths resolve against the game folder.</summary>
    public string SongsDirectory { get; set; } = "Songs";

    public static GameSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new GameSettings();
        }

        return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), JsonOptions) ?? new GameSettings();
    }

    public string ResolveSongsDirectory(string baseDirectory) =>
        Path.IsPathRooted(SongsDirectory) ? SongsDirectory : Path.Combine(baseDirectory, SongsDirectory);
}

public sealed class PerformanceRecord
{
    public string GameId { get; set; } = string.Empty;

    public string SongId { get; set; } = string.Empty;

    public string SongTitle { get; set; } = string.Empty;

    public int Score { get; set; }

    public string Grade { get; set; } = string.Empty;

    public DateTime PlayedAt { get; set; }

    public bool RewardIssued { get; set; }
}

/// <summary>Per-player data saved under <c>%APPDATA%\MyasMusicChallenge</c>.</summary>
public sealed class PlayerProfile
{
    public string PlayerName { get; set; } = "Superstar";

    public string WalletAddress { get; set; } = string.Empty;

    public int MicrophoneDeviceNumber { get; set; }

    public List<PerformanceRecord> History { get; set; } = [];

    public List<Song> SpotifyCatalog { get; set; } = [];

    public DateTime? SpotifySyncedAt { get; set; }

    public int BestScore(string songId) =>
        History.Where(h => h.SongId == songId).Select(h => h.Score).DefaultIfEmpty(0).Max();

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyasMusicChallenge",
            "player.json");

    public static PlayerProfile Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<PlayerProfile>(File.ReadAllText(path), JsonOptions) ?? new PlayerProfile();
            }
        }
        catch (JsonException)
        {
            // A corrupt profile should never stop the game from starting.
        }

        return new PlayerProfile();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
}
