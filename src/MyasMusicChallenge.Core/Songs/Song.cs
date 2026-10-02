using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace MyasMusicChallenge.Core.Songs;

public sealed record Song
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Album { get; init; } = string.Empty;

    public int? Year { get; init; }

    /// <summary>Collaboration credit shown after the title, e.g. "feat. Sisqó".</summary>
    public string? Credit { get; init; }

    public string? SpotifyTrackId { get; init; }

    public double? DurationSeconds { get; init; }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Credit) ? Title : $"{Title} ({Credit})";

    [JsonIgnore]
    public string? SpotifyUri => IsValidSpotifyId(SpotifyTrackId) ? $"spotify:track:{SpotifyTrackId}" : null;

    [JsonIgnore]
    public string? SpotifyUrl => IsValidSpotifyId(SpotifyTrackId) ? $"https://open.spotify.com/track/{SpotifyTrackId}" : null;

    /// <summary>Spotify ids are 22 character base-62 strings.</summary>
    public static bool IsValidSpotifyId(string? id) =>
        id is { Length: 22 } && id.All(char.IsAsciiLetterOrDigit);

    /// <summary>Creates a stable, file-system friendly id (e.g. "Case of the Ex" → "case-of-the-ex").</summary>
    public static string CreateId(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasDash = true;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark || character == '\'' || character == '’')
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasDash = false;
            }
            else if (character == '&')
            {
                if (!lastWasDash)
                {
                    builder.Append('-');
                }

                builder.Append("and-");
                lastWasDash = true;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }
}
