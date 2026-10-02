namespace MyasMusicChallenge.Core.Songs;

/// <summary>
/// Built-in starter catalog of Mýa's signature songs (metadata only). The complete, up-to-date discography
/// is pulled from Spotify at runtime by <see cref="Spotify.SpotifyCatalogClient"/>, and players can add any
/// other song through the <c>Songs</c> folder.
/// </summary>
public static class MyaCatalog
{
    public const string ArtistName = "Mýa";

    public static IReadOnlyList<Song> Songs { get; } =
    [
        Create("It's All About Me", "Mýa", 1998, "feat. Sisqó"),
        Create("Movin' On", "Mýa", 1998, "feat. Silkk the Shocker"),
        Create("My First Night with You", "Mýa", 1998),
        Create("Take Me There", "The Rugrats Movie (Soundtrack)", 1998, "with Blackstreet, Mase & Blinky Blink"),
        Create("Ghetto Supastar (That Is What You Are)", "Bulworth (Soundtrack)", 1998, "with Pras & Ol' Dirty Bastard"),
        Create("The Best of Me", "Fear of Flying", 2000, "feat. Jadakiss"),
        Create("Case of the Ex", "Fear of Flying", 2000),
        Create("Free", "Fear of Flying", 2000),
        Create("Lady Marmalade", "Moulin Rouge! (Soundtrack)", 2001, "with Christina Aguilera, Lil' Kim & Pink"),
        Create("My Love Is Like...Wo", "Moodring", 2003),
        Create("Fallen", "Moodring", 2003),
        Create("Ridin'", "Liberation", 2007),
        Create("Walka Not a Talka", "Liberation", 2007, "feat. Snoop Dogg"),
        Create("Lock U Down", "Liberation", 2007, "feat. Lil Wayne"),
        Create("Paradise", "Sugar & Spice", 2008),
        Create("Fabulous Life", "K.I.S.S. (Keep It Sexy & Simple)", 2011),
        Create("Earthquake", "K.I.S.S. (Keep It Sexy & Simple)", 2011, "feat. Trina"),
        Create("Welcome to My World", "Smoove Jones", 2016),
        Create("Team You", "Smoove Jones", 2016),
        Create("Ready for Whatever", "T.K.O. (The Knock Out)", 2018),
    ];

    private static Song Create(string title, string album, int year, string? credit = null) => new()
    {
        Id = Song.CreateId(title),
        Title = title,
        Album = album,
        Year = year,
        Credit = credit,
    };
}
