using System.Globalization;
using System.Text.RegularExpressions;

namespace MyasMusicChallenge.Core.Lyrics;

public sealed record LyricLine(TimeSpan Start, string Text);

/// <summary>
/// Parses synchronized lyrics in the common LRC format (<c>[mm:ss.xx] lyric text</c>).
/// Lyrics are supplied by the player (licensed karaoke files); none are bundled with the game.
/// </summary>
public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2}(?:[.:]\d{1,3})?)\]")]
    private static partial Regex TimeTagRegex();

    [GeneratedRegex(@"^\[offset:\s*([+-]?\d+)\s*\]$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetTagRegex();

    public static IReadOnlyList<LyricLine> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = new List<LyricLine>();
        var offset = TimeSpan.Zero;

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var offsetMatch = OffsetTagRegex().Match(line);
            if (offsetMatch.Success)
            {
                // LRC offsets are in milliseconds; positive values make lyrics appear sooner.
                if (int.TryParse(offsetMatch.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offsetMs))
                {
                    offset = TimeSpan.FromMilliseconds(-offsetMs);
                }

                continue;
            }

            var matches = TimeTagRegex().Matches(line);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = line[(matches[^1].Index + matches[^1].Length)..].Trim();
            foreach (Match match in matches)
            {
                var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = double.Parse(match.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                lines.Add(new LyricLine(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds), text));
            }
        }

        return lines
            .Select(l => l with { Start = l.Start + offset < TimeSpan.Zero ? TimeSpan.Zero : l.Start + offset })
            .OrderBy(l => l.Start)
            .ToList();
    }

    public static IReadOnlyList<LyricLine> Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>Index of the line being sung at <paramref name="position"/>, or -1 before the first line.</summary>
    public static int FindCurrentLine(IReadOnlyList<LyricLine> lines, TimeSpan position)
    {
        var index = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Start > position)
            {
                break;
            }

            index = i;
        }

        return index;
    }
}
