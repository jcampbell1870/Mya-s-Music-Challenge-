namespace MyasMusicChallenge.Core.Audio;

public readonly record struct PitchEstimate(double Frequency, double Confidence)
{
    public static PitchEstimate Unvoiced { get; } = new(0, 0);

    public bool IsVoiced => Frequency > 0;
}

/// <summary>
/// Monophonic pitch detector based on the YIN algorithm (de Cheveigné &amp; Kawahara, 2002),
/// tuned for the human singing voice.
/// </summary>
/// <remarks>Instances reuse internal buffers and are not thread-safe.</remarks>
public sealed class YinPitchDetector
{
    private readonly int _sampleRate;
    private readonly int _frameSize;
    private readonly int _tauMin;
    private readonly int _tauMax;
    private readonly double _threshold;
    private readonly double[] _difference;

    public YinPitchDetector(
        int sampleRate,
        int frameSize,
        double minFrequency = 70.0,
        double maxFrequency = 1100.0,
        double threshold = 0.15)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (minFrequency <= 0 || maxFrequency <= minFrequency)
        {
            throw new ArgumentOutOfRangeException(nameof(minFrequency), "Frequency range is invalid.");
        }

        _sampleRate = sampleRate;
        _frameSize = frameSize;
        _threshold = threshold;
        _tauMin = Math.Max(2, (int)Math.Floor(sampleRate / maxFrequency));
        _tauMax = (int)Math.Ceiling(sampleRate / minFrequency);

        if (_tauMax * 2 > frameSize)
        {
            throw new ArgumentException(
                $"Frame size {frameSize} is too small to detect {minFrequency} Hz at {sampleRate} Hz.",
                nameof(frameSize));
        }

        _difference = new double[_tauMax + 2];
    }

    public int FrameSize => _frameSize;

    public PitchEstimate Detect(ReadOnlySpan<float> frame)
    {
        if (frame.Length < _frameSize)
        {
            throw new ArgumentException($"Frame must contain at least {_frameSize} samples.", nameof(frame));
        }

        var window = _frameSize - _tauMax - 1;
        var d = _difference;

        // Difference function.
        d[0] = 0;
        for (var tau = 1; tau <= _tauMax + 1; tau++)
        {
            double sum = 0;
            for (var j = 0; j < window; j++)
            {
                double delta = frame[j] - frame[j + tau];
                sum += delta * delta;
            }

            d[tau] = sum;
        }

        // Cumulative mean normalized difference.
        d[0] = 1;
        double runningSum = 0;
        for (var tau = 1; tau <= _tauMax + 1; tau++)
        {
            runningSum += d[tau];
            d[tau] = runningSum <= 0 ? 1 : d[tau] * tau / runningSum;
        }

        // Absolute threshold, then walk down to the local minimum.
        var tauEstimate = -1;
        for (var tau = _tauMin; tau <= _tauMax; tau++)
        {
            if (d[tau] < _threshold)
            {
                while (tau + 1 <= _tauMax && d[tau + 1] < d[tau])
                {
                    tau++;
                }

                tauEstimate = tau;
                break;
            }
        }

        if (tauEstimate < 0)
        {
            return PitchEstimate.Unvoiced;
        }

        // Parabolic interpolation for sub-sample accuracy.
        double betterTau = tauEstimate;
        var s0 = d[tauEstimate - 1];
        var s1 = d[tauEstimate];
        var s2 = d[tauEstimate + 1];
        var denominator = s0 + s2 - (2 * s1);
        if (Math.Abs(denominator) > double.Epsilon)
        {
            betterTau = tauEstimate + ((s0 - s2) / (2 * denominator));
        }

        var confidence = Math.Clamp(1.0 - d[tauEstimate], 0.0, 1.0);
        return new PitchEstimate(_sampleRate / betterTau, confidence);
    }

    public static double Rms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0;
        }

        double sum = 0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples.Length);
    }
}
