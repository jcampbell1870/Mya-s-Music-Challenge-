using MyasMusicChallenge.Core.Assessment;
using MyasMusicChallenge.Core.Lyrics;
using MyasMusicChallenge.Core.Songs;
using MyasMusicChallenge.Tests.TestUtilities;

namespace MyasMusicChallenge.Tests.Assessment;

public class PerformanceAssessorTests
{
    private static readonly int[] CMajorMelody = [60, 62, 64, 65, 67, 69, 71, 72, 74, 76, 77, 79];

    private readonly PerformanceAssessor _assessor = new();

    [Fact]
    public void SkilledSinger_ScoresHighWithPositiveFeedback()
    {
        var track = SkilledPerformance();
        var context = new AssessmentContext("Case of the Ex", track.Duration, PlayerName: "Jordan");

        var result = _assessor.Assess(track.Build(), context);

        Assert.True(result.OverallScore >= 80, $"Expected a high score but got {result.OverallScore}.");
        Assert.Contains(result.Grade, new[] { "S", "A" });
        Assert.Contains("Jordan", result.Headline);
        Assert.Contains("Case of the Ex", result.Headline);
        Assert.NotEmpty(result.Strengths);
        Assert.Equal(6, result.Categories.Count);
        Assert.True(result.Statistics.VibratoDetected);
        Assert.StartsWith("C major", result.Statistics.DetectedKey);
        Assert.Equal("C4", result.Statistics.LowestNote);
        Assert.Contains(result.CoachingTip, result.SpeechLines());
    }

    [Fact]
    public void OffKeyShakySinger_ScoresLowerWithImprovements()
    {
        var random = new Random(42);
        var track = new PitchTrackBuilder();
        for (var phrase = 0; phrase < 12; phrase++)
        {
            for (var note = 0; note < 4; note++)
            {
                // Random, out-of-tune pitches in a narrow range with lots of wobble and uneven volume.
                var midi = 60 + random.Next(0, 5) + 0.4 + (random.NextDouble() * 0.2);
                track.Note(midi, 0.2, rms: 0.01 + (random.NextDouble() * 0.3), centsJitter: 35, random: random);
            }

            track.Rest(1.5);
        }

        var skilled = _assessor.Assess(SkilledPerformance().Build(), new AssessmentContext("Free", 60));
        var result = _assessor.Assess(track.Build(), new AssessmentContext("Free", track.Duration));

        Assert.True(result.OverallScore < 55, $"Expected a low score but got {result.OverallScore}.");
        Assert.True(result.OverallScore < skilled.OverallScore - 25);
        Assert.NotEmpty(result.Improvements);
        Assert.True(Score(result, AssessmentCategory.PitchAccuracy) < 50);
        Assert.True(Score(result, AssessmentCategory.PitchStability) < 50, string.Join(",", result.Categories.Select(c => c.Score)));
        Assert.True(Score(result, AssessmentCategory.BreathSupport) < 50);
        Assert.False(result.Statistics.VibratoDetected);
    }

    [Fact]
    public void Silence_AsksForAMicCheck()
    {
        var track = new PitchTrackBuilder().Rest(30).Note(64, 1.0);

        var result = _assessor.Assess(track.Build(), new AssessmentContext("Fallen", 30));

        Assert.Equal(0, result.OverallScore);
        Assert.Equal("Mic Check Needed", result.Title);
        Assert.All(result.Categories, c => Assert.Equal(0, c.Score));
    }

    [Fact]
    public void ReferenceMelody_RewardsMatchingNotesAndPenalizesWrongOnes()
    {
        var melody = new List<ReferenceNote>();
        var time = 0.0;
        foreach (var midi in CMajorMelody.Concat(CMajorMelody))
        {
            melody.Add(new ReferenceNote(time, 0.9, midi));
            time += 1.0;
        }

        var matching = new PitchTrackBuilder();
        var wrong = new PitchTrackBuilder();
        foreach (var note in melody)
        {
            matching.Note(note.Midi - 12, 0.9).Rest(0.1); // an octave lower still counts as the right note
            wrong.Note(note.Midi + 3, 0.9).Rest(0.1);
        }

        var context = new AssessmentContext("Ridin'", time, Melody: melody);
        var good = _assessor.Assess(matching.Build(), context);
        var bad = _assessor.Assess(wrong.Build(), context);

        Assert.True(Score(good, AssessmentCategory.PitchAccuracy) >= 95);
        Assert.True(Score(good, AssessmentCategory.RhythmAndTiming) >= 95);
        Assert.Equal(0, Score(bad, AssessmentCategory.PitchAccuracy));
        Assert.True(good.OverallScore > bad.OverallScore);
    }

    [Fact]
    public void Lyrics_DriveRhythmScore()
    {
        var lyrics = Enumerable.Range(0, 10)
            .Select(i => new LyricLine(TimeSpan.FromSeconds(i * 4), $"line {i}"))
            .ToList();

        var onCue = new PitchTrackBuilder();
        var halfMissing = new PitchTrackBuilder();
        for (var i = 0; i < 10; i++)
        {
            onCue.Note(CMajorMelody[i], 3.0).Rest(1.0);
            if (i % 2 == 0)
            {
                halfMissing.Note(CMajorMelody[i], 3.0).Rest(1.0);
            }
            else
            {
                halfMissing.Rest(4.0);
            }
        }

        var context = new AssessmentContext("Paradise", 40, Lyrics: lyrics);

        Assert.Equal(100, Score(_assessor.Assess(onCue.Build(), context), AssessmentCategory.RhythmAndTiming));
        Assert.Equal(50, Score(_assessor.Assess(halfMissing.Build(), context), AssessmentCategory.RhythmAndTiming));
    }

    [Theory]
    [InlineData(95, "S", "Superstar")]
    [InlineData(80, "A", "Headliner")]
    [InlineData(72, "B", "Rising Star")]
    [InlineData(55, "C", "Showcase Contender")]
    [InlineData(41, "D", "Open Mic Brave Heart")]
    [InlineData(3, "E", "Studio Rookie")]
    public void GradeFor_MapsScoreBands(int score, string grade, string title)
    {
        Assert.Equal((grade, title), MyaFeedback.GradeFor(score));
    }

    private static PitchTrackBuilder SkilledPerformance()
    {
        var track = new PitchTrackBuilder();
        var dynamics = new[] { 0.05, 0.08, 0.12, 0.2 };
        for (var phrase = 0; phrase < 6; phrase++)
        {
            for (var note = 0; note < 6; note++)
            {
                var midi = CMajorMelody[((phrase * 2) + note) % CMajorMelody.Length];
                track.Note(midi, 0.6, rms: dynamics[(phrase + note) % dynamics.Length]);
            }

            track.Note(CMajorMelody[phrase % 5] + 12, 1.5, rms: 0.15, vibratoCents: 40);
            track.Rest(1.0);
        }

        return track;
    }

    private static int Score(VocalAssessment assessment, AssessmentCategory category) =>
        assessment.Categories.Single(c => c.Category == category).Score;
}
