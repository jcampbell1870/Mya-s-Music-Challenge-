using MyasMusicChallenge.Core.Audio;
using MyasMusicChallenge.Tests.TestUtilities;

namespace MyasMusicChallenge.Tests.Audio;

public class PitchDetectionTests
{
    private const int SampleRate = 44100;

    [Theory]
    [InlineData(440.0, 69)]
    [InlineData(261.6256, 60)]
    [InlineData(110.0, 45)]
    public void FrequencyToMidi_MapsStandardPitches(double frequency, int midi)
    {
        Assert.Equal(midi, PitchMath.FrequencyToMidi(frequency), 3);
        Assert.Equal(frequency, PitchMath.MidiToFrequency(midi), 2);
    }

    [Fact]
    public void NoteName_UsesScientificPitchNotation()
    {
        Assert.Equal("A4", PitchMath.NoteName(69));
        Assert.Equal("C4", PitchMath.NoteName(60.2));
        Assert.Equal("C#5", PitchMath.NoteName(73));
    }

    [Theory]
    [InlineData(72, 60, 0)]
    [InlineData(60.5, 60, 50)]
    [InlineData(59, 72, -100)]
    public void PitchClassDistance_IsOctaveAgnostic(double sung, double target, double expectedCents)
    {
        Assert.Equal(expectedCents, PitchMath.PitchClassDistanceCents(sung, target), 6);
    }

    [Theory]
    [InlineData(98.0)]
    [InlineData(196.0)]
    [InlineData(261.63)]
    [InlineData(440.0)]
    [InlineData(880.0)]
    public void Yin_DetectsVoiceLikeTones(double frequency)
    {
        var detector = new YinPitchDetector(SampleRate, VocalAnalyzer.DefaultFrameSize);
        var samples = Signals.Voice(frequency, SampleRate, 0.1);

        var estimate = detector.Detect(samples);

        Assert.True(estimate.IsVoiced);
        Assert.InRange(estimate.Frequency, frequency * 0.99, frequency * 1.01);
        Assert.True(estimate.Confidence > 0.8);
    }

    [Fact]
    public void Yin_ReportsNoiseAsUnvoiced()
    {
        var detector = new YinPitchDetector(SampleRate, VocalAnalyzer.DefaultFrameSize);

        var estimate = detector.Detect(Signals.Noise(VocalAnalyzer.DefaultFrameSize, 0.3));

        Assert.False(estimate.IsVoiced);
    }

    [Fact]
    public void Yin_RejectsFrameSizeTooSmallForMinimumFrequency()
    {
        Assert.Throws<ArgumentException>(() => new YinPitchDetector(SampleRate, 512, minFrequency: 70));
    }

    [Fact]
    public void Analyzer_TracksPitchAcrossArbitraryChunkSizes()
    {
        var analyzer = new VocalAnalyzer(SampleRate);
        var samples = Signals.Concat(
            Signals.Voice(220, SampleRate, 1.0),
            new float[SampleRate / 2],
            Signals.Voice(330, SampleRate, 1.0));

        for (var offset = 0; offset < samples.Length; offset += 441)
        {
            analyzer.AddSamples(samples.AsSpan(offset, Math.Min(441, samples.Length - offset)));
        }

        var frames = analyzer.GetFrames();
        var expectedFrames = ((samples.Length - VocalAnalyzer.DefaultFrameSize) / VocalAnalyzer.DefaultHopSize) + 1;
        Assert.Equal(expectedFrames, frames.Count);

        var first = frames.Where(f => f.Time is > 0.1 and < 0.9).ToList();
        var silent = frames.Where(f => f.Time is > 1.1 and < 1.4).ToList();
        var second = frames.Where(f => f.Time is > 1.7 and < 2.4).ToList();

        Assert.All(first, f => Assert.InRange(f.Frequency, 217, 223));
        Assert.All(silent, f => Assert.False(f.IsVoiced));
        Assert.All(second, f => Assert.InRange(f.Frequency, 326, 334));
        Assert.True(frames.Zip(frames.Skip(1)).All(p => p.Second.Time > p.First.Time));
    }

    [Fact]
    public void Analyzer_ResetClearsFramesAndSinceFiltersByTime()
    {
        var analyzer = new VocalAnalyzer(SampleRate);
        analyzer.AddSamples(Signals.Voice(262, SampleRate, 1.0));

        Assert.NotNull(analyzer.LatestFrame);
        Assert.True(analyzer.InputLevel > 0);
        Assert.All(analyzer.GetFramesSince(0.5), f => Assert.True(f.Time >= 0.5));
        Assert.True(analyzer.GetFramesSince(0.5).Count < analyzer.GetFrames().Count);

        analyzer.Reset();

        Assert.Empty(analyzer.GetFrames());
        Assert.Null(analyzer.LatestFrame);
    }
}
