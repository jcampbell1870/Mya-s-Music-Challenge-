namespace MyasMusicChallenge.Core.Spotify;

/// <summary>
/// Optional Spotify connection used to load Mýa's full discography and to play the original recordings in the
/// Spotify app. Create an app at https://developer.spotify.com/dashboard and supply its credentials through
/// <c>appsettings.json</c> or the <c>MYA_SPOTIFY_CLIENT_ID</c> / <c>MYA_SPOTIFY_CLIENT_SECRET</c> environment variables.
/// </summary>
public sealed class SpotifyOptions
{
    public const string ClientIdEnvironmentVariable = "MYA_SPOTIFY_CLIENT_ID";
    public const string ClientSecretEnvironmentVariable = "MYA_SPOTIFY_CLIENT_SECRET";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Optional Spotify artist id for Mýa; when empty the artist is found by search.</summary>
    public string ArtistId { get; set; } = string.Empty;

    public string Market { get; set; } = "US";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public SpotifyOptions WithEnvironmentOverrides()
    {
        var id = Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable);
        var secret = Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariable);
        return new SpotifyOptions
        {
            ClientId = string.IsNullOrWhiteSpace(id) ? ClientId : id.Trim(),
            ClientSecret = string.IsNullOrWhiteSpace(secret) ? ClientSecret : secret.Trim(),
            ArtistId = ArtistId,
            Market = Market,
        };
    }
}
