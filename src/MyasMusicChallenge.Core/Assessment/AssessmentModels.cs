using MyasMusicChallenge.Core.Lyrics;
using MyasMusicChallenge.Core.Songs;

namespace MyasMusicChallenge.Core.Assessment;

public enum AssessmentCategory
{
    PitchAccuracy,
    PitchStability,
    RhythmAndTiming,
    VocalRange,
    DynamicsControl,
    BreathSupport,
}

public sealed record CategoryScore(AssessmentCategory Category, string Name, int Score, string Comment);

public sealed record PerformanceStatistics(
    double DurationSeconds,
    double VoicedSeconds,
    string? LowestNote,
    string? HighestNote,
    double RangeSemitones,
    string? DetectedKey,
    double AverageCentsOff,
    double LongestPhraseSeconds,
    bool VibratoDetected);

/// <summary>Mýa's professional assessment of a karaoke performance.</summary>
public sealed record VocalAssessment(
    int OverallScore,
    string Grade,
    string Title,
    string Headline,
    IReadOnlyList<CategoryScore> Categories,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Improvements,
    string CoachingTip,
    string SignOff,
    PerformanceStatistics Statistics)
{
    /// <summary>All of Mýa's spoken feedback lines in order, for the results screen speech bubble.</summary>
    public IEnumerable<string> SpeechLines()
    {
        yield return Headline;
        foreach (var strength in Strengths)
        {
            yield return strength;
        }

        foreach (var improvement in Improvements)
        {
            yield return improvement;
        }

        yield return CoachingTip;
        yield return SignOff;
    }
}

/// <summary>Song context that helps the assessor judge timing and pitch.</summary>
public sealed record AssessmentContext(
    string SongTitle,
    double DurationSeconds,
    IReadOnlyList<LyricLine>? Lyrics = null,
    IReadOnlyList<ReferenceNote>? Melody = null,
    string PlayerName = "superstar");
