using System.Net;
using System.Text;
using MyasMusicChallenge.Core.Audio;

namespace MyasMusicChallenge.Tests.TestUtilities;

internal static class Signals
{
    public static float[] Sine(double frequency, int sampleRate, double seconds, double amplitude = 0.5)
    {
        var samples = new float[(int)(sampleRate * seconds)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
        }

        return samples;
    }

    /// <summary>A voice-like tone with harmonics, which is harder for pitch detectors than a pure sine.</summary>
    public static float[] Voice(double frequency, int sampleRate, double seconds, double amplitude = 0.4)
    {
        var samples = new float[(int)(sampleRate * seconds)];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = (double)i / sampleRate;
            samples[i] = (float)(amplitude * (
                Math.Sin(2 * Math.PI * frequency * t)
                + (0.5 * Math.Sin(2 * Math.PI * 2 * frequency * t))
                + (0.25 * Math.Sin(2 * Math.PI * 3 * frequency * t))) / 1.75);
        }

        return samples;
    }

    public static float[] Concat(params float[][] parts) => parts.SelectMany(p => p).ToArray();

    public static float[] Noise(int count, double amplitude, int seed = 7)
    {
        var random = new Random(seed);
        var samples = new float[count];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(((random.NextDouble() * 2) - 1) * amplitude);
        }

        return samples;
    }
}

/// <summary>Builds synthetic pitch tracks at the analyzer's native frame rate.</summary>
internal sealed class PitchTrackBuilder
{
    public const double FrameSeconds = VocalAnalyzer.DefaultHopSize / 44100.0;

    private readonly List<PitchFrame> _frames = [];
    private double _time;

    public PitchTrackBuilder Note(double midi, double seconds, double rms = 0.1, double centsJitter = 0, double vibratoCents = 0, double vibratoHz = 5.5, Random? random = null)
    {
        var count = (int)Math.Round(seconds / FrameSeconds);
        var start = _time;
        for (var i = 0; i < count; i++)
        {
            var t = _time - start;
            var cents = vibratoCents * Math.Sin(2 * Math.PI * vibratoHz * t);
            if (centsJitter > 0 && random is not null)
            {
                cents += ((random.NextDouble() * 2) - 1) * centsJitter;
            }

            _frames.Add(new PitchFrame(_time, PitchMath.MidiToFrequency(midi + (cents / 100.0)), 0.95, rms));
            _time += FrameSeconds;
        }

        return this;
    }

    public PitchTrackBuilder Rest(double seconds)
    {
        var count = (int)Math.Round(seconds / FrameSeconds);
        for (var i = 0; i < count; i++)
        {
            _frames.Add(new PitchFrame(_time, 0, 0, 0.001));
            _time += FrameSeconds;
        }

        return this;
    }

    public double Duration => _time;

    public IReadOnlyList<PitchFrame> Build() => _frames;
}

internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    public StubHttpHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return _responder(request, body);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
