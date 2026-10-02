using MyasMusicChallenge.Core.Audio;
using MyasMusicChallenge.Core.Songs;

namespace MyasMusicChallenge.Core.Assessment;

/// <summary>
/// Turns a recorded pitch track into a professional-style vocal assessment covering intonation, pitch
/// stability, rhythm, range, dynamics and breath support.
/// </summary>
public sealed class PerformanceAssessor
{
    public const double MinimumVoicedSeconds = 3.0;

    private const double DefaultFrameSeconds = VocalAnalyzer.DefaultHopSize / 44100.0;
    private const double PhraseGapSeconds = 0.3;
    private const double SameNoteSemitones = 0.6;
    private const int MinimumSustainFrames = 4;

    private static readonly int[] MajorScale = [0, 2, 4, 5, 7, 9, 11];

    private static readonly IReadOnlyDictionary<AssessmentCategory, double> Weights =
        new Dictionary<AssessmentCategory, double>
        {
            [AssessmentCategory.PitchAccuracy] = 0.30,
            [AssessmentCategory.PitchStability] = 0.15,
            [AssessmentCategory.RhythmAndTiming] = 0.20,
            [AssessmentCategory.VocalRange] = 0.10,
            [AssessmentCategory.DynamicsControl] = 0.10,
            [AssessmentCategory.BreathSupport] = 0.15,
        };

    public VocalAssessment Assess(IReadOnlyList<PitchFrame> frames, AssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(context);

        var frameSeconds = EstimateFrameSeconds(frames);
        var voiced = frames.Where(f => f.IsVoiced).ToList();
        var voicedSeconds = voiced.Count * frameSeconds;
        var duration = Math.Max(context.DurationSeconds, frames.Count == 0 ? 0 : frames[^1].Time);

        if (voicedSeconds < MinimumVoicedSeconds)
        {
            return MyaFeedback.NotEnoughSinging(context, duration, voicedSeconds);
        }

        var segments = FindSustainedSegments(frames);
        var phrases = FindPhrases(voiced);
        var midis = voiced.Select(f => f.Midi).OrderBy(m => m).ToList();
        var low = Percentile(midis, 0.05);
        var high = Percentile(midis, 0.95);

        var (accuracy, averageCents, key) = ScorePitchAccuracy(voiced, segments, context.Melody);
        var stability = ScoreStability(segments);
        var rhythm = ScoreRhythm(frames, context, duration, voicedSeconds);
        var range = ScoreRange(high - low, context.Melody);
        var dynamics = ScoreDynamics(voiced);
        var averagePhrase = phrases.Count == 0 ? 0 : phrases.Average();
        var longestPhrase = phrases.Count == 0 ? 0 : phrases.Max();
        var breath = Clamp01(averagePhrase / 3.0);
        var vibrato = DetectVibrato(segments, frameSeconds);

        var statistics = new PerformanceStatistics(
            duration,
            voicedSeconds,
            PitchMath.NoteName(low),
            PitchMath.NoteName(high),
            high - low,
            key,
            averageCents,
            longestPhrase,
            vibrato);

        var scores = new Dictionary<AssessmentCategory, double>
        {
            [AssessmentCategory.PitchAccuracy] = accuracy,
            [AssessmentCategory.PitchStability] = stability,
            [AssessmentCategory.RhythmAndTiming] = rhythm,
            [AssessmentCategory.VocalRange] = range,
            [AssessmentCategory.DynamicsControl] = dynamics,
            [AssessmentCategory.BreathSupport] = breath,
        };

        var overall = scores.Sum(pair => Weights[pair.Key] * pair.Value) * 100.0;
        if (vibrato)
        {
            overall += 3;
        }

        var categoryScores = scores.ToDictionary(pair => pair.Key, pair => (int)Math.Round(pair.Value * 100));
        return MyaFeedback.Build(context, (int)Math.Clamp(Math.Round(overall), 0, 100), categoryScores, statistics);
    }

    private static double EstimateFrameSeconds(IReadOnlyList<PitchFrame> frames)
    {
        if (frames.Count < 2)
        {
            return DefaultFrameSeconds;
        }

        var span = frames[^1].Time - frames[0].Time;
        return span > 0 ? span / (frames.Count - 1) : DefaultFrameSeconds;
    }

    private static List<List<PitchFrame>> FindSustainedSegments(IReadOnlyList<PitchFrame> frames)
    {
        var segments = new List<List<PitchFrame>>();
        var current = new List<PitchFrame>();
        var currentSum = 0.0;

        foreach (var frame in frames)
        {
            if (frame.IsVoiced && (current.Count == 0 || Math.Abs(frame.Midi - (currentSum / current.Count)) < SameNoteSemitones))
            {
                current.Add(frame);
                currentSum += frame.Midi;
                continue;
            }

            if (current.Count >= MinimumSustainFrames)
            {
                segments.Add(current);
            }

            current = frame.IsVoiced ? [frame] : [];
            currentSum = frame.IsVoiced ? frame.Midi : 0;
        }

        if (current.Count >= MinimumSustainFrames)
        {
            segments.Add(current);
        }

        return segments;
    }

    private static List<double> FindPhrases(IReadOnlyList<PitchFrame> voiced)
    {
        var phrases = new List<double>();
        if (voiced.Count == 0)
        {
            return phrases;
        }

        var start = voiced[0].Time;
        var previous = voiced[0].Time;
        foreach (var frame in voiced.Skip(1))
        {
            if (frame.Time - previous > PhraseGapSeconds)
            {
                AddPhrase(start, previous);
                start = frame.Time;
            }

            previous = frame.Time;
        }

        AddPhrase(start, previous);
        return phrases;

        void AddPhrase(double from, double to)
        {
            if (to - from >= PhraseGapSeconds)
            {
                phrases.Add(to - from);
            }
        }
    }

    private static (double Score, double AverageCents, string? Key) ScorePitchAccuracy(
        IReadOnlyList<PitchFrame> voiced,
        IReadOnlyList<List<PitchFrame>> segments,
        IReadOnlyList<ReferenceNote>? melody)
    {
        if (melody is { Count: > 0 })
        {
            var errors = new List<double>();
            foreach (var frame in voiced)
            {
                var note = melody.FirstOrDefault(n => frame.Time >= n.Start - 0.1 && frame.Time <= n.End + 0.1);
                if (note is not null)
                {
                    errors.Add(Math.Abs(PitchMath.PitchClassDistanceCents(frame.Midi, note.Midi)));
                }
            }

            if (errors.Count == 0)
            {
                return (0, 0, null);
            }

            var score = errors.Average(cents => Clamp01(1.0 - ((cents - 25.0) / 75.0)));
            return (score, errors.Average(), null);
        }

        var sustained = segments.SelectMany(s => s).ToList();
        if (sustained.Count == 0)
        {
            sustained = voiced.ToList();
        }

        var meanCents = sustained.Average(f => Math.Abs(PitchMath.CentsFromNearestSemitone(f.Midi)));
        var tuning = Clamp01(1.0 - ((meanCents - 8.0) / 17.0));

        var histogram = new double[12];
        foreach (var frame in sustained)
        {
            histogram[PitchMath.PitchClass(frame.Midi)]++;
        }

        var total = histogram.Sum();
        var bestTonic = 0;
        var bestFit = 0.0;
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var fit = MajorScale.Sum(step => histogram[(tonic + step) % 12]) / total;
            if (fit > bestFit)
            {
                bestFit = fit;
                bestTonic = tonic;
            }
        }

        var scaleFit = Clamp01((bestFit - 0.6) / 0.35);
        var key = $"{PitchMath.PitchClassName(bestTonic)} major / {PitchMath.PitchClassName(bestTonic + 9)} minor";
        return ((0.6 * tuning) + (0.4 * scaleFit), meanCents, key);
    }

    private static double ScoreStability(IReadOnlyList<List<PitchFrame>> segments)
    {
        var residuals = new List<double>();
        foreach (var segment in segments)
        {
            for (var i = 1; i < segment.Count - 1; i++)
            {
                var average = (segment[i - 1].Midi + segment[i].Midi + segment[i + 1].Midi) / 3.0;
                residuals.Add(Math.Abs(segment[i].Midi - average) * 100.0);
            }
        }

        if (residuals.Count == 0)
        {
            return 0.3;
        }

        return Clamp01(1.0 - ((residuals.Average() - 4.0) / 14.0));
    }

    private static double ScoreRhythm(
        IReadOnlyList<PitchFrame> frames,
        AssessmentContext context,
        double duration,
        double voicedSeconds)
    {
        if (context.Melody is { Count: > 0 })
        {
            double covered = 0;
            double expected = 0;
            foreach (var note in context.Melody.Where(n => n.Start < duration))
            {
                var inNote = frames.Where(f => f.Time >= note.Start && f.Time <= note.End).ToList();
                if (inNote.Count == 0)
                {
                    continue;
                }

                expected += note.Duration;
                covered += note.Duration * inNote.Count(f => f.IsVoiced) / inNote.Count;
            }

            if (expected > 0)
            {
                return Clamp01(covered / expected / 0.8);
            }
        }

        if (context.Lyrics is { Count: > 0 })
        {
            var lyrics = context.Lyrics;
            var linesExpected = 0;
            var linesOnCue = 0;
            for (var i = 0; i < lyrics.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lyrics[i].Text))
                {
                    continue;
                }

                var start = lyrics[i].Start.TotalSeconds;
                var end = i + 1 < lyrics.Count ? lyrics[i + 1].Start.TotalSeconds : start + 6;
                end = Math.Min(end, start + 8);
                if (start >= duration)
                {
                    break;
                }

                var inLine = frames.Where(f => f.Time >= start && f.Time < end).ToList();
                if (inLine.Count == 0)
                {
                    continue;
                }

                linesExpected++;
                if (inLine.Count(f => f.IsVoiced) >= 0.25 * inLine.Count)
                {
                    linesOnCue++;
                }
            }

            if (linesExpected > 0)
            {
                return (double)linesOnCue / linesExpected;
            }
        }

        return duration <= 0 ? 0 : Clamp01(voicedSeconds / duration / 0.4);
    }

    private static double ScoreRange(double spanSemitones, IReadOnlyList<ReferenceNote>? melody)
    {
        if (melody is { Count: > 0 })
        {
            var melodySpan = melody.Max(n => n.Midi) - melody.Min(n => n.Midi);
            return Clamp01(spanSemitones / Math.Max(melodySpan, 5));
        }

        return Clamp01(spanSemitones / 19.0);
    }

    private static double ScoreDynamics(IReadOnlyList<PitchFrame> voiced)
    {
        var decibels = voiced.Select(f => 20.0 * Math.Log10(Math.Max(f.Rms, 1e-6))).ToList();
        var mean = decibels.Average();
        var deviation = Math.Sqrt(decibels.Average(db => (db - mean) * (db - mean)));

        if (deviation < 2.5)
        {
            return 0.6 + (0.4 * deviation / 2.5);
        }

        return deviation <= 8.0 ? 1.0 : Clamp01(1.0 - ((deviation - 8.0) / 10.0));
    }

    private static bool DetectVibrato(IReadOnlyList<List<PitchFrame>> segments, double frameSeconds)
    {
        var minimumFrames = (int)Math.Ceiling(0.6 / frameSeconds);
        foreach (var segment in segments.Where(s => s.Count >= minimumFrames))
        {
            var n = segment.Count;
            var times = segment.Select(f => f.Time).ToArray();
            var values = segment.Select(f => f.Midi).ToArray();
            var meanT = times.Average();
            var meanV = values.Average();
            var covariance = 0.0;
            var variance = 0.0;
            for (var i = 0; i < n; i++)
            {
                covariance += (times[i] - meanT) * (values[i] - meanV);
                variance += (times[i] - meanT) * (times[i] - meanT);
            }

            var slope = variance > 0 ? covariance / variance : 0;
            var detrended = new double[n];
            for (var i = 0; i < n; i++)
            {
                detrended[i] = values[i] - (meanV + (slope * (times[i] - meanT)));
            }

            var crossings = 0;
            for (var i = 1; i < n; i++)
            {
                if (Math.Sign(detrended[i]) != Math.Sign(detrended[i - 1]))
                {
                    crossings++;
                }
            }

            var seconds = times[^1] - times[0];
            var rate = crossings / 2.0 / seconds;
            var extentCents = Math.Sqrt(2.0 * detrended.Average(v => v * v)) * 100.0;
            if (rate is >= 4.0 and <= 8.0 && extentCents is >= 15.0 and <= 150.0)
            {
                return true;
            }
        }

        return false;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        var index = (int)Math.Round(percentile * (sorted.Count - 1));
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0.0, 1.0);
}
