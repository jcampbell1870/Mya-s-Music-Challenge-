using System.Diagnostics;
using System.Drawing.Drawing2D;
using MyasMusicChallenge.Audio;
using MyasMusicChallenge.Core.Assessment;
using MyasMusicChallenge.Core.Audio;
using MyasMusicChallenge.Core.Lyrics;
using MyasMusicChallenge.Core.Songs;
using MyasMusicChallenge.Rendering;

namespace MyasMusicChallenge.Scenes;

/// <summary>The karaoke stage: lyrics, scrolling pitch lane, live mic feedback and Mýa singing along.</summary>
public sealed class SingScene : Scene
{
    private const double CountdownSeconds = 3;
    private const double PastSeconds = 2.4;
    private const double FutureSeconds = 5.5;
    private const int VisibleSemitones = 24;
    private static readonly RectangleF Lane = new(330, 96, 860, 270);
    private static readonly float NowX = Lane.X + (float)(Lane.Width * PastSeconds / (PastSeconds + FutureSeconds));

    private readonly SongEntry _entry;
    private readonly VocalAnalyzer _analyzer = new(MicrophoneInput.SampleRate);
    private readonly Stopwatch _songClock = new();
    private readonly List<string> _warnings = [];
    private IReadOnlyList<LyricLine> _lyrics = [];
    private IReadOnlyList<ReferenceNote> _melody = [];
    private MicrophoneInput? _microphone;
    private BackingTrackPlayer? _backing;
    private double? _duration;
    private Phase _phase = Phase.Ready;
    private double _countdownStart;
    private double _laneCenter = 60;
    private double _smoothedLevel;

    public SingScene(GameForm game, SongEntry entry)
        : base(game)
    {
        _entry = entry;
    }

    private enum Phase
    {
        Ready,
        Countdown,
        Singing,
    }

    private double Elapsed => _songClock.Elapsed.TotalSeconds;

    public override void Enter()
    {
        try
        {
            _lyrics = SongLibrary.LoadLyrics(_entry);
            if (_entry.MelodyPath is { } melodyPath)
            {
                _melody = SongLibrary.LoadMelody(melodyPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _warnings.Add($"Couldn't read song files: {ex.Message}");
        }

        _backing = BackingTrackPlayer.TryOpen(_entry.BackingTrackPath, out var backingError);
        if (backingError is not null)
        {
            _warnings.Add(backingError);
        }

        _duration = _backing?.Duration.TotalSeconds
                    ?? _entry.Song.DurationSeconds
                    ?? (_lyrics.Count > 0 ? _lyrics[^1].Start.TotalSeconds + 5 : null);

        if (_melody.Count > 0)
        {
            _laneCenter = _melody.Average(n => n.Midi);
        }

        _microphone = new MicrophoneInput(MicrophoneInput.ClampDevice(Context.Profile.MicrophoneDeviceNumber), _analyzer);

        if (_backing is not null)
        {
            StartCountdown();
        }
    }

    public override void Leave()
    {
        _microphone?.Dispose();
        _microphone = null;
        _backing?.Dispose();
        _backing = null;
    }

    private void StartCountdown()
    {
        _phase = Phase.Countdown;
        _countdownStart = Time;
    }

    public override void Update(double deltaSeconds)
    {
        base.Update(deltaSeconds);
        _smoothedLevel += (Math.Min(1, _analyzer.InputLevel * 8) - _smoothedLevel) * Math.Min(1, deltaSeconds * 12);

        if (_phase == Phase.Countdown && Time - _countdownStart >= CountdownSeconds)
        {
            BeginSinging();
        }
        else if (_phase == Phase.Singing)
        {
            if (_analyzer.LatestFrame is { IsVoiced: true } frame && _melody.Count == 0)
            {
                _laneCenter += (frame.Midi - _laneCenter) * Math.Min(1, deltaSeconds * 0.8);
            }

            if ((_duration is { } duration && Elapsed >= duration) || _backing?.IsFinished == true)
            {
                Finish();
            }
        }
    }

    private void BeginSinging()
    {
        _analyzer.Reset();
        if (_microphone?.Start() != true)
        {
            _warnings.Add(_microphone?.LastError ?? "Microphone unavailable.");
        }

        _backing?.Play();
        _songClock.Restart();
        _phase = Phase.Singing;
    }

    private void Finish()
    {
        var elapsed = Elapsed;
        _songClock.Stop();
        _microphone?.Stop();
        _backing?.Stop();
        var frames = _analyzer.GetFrames();
        var context = new AssessmentContext(
            _entry.Song.DisplayTitle,
            elapsed,
            _lyrics.Count > 0 ? _lyrics : null,
            _melody.Count > 0 ? _melody : null,
            Context.Profile.PlayerName);
        var assessment = Context.Assessor.Assess(frames, context);
        Game.ChangeScene(new ResultsScene(Game, _entry, assessment, elapsed));
    }

    public override void OnKey(Keys key)
    {
        switch (key)
        {
            case Keys.Space when _phase == Phase.Ready:
                StartCountdown();
                break;
            case Keys.S when _entry.HasSpotify && _phase != Phase.Singing:
                GameContext.OpenInSpotify(_entry.Song);
                break;
            case Keys.Enter when _phase == Phase.Singing:
                Finish();
                break;
            case Keys.Escape:
                Game.ChangeScene(new SongSelectScene(Game));
                break;
        }
    }

    public override void Draw(Graphics g)
    {
        Stage.Draw(g, Time, (float)_smoothedLevel);
        var singing = _phase == Phase.Singing;
        var mouth = singing ? (float)Math.Clamp(_smoothedLevel * 1.4 + 0.15 * Math.Abs(Math.Sin(Time * 9)), 0, 1) : 0f;
        MyaSprite.Draw(g, 165, 610, 1.0f, singing ? MyaPose.Sing : MyaPose.Talk, Time, mouth);

        DrawTopBar(g);
        DrawPitchLane(g);
        DrawLyrics(g);
        DrawMicMeter(g);

        for (var i = 0; i < _warnings.Count; i++)
        {
            Ui.FillRounded(g, Color.FromArgb(220, 150, 20, 40), new RectangleF(330, 590 - i * 40, 900, 34), 10);
            Ui.Text(g, _warnings[i], Ui.Small, Color.White, new RectangleF(342, 590 - i * 40, 880, 34), Ui.Left, shadow: false);
        }

        if (_phase == Phase.Ready)
        {
            DrawReadyPanel(g);
        }
        else if (_phase == Phase.Countdown)
        {
            var remaining = (int)Math.Ceiling(CountdownSeconds - (Time - _countdownStart));
            var pulse = (float)(1 - (Time - _countdownStart) % 1);
            using var font = new Font(Ui.Huge.FontFamily, 90 + 60 * pulse, FontStyle.Bold, GraphicsUnit.Pixel);
            Ui.Text(g, remaining.ToString(), font, Ui.Gold, new RectangleF(330, 120, 860, 300), Ui.Center);
        }

        Ui.KeyHints(g, _phase switch
        {
            Phase.Singing => "ENTER finish & get Mýa's assessment   •   ESC quit song",
            _ => "SPACE start   •   S open in Spotify   •   ESC back to songs",
        });
    }

    private void DrawTopBar(Graphics g)
    {
        Ui.Text(g, _entry.Song.DisplayTitle, Ui.Heading, Color.White, new RectangleF(330, 16, 700, 40), Ui.Left);
        var clock = _duration is { } d ? $"{Ui.Clock(Elapsed)} / {Ui.Clock(d)}" : Ui.Clock(Elapsed);
        Ui.Text(g, clock, Ui.Heading, Ui.Gold, new RectangleF(1030, 16, 210, 40), Ui.Right);
        Ui.Bar(g, new RectangleF(330, 62, 910, 10), _duration is { } total && total > 0 ? Elapsed / total : 0, Ui.Teal);
    }

    private void DrawPitchLane(Graphics g)
    {
        Ui.FillRounded(g, Color.FromArgb(170, 10, 4, 26), Lane, 14);
        var state = g.Save();
        g.SetClip(Lane);

        var low = Math.Round(_laneCenter) - VisibleSemitones / 2.0;
        float YFor(double midi) => (float)(Lane.Bottom - (midi - low) / VisibleSemitones * Lane.Height);
        float XFor(double t) => NowX + (float)((t - Elapsed) * Lane.Width / (PastSeconds + FutureSeconds));

        using (var linePen = new Pen(Color.FromArgb(30, 255, 255, 255)))
        using (var cPen = new Pen(Color.FromArgb(70, 255, 255, 255)))
        {
            for (var m = (int)Math.Ceiling(low); m <= low + VisibleSemitones; m++)
            {
                var y = YFor(m);
                var isC = PitchMath.PitchClass(m) == 0;
                g.DrawLine(isC ? cPen : linePen, Lane.X, y, Lane.Right, y);
                if (isC)
                {
                    Ui.Text(g, PitchMath.NoteName(m), Ui.Small, Ui.Muted, Lane.X + 6, y - 20, shadow: false);
                }
            }
        }

        var now = Elapsed;
        ReferenceNote? target = null;
        foreach (var note in _melody)
        {
            if (note.End < now - PastSeconds || note.Start > now + FutureSeconds)
            {
                continue;
            }

            var active = note.Start <= now && note.End >= now;
            if (active)
            {
                target = note;
            }

            var r = new RectangleF(XFor(note.Start), YFor(note.Midi + 0.5), Math.Max(4, XFor(note.End) - XFor(note.Start)), Lane.Height / VisibleSemitones);
            Ui.FillRounded(g, active ? Ui.Gold : Color.FromArgb(150, Ui.Purple), r, 5);
        }

        using (var nowPen = new Pen(Color.FromArgb(200, Ui.Pink), 3))
        {
            g.DrawLine(nowPen, NowX, Lane.Y, NowX, Lane.Bottom);
        }

        var frames = _phase == Phase.Singing ? _analyzer.GetFramesSince(Math.Max(0, now - PastSeconds)) : [];
        using (var dot = new SolidBrush(Ui.Teal))
        {
            foreach (var frame in frames)
            {
                if (!frame.IsVoiced)
                {
                    continue;
                }

                var midi = FoldToTarget(frame.Midi, NoteAt(frame.Time)?.Midi ?? _laneCenter);
                g.FillEllipse(dot, XFor(frame.Time) - 4, YFor(midi) - 4, 8, 8);
            }
        }

        g.Restore(state);
        DrawLiveFeedback(g, target);
    }

    private ReferenceNote? NoteAt(double time)
    {
        foreach (var note in _melody)
        {
            if (note.Start <= time && note.End >= time)
            {
                return note;
            }
        }

        return null;
    }

    private static double FoldToTarget(double midi, double target) => midi + 12 * Math.Round((target - midi) / 12);

    private void DrawLiveFeedback(Graphics g, ReferenceNote? target)
    {
        if (_phase != Phase.Singing || _analyzer.LatestFrame is not { IsVoiced: true } frame)
        {
            return;
        }

        var cents = target is null
            ? Math.Abs(PitchMath.CentsFromNearestSemitone(frame.Midi))
            : PitchMath.PitchClassDistanceCents(frame.Midi, target.Midi);
        var (label, color) = cents switch
        {
            <= 15 => ("PERFECT!", Ui.Gold),
            <= 30 => ("GREAT", Ui.Teal),
            <= 50 => ("GOOD", Color.White),
            _ => ("FIND THE NOTE", Ui.Pink),
        };
        Ui.Text(g, $"{PitchMath.NoteName(frame.Midi)}  {label}", Ui.Heading, color, new RectangleF(Lane.X, Lane.Bottom + 4, Lane.Width, 36), Ui.Right);
    }

    private void DrawLyrics(Graphics g)
    {
        var box = new RectangleF(330, 410, 860, 170);
        Ui.FillRounded(g, Color.FromArgb(150, 10, 4, 26), box, 18);

        if (_lyrics.Count == 0)
        {
            Ui.Text(g, "No lyrics file installed — sing it from the heart!", Ui.Heading, Color.White, new RectangleF(box.X, box.Y + 20, box.Width, 60), Ui.Center);
            Ui.Text(g, $"Add your licensed synced lyrics as Songs\\{_entry.Song.Id}\\lyrics.lrc", Ui.Body, Ui.Muted, new RectangleF(box.X, box.Y + 90, box.Width, 40), Ui.Center);
            return;
        }

        var position = TimeSpan.FromSeconds(_phase == Phase.Singing ? Elapsed : 0);
        var index = LrcParser.FindCurrentLine(_lyrics, position);
        var currentRect = new RectangleF(box.X + 20, box.Y + 22, box.Width - 40, 70);
        var nextRect = new RectangleF(box.X + 20, box.Y + 100, box.Width - 40, 50);

        if (index < 0)
        {
            var untilFirst = _lyrics[0].Start.TotalSeconds - position.TotalSeconds;
            var intro = untilFirst <= 3 && _phase == Phase.Singing ? $"Get ready… {Math.Ceiling(untilFirst)}" : "♪ Intro ♪";
            Ui.Text(g, intro, Ui.Lyric, Ui.Muted, currentRect, Ui.Center);
            Ui.Text(g, _lyrics[0].Text, Ui.LyricNext, Ui.Muted, nextRect, Ui.Center);
            return;
        }

        var line = _lyrics[index];
        var lineEnd = index + 1 < _lyrics.Count ? _lyrics[index + 1].Start.TotalSeconds : line.Start.TotalSeconds + 4;
        var progress = Math.Clamp((position.TotalSeconds - line.Start.TotalSeconds) / Math.Max(0.5, (lineEnd - line.Start.TotalSeconds) * 0.85), 0, 1);
        DrawKaraokeLine(g, line.Text, currentRect, progress);
        if (index + 1 < _lyrics.Count)
        {
            Ui.Text(g, _lyrics[index + 1].Text, Ui.LyricNext, Ui.Muted, nextRect, Ui.Center);
        }
    }

    private static void DrawKaraokeLine(Graphics g, string text, RectangleF rect, double progress)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Ui.Text(g, "♪ ♪ ♪", Ui.Lyric, Ui.Muted, rect, Ui.Center);
            return;
        }

        Ui.Text(g, text, Ui.Lyric, Color.White, rect, Ui.Center);
        var size = g.MeasureString(text, Ui.Lyric, (int)rect.Width, Ui.Center);
        var left = rect.X + (rect.Width - size.Width) / 2;
        var state = g.Save();
        g.SetClip(new RectangleF(left, rect.Y, (float)(size.Width * progress), rect.Height), CombineMode.Intersect);
        using (var brush = new LinearGradientBrush(rect, Ui.Pink, Ui.Gold, LinearGradientMode.Vertical))
        {
            g.DrawString(text, Ui.Lyric, brush, rect, Ui.Center);
        }

        g.Restore(state);
    }

    private void DrawMicMeter(Graphics g)
    {
        var r = new RectangleF(1206, 96, 34, 484);
        Ui.FillRounded(g, Color.FromArgb(170, 10, 4, 26), r, 12);
        var h = (float)(r.Height - 16) * (float)_smoothedLevel;
        using var brush = new LinearGradientBrush(r, Ui.Pink, Ui.Teal, LinearGradientMode.Vertical);
        g.FillRectangle(brush, r.X + 8, r.Bottom - 8 - h, r.Width - 16, h);
        Ui.Text(g, "MIC", Ui.Small, Ui.Muted, new RectangleF(r.X - 8, r.Bottom + 2, r.Width + 16, 20), Ui.Center, shadow: false);
    }

    private void DrawReadyPanel(Graphics g)
    {
        var panel = new RectangleF(330, 120, 860, 260);
        Ui.FillRounded(g, Color.FromArgb(235, 22, 14, 48), panel, 18);
        Ui.OutlineRounded(g, Ui.Pink, 3, panel, 18);
        Ui.Text(g, $"Ready, {Context.Profile.PlayerName}? Let's hear those vocals!", Ui.Heading, Color.White, new RectangleF(panel.X, panel.Y + 18, panel.Width, 40), Ui.Center);
        var lines = new List<string> { "No backing track is installed for this song, so the music is up to you:" };
        if (_entry.HasSpotify)
        {
            lines.Add("• Press S to open it in Spotify, start playback, then press SPACE as the song begins.");
        }

        lines.Add("• Or press SPACE to sing it a cappella — Mýa loves a raw vocal!");
        lines.Add($"• Tip: add backing.mp3 + lyrics.lrc to Songs\\{_entry.Song.Id}\\ for full karaoke sync.");
        lines.Add("• Wear headphones so the mic only hears you.");
        for (var i = 0; i < lines.Count; i++)
        {
            Ui.Text(g, lines[i], Ui.Body, i == 0 ? Ui.Gold : Ui.Muted, panel.X + 30, panel.Y + 74 + i * 34, shadow: false);
        }
    }
}
