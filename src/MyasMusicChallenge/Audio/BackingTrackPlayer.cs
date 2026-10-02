using NAudio.Wave;

namespace MyasMusicChallenge.Audio;

/// <summary>Plays a licensed instrumental/backing track installed by the player.</summary>
public sealed class BackingTrackPlayer : IDisposable
{
    private readonly AudioFileReader _reader;
    private readonly WaveOutEvent _output;

    public BackingTrackPlayer(string path)
    {
        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent { DesiredLatency = 150 };
        _output.Init(_reader);
    }

    public TimeSpan Duration => _reader.TotalTime;

    public bool IsFinished => _output.PlaybackState == PlaybackState.Stopped && _reader.Position >= _reader.Length;

    public static BackingTrackPlayer? TryOpen(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return new BackingTrackPlayer(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException
                                       or FormatException or NAudio.MmException or System.Runtime.InteropServices.COMException)
        {
            error = $"Couldn't play the backing track: {ex.Message}";
            return null;
        }
    }

    public void Play() => _output.Play();

    public void Stop() => _output.Stop();

    public void Dispose()
    {
        _output.Dispose();
        _reader.Dispose();
    }
}
