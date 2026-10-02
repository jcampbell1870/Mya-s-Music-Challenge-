using MyasMusicChallenge.Core.Audio;
using NAudio.Wave;

namespace MyasMusicChallenge.Audio;

/// <summary>Captures 44.1 kHz mono audio from a Windows recording device and feeds the vocal analyzer.</summary>
public sealed class MicrophoneInput : IDisposable
{
    public const int SampleRate = 44100;

    private readonly WaveInEvent _waveIn;
    private float[] _scratch = new float[4096];
    private bool _recording;

    public MicrophoneInput(int deviceNumber, VocalAnalyzer analyzer)
    {
        Analyzer = analyzer;
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(SampleRate, 16, 1),
            BufferMilliseconds = 40,
            NumberOfBuffers = 3,
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null)
            {
                LastError = e.Exception.Message;
            }
        };
    }

    public VocalAnalyzer Analyzer { get; }

    public string? LastError { get; private set; }

    public static IReadOnlyList<string> GetDeviceNames()
    {
        var names = new List<string>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            names.Add(WaveInEvent.GetCapabilities(i).ProductName);
        }

        return names;
    }

    public static int ClampDevice(int deviceNumber)
    {
        var count = WaveInEvent.DeviceCount;
        return count == 0 ? 0 : Math.Clamp(deviceNumber, 0, count - 1);
    }

    /// <summary>Starts recording. Returns false (with <see cref="LastError"/>) if no microphone is available.</summary>
    public bool Start()
    {
        if (_recording)
        {
            return true;
        }

        if (WaveInEvent.DeviceCount == 0)
        {
            LastError = "No microphone found. Plug one in and press M to choose it.";
            return false;
        }

        try
        {
            _waveIn.StartRecording();
            _recording = true;
            return true;
        }
        catch (Exception ex) when (ex is NAudio.MmException or InvalidOperationException)
        {
            LastError = $"Microphone error: {ex.Message}";
            return false;
        }
    }

    public void Stop()
    {
        if (_recording)
        {
            _recording = false;
            _waveIn.StopRecording();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var count = e.BytesRecorded / 2;
        if (_scratch.Length < count)
        {
            _scratch = new float[count];
        }

        for (var i = 0; i < count; i++)
        {
            _scratch[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        }

        Analyzer.AddSamples(_scratch.AsSpan(0, count));
    }

    public void Dispose()
    {
        Stop();
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
    }
}
