using System.Diagnostics;
using MyasMusicChallenge.Core.Assessment;
using MyasMusicChallenge.Core.Configuration;
using MyasMusicChallenge.Core.Rewards;
using MyasMusicChallenge.Core.Songs;
using MyasMusicChallenge.Core.Spotify;

namespace MyasMusicChallenge;

/// <summary>Shared game services: settings, player profile, song library, rewards and Spotify.</summary>
public sealed class GameContext : IDisposable
{
    private readonly HttpClient _httpClient;
    private ClaimPageServer? _claimServer;

    private GameContext(GameSettings settings, string profilePath, string songsDirectory)
    {
        Settings = settings;
        ProfilePath = profilePath;
        SongsDirectory = songsDirectory;
        Profile = PlayerProfile.Load(profilePath);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyasMusicChallenge/1.0");
        Rewards = new RewardIssuerClient(_httpClient, settings.Rewards);
        Spotify = settings.Spotify.WithEnvironmentOverrides();
        ReloadSongs();
    }

    public GameSettings Settings { get; }

    public SpotifyOptions Spotify { get; }

    public PlayerProfile Profile { get; }

    public string ProfilePath { get; }

    public string SongsDirectory { get; }

    public RewardIssuerClient Rewards { get; }

    public PerformanceAssessor Assessor { get; } = new();

    public IReadOnlyList<SongEntry> Songs { get; private set; } = [];

    public static GameContext Create(string baseDirectory)
    {
        var settings = GameSettings.Load(Path.Combine(baseDirectory, GameSettings.FileName));
        var songsDirectory = settings.ResolveSongsDirectory(baseDirectory);
        try
        {
            Directory.CreateDirectory(songsDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The game still works with the built-in catalog if the folder can't be created.
        }

        return new GameContext(settings, PlayerProfile.DefaultPath, songsDirectory);
    }

    public void ReloadSongs()
    {
        var catalog = SongLibrary.Merge(MyaCatalog.Songs, Profile.SpotifyCatalog);
        Songs = SongLibrary.Build(catalog, SongsDirectory);
    }

    public void SaveProfile()
    {
        try
        {
            Profile.Save(ProfilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not save profile: {ex.Message}");
        }
    }

    public async Task<int> SyncSpotifyAsync(CancellationToken cancellationToken = default)
    {
        var client = new SpotifyCatalogClient(_httpClient, Spotify);
        var songs = await client.GetDiscographyAsync(cancellationToken);
        Profile.SpotifyCatalog = [.. songs];
        Profile.SpotifySyncedAt = DateTime.UtcNow;
        SaveProfile();
        ReloadSongs();
        return songs.Count;
    }

    /// <summary>Serves the MetaMask claim page on loopback and opens it in the default browser.</summary>
    public Uri PublishClaimPage(RewardClaimTransaction transaction, string songTitle)
    {
        if (_claimServer is null)
        {
            _claimServer = new ClaimPageServer();
            _claimServer.Start();
        }

        var uri = _claimServer.Publish(ClaimPageBuilder.Build(transaction, songTitle));
        OpenExternal(uri.AbsoluteUri);
        return uri;
    }

    /// <summary>Opens a trusted URL (http/https loopback claim page, Spotify links) or folder with the shell.</summary>
    public static bool OpenExternal(string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Debug.WriteLine($"Could not open {target}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Opens the track in the Spotify desktop app, falling back to the Spotify web player.</summary>
    public static bool OpenInSpotify(Song song) =>
        (song.SpotifyUri is { } uri && OpenExternal(uri)) || (song.SpotifyUrl is { } url && OpenExternal(url));

    public void Dispose()
    {
        if (_claimServer is not null)
        {
            var server = _claimServer;
            Task.Run(async () => await server.DisposeAsync()).Wait(TimeSpan.FromSeconds(2));
        }

        _httpClient.Dispose();
    }
}
