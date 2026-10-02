namespace MyasMusicChallenge.Core.Audio;

/// <summary>One analysis frame of the singer's voice.</summary>
/// <param name="Time">Frame center time in seconds since the performance started.</param>
/// <param name="Frequency">Detected fundamental frequency in Hz, or 0 when no singing was detected.</param>
/// <param name="Confidence">Pitch confidence between 0 and 1.</param>
/// <param name="Rms">Root-mean-square level of the frame (0..1).</param>
public readonly record struct PitchFrame(double Time, double Frequency, double Confidence, double Rms)
{
    public bool IsVoiced => Frequency > 0;

    public double Midi => IsVoiced ? PitchMath.FrequencyToMidi(Frequency) : 0;
}

/// <summary>
/// Streams microphone samples into overlapping frames and records a pitch track of the performance.
/// Safe to feed from an audio callback thread while the UI thread reads snapshots.
/// </summary>
public sealed class VocalAnalyzer
{
    public const int DefaultFrameSize = 2048;
    public const int DefaultHopSize = 1024;
    public const double DefaultSilenceRms = 0.01;

    private readonly object _gate = new();
    private readonly YinPitchDetector _detector;
    private readonly float[] _buffer;
    private readonly int _hopSize;
    private readonly double _silenceRms;
    private readonly List<PitchFrame> _frames = [];
    private int _buffered;
    private long _frameIndex;
    private double _inputLevel;

    public VocalAnalyzer(
        int sampleRate,
        int frameSize = DefaultFrameSize,
        int hopSize = DefaultHopSize,
        double silenceRms = DefaultSilenceRms)
    {
        if (hopSize <= 0 || hopSize > frameSize)
        {
            throw new ArgumentOutOfRangeException(nameof(hopSize));
        }

        SampleRate = sampleRate;
        _detector = new YinPitchDetector(sampleRate, frameSize);
        _buffer = new float[frameSize];
        _hopSize = hopSize;
        _silenceRms = silenceRms;
    }

    public int SampleRate { get; }

    public int FrameSize => _buffer.Length;

    /// <summary>Most recent input level (RMS 0..1), useful for a microphone meter.</summary>
    public double InputLevel
    {
        get
        {
            lock (_gate)
            {
                return _inputLevel;
            }
        }
    }

    public PitchFrame? LatestFrame
    {
        get
        {
            lock (_gate)
            {
                return _frames.Count == 0 ? null : _frames[^1];
            }
        }
    }

    public void AddSamples(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            while (!samples.IsEmpty)
            {
                var toCopy = Math.Min(samples.Length, _buffer.Length - _buffered);
                samples[..toCopy].CopyTo(_buffer.AsSpan(_buffered));
                _buffered += toCopy;
                samples = samples[toCopy..];

                if (_buffered == _buffer.Length)
                {
                    AnalyzeFrame();
                    Array.Copy(_buffer, _hopSize, _buffer, 0, _buffer.Length - _hopSize);
                    _buffered -= _hopSize;
                }
            }
        }
    }

    public IReadOnlyList<PitchFrame> GetFrames()
    {
        lock (_gate)
        {
            return _frames.ToArray();
        }
    }

    /// <summary>Returns frames whose time is at or after <paramref name="fromTime"/>.</summary>
    public IReadOnlyList<PitchFrame> GetFramesSince(double fromTime)
    {
        lock (_gate)
        {
            var start = _frames.Count;
            while (start > 0 && _frames[start - 1].Time >= fromTime)
            {
                start--;
            }

            return _frames.GetRange(start, _frames.Count - start).ToArray();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _frames.Clear();
            _buffered = 0;
            _frameIndex = 0;
            _inputLevel = 0;
        }
    }

    private void AnalyzeFrame()
    {
        var span = _buffer.AsSpan();
        var rms = YinPitchDetector.Rms(span);
        var time = ((_frameIndex * (double)_hopSize) + (_buffer.Length / 2.0)) / SampleRate;
        _frameIndex++;
        _inputLevel = rms;

        var estimate = rms >= _silenceRms ? _detector.Detect(span) : PitchEstimate.Unvoiced;
        _frames.Add(new PitchFrame(time, estimate.Frequency, estimate.Confidence, rms));
    }
}
