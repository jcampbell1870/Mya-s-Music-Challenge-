using MyasMusicChallenge.Core.Configuration;
using MyasMusicChallenge.Core.Lyrics;
using MyasMusicChallenge.Core.Songs;

namespace MyasMusicChallenge.Tests.Songs;

public sealed class SongLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mya-tests-" + Guid.NewGuid().ToString("N"));

    public SongLibraryTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("Case of the Ex", "case-of-the-ex")]
    [InlineData("It's All About Me", "its-all-about-me")]
    [InlineData("My Love Is Like...Wo", "my-love-is-like-wo")]
    [InlineData("Sugar & Spice", "sugar-and-spice")]
    [InlineData("Mýa", "mya")]
    [InlineData("  Ridin'  ", "ridin")]
    public void CreateId_ProducesStableSlugs(string title, string expected)
    {
        Assert.Equal(expected, Song.CreateId(title));
    }

    [Fact]
    public void Catalog_HasUniqueIdsAndMyaSignatureSongs()
    {
        var songs = MyaCatalog.Songs;

        Assert.Equal(songs.Count, songs.Select(s => s.Id).Distinct().Count());
        Assert.Contains(songs, s => s.Title == "Case of the Ex" && s.Album == "Fear of Flying" && s.Year == 2000);
        Assert.Contains(songs, s => s.Title == "Lady Marmalade");
        Assert.All(songs, s => Assert.False(string.IsNullOrWhiteSpace(s.Album)));
        Assert.Equal("It's All About Me (feat. Sisqó)", songs.Single(s => s.Id == "its-all-about-me").DisplayTitle);
    }

    [Fact]
    public void SpotifyLinks_RequireValidIds()
    {
        var valid = new Song { Id = "free", Title = "Free", SpotifyTrackId = "4uLU6hMCjMI75M1A2tKUQC" };
        var invalid = valid with { SpotifyTrackId = "bad id\" & calc.exe" };

        Assert.Equal("spotify:track:4uLU6hMCjMI75M1A2tKUQC", valid.SpotifyUri);
        Assert.Equal("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", valid.SpotifyUrl);
        Assert.Null(invalid.SpotifyUri);
        Assert.Null(invalid.SpotifyUrl);
    }

    [Fact]
    public void Merge_FillsSpotifyDetailsAndAddsNewSongs()
    {
        var fromSpotify = new[]
        {
            new Song { Id = "case-of-the-ex", Title = "Case of the Ex", Album = "Fear of Flying", SpotifyTrackId = "4uLU6hMCjMI75M1A2tKUQC", DurationSeconds = 236 },
            new Song { Id = "brand-new-song", Title = "Brand New Song", Album = "New Album", Year = 2026 },
        };

        var merged = SongLibrary.Merge(MyaCatalog.Songs, fromSpotify);

        Assert.Equal(MyaCatalog.Songs.Count + 1, merged.Count);
        var caseOfTheEx = merged.Single(s => s.Id == "case-of-the-ex");
        Assert.Equal("4uLU6hMCjMI75M1A2tKUQC", caseOfTheEx.SpotifyTrackId);
        Assert.Equal(236, caseOfTheEx.DurationSeconds);
        Assert.Equal(2000, caseOfTheEx.Year);
    }

    [Fact]
    public void Build_DiscoversKaraokeAssetsAndCustomSongs()
    {
        var caseFolder = Directory.CreateDirectory(Path.Combine(_root, "case-of-the-ex")).FullName;
        File.WriteAllText(Path.Combine(caseFolder, SongLibrary.LyricsFileName), "[00:01.00]la la");
        File.WriteAllBytes(Path.Combine(caseFolder, "backing.mp3"), [1, 2, 3]);
        File.WriteAllText(
            Path.Combine(caseFolder, SongLibrary.MelodyFileName),
            """[{"start": 1.0, "duration": 0.5, "midi": 64}, {"start": 0.2, "duration": 0.5, "midi": 62}, {"start": 3, "duration": 0, "midi": 60}]""");

        var customFolder = Directory.CreateDirectory(Path.Combine(_root, "Unreleased Demo")).FullName;
        File.WriteAllText(
            Path.Combine(customFolder, SongLibrary.SongInfoFileName),
            """{ "title": "Unreleased Demo", "album": "Vault", "year": 2025, "credit": "feat. A Friend" }""");

        Directory.CreateDirectory(Path.Combine(_root, "folder-without-info"));

        var entries = SongLibrary.Build(MyaCatalog.Songs, _root);

        Assert.Equal(MyaCatalog.Songs.Count + 1, entries.Count);
        var caseEntry = entries.Single(e => e.Song.Id == "case-of-the-ex");
        Assert.True(caseEntry.HasLyrics);
        Assert.True(caseEntry.HasBackingTrack);
        Assert.True(caseEntry.HasMelody);
        Assert.Single(SongLibrary.LoadLyrics(caseEntry));

        var melody = SongLibrary.LoadMelody(caseEntry.MelodyPath!);
        Assert.Equal(new[] { 62.0, 64.0 }, melody.Select(n => n.Midi));

        var custom = entries.Single(e => e.Song.Id == "unreleased-demo");
        Assert.Equal("Unreleased Demo (feat. A Friend)", custom.Song.DisplayTitle);

        // Songs with karaoke assets are listed first.
        Assert.Equal("case-of-the-ex", entries[0].Song.Id);
    }

    [Fact]
    public void Build_HandlesMissingSongsFolder()
    {
        var entries = SongLibrary.Build(MyaCatalog.Songs, Path.Combine(_root, "missing"));

        Assert.Equal(MyaCatalog.Songs.Count, entries.Count);
        Assert.All(entries, e => Assert.False(e.HasLyrics));
    }

    [Fact]
    public void Lrc_ParsesTimestampsOffsetsAndRepeatedTags()
    {
        const string lrc = """
            [ar:Mýa]
            [ti:Practice Song]
            [offset:+500]
            [00:12.50]First line
            [00:05.00][01:02.25]Chorus line
            [00:20]Third line
            not a lyric line
            """;

        var lines = LrcParser.Parse(lrc);

        Assert.Equal(4, lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(4.5), lines[0].Start);
        Assert.Equal("Chorus line", lines[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(12), lines[1].Start);
        Assert.Equal(TimeSpan.FromSeconds(19.5), lines[2].Start);
        Assert.Equal(TimeSpan.FromSeconds(61.75), lines[3].Start);

        Assert.Equal(-1, LrcParser.FindCurrentLine(lines, TimeSpan.FromSeconds(1)));
        Assert.Equal(1, LrcParser.FindCurrentLine(lines, TimeSpan.FromSeconds(15)));
        Assert.Equal(3, LrcParser.FindCurrentLine(lines, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Lrc_IgnoresOutOfRangeOffsetTag()
    {
        var lines = LrcParser.Parse("[offset:99999999999999]\n[00:10.00]Line");

        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(lines).Start);
    }

    [Fact]
    public void Settings_LoadFromJsonWithDefaultsForMissingValues()
    {
        var path = Path.Combine(_root, GameSettings.FileName);
        File.WriteAllText(path, """
            {
              // comments are allowed
              "Spotify": { "ClientId": "abc", "ClientSecret": "def" },
              "SongsDirectory": "MySongs",
            }
            """);

        var settings = GameSettings.Load(path);

        Assert.True(settings.Spotify.IsConfigured);
        Assert.Equal("0x1e4f6e4a382adbdb662733a19ae773d3ab8f497d", settings.Rewards.RewardVaultAddress);
        Assert.Equal(Path.Combine(_root, "MySongs"), settings.ResolveSongsDirectory(_root));
        Assert.False(GameSettings.Load(Path.Combine(_root, "nope.json")).Spotify.IsConfigured);
    }

    [Fact]
    public void PlayerProfile_RoundTripsAndToleratesCorruption()
    {
        var path = Path.Combine(_root, "profile", "player.json");
        var profile = new PlayerProfile { PlayerName = "Jordan", WalletAddress = "0x" + new string('a', 40) };
        profile.History.Add(new PerformanceRecord { SongId = "free", Score = 81 });
        profile.History.Add(new PerformanceRecord { SongId = "free", Score = 64 });
        profile.SpotifyCatalog.Add(new Song { Id = "free", Title = "Free", SpotifyTrackId = "4uLU6hMCjMI75M1A2tKUQC" });

        profile.Save(path);
        var loaded = PlayerProfile.Load(path);

        Assert.Equal("Jordan", loaded.PlayerName);
        Assert.Equal(81, loaded.BestScore("free"));
        Assert.Equal(0, loaded.BestScore("fallen"));
        Assert.Equal("spotify:track:4uLU6hMCjMI75M1A2tKUQC", loaded.SpotifyCatalog.Single().SpotifyUri);

        File.WriteAllText(path, "{ not json");
        Assert.Equal("Superstar", PlayerProfile.Load(path).PlayerName);
    }
}
