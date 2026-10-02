namespace MyasMusicChallenge.Core.Audio;

public static class PitchMath
{
    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static double FrequencyToMidi(double frequency)
    {
        if (frequency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be positive.");
        }

        return 69.0 + 12.0 * Math.Log2(frequency / 440.0);
    }

    public static double MidiToFrequency(double midi) => 440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0);

    /// <summary>Signed distance in cents from <paramref name="midi"/> to the nearest equal-tempered semitone.</summary>
    public static double CentsFromNearestSemitone(double midi) => (midi - Math.Round(midi)) * 100.0;

    /// <summary>Octave-agnostic signed distance in cents between two pitches, in the range [-600, 600).</summary>
    public static double PitchClassDistanceCents(double sungMidi, double targetMidi)
    {
        var semitones = (sungMidi - targetMidi) % 12.0;
        if (semitones >= 6.0)
        {
            semitones -= 12.0;
        }
        else if (semitones < -6.0)
        {
            semitones += 12.0;
        }

        return semitones * 100.0;
    }

    public static int PitchClass(double midi) => (((int)Math.Round(midi) % 12) + 12) % 12;

    public static string PitchClassName(int pitchClass) => NoteNames[((pitchClass % 12) + 12) % 12];

    public static string NoteName(double midi)
    {
        var rounded = (int)Math.Round(midi);
        var octave = (rounded / 12) - 1;
        return $"{PitchClassName(rounded)}{octave}";
    }
}
