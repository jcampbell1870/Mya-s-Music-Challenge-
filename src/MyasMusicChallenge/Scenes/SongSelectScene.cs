using MyasMusicChallenge.Audio;
using MyasMusicChallenge.Core.Rewards;
using MyasMusicChallenge.Core.Songs;
using MyasMusicChallenge.Core.Spotify;
using MyasMusicChallenge.Rendering;

namespace MyasMusicChallenge.Scenes;

public sealed class SongSelectScene : Scene
{
    private const int RowHeight = 44;
    private const int VisibleRows = 10;

    private static int s_selected;
    private int _scroll;
    private string _status = string.Empty;
    private double _statusTime = -100;
    private bool _syncing;
    private IReadOnlyList<string> _microphones = [];

    public SongSelectScene(GameForm game)
        : base(game)
    {
    }

    public override void Enter()
    {
        _microphones = MicrophoneInput.GetDeviceNames();
        Context.Profile.MicrophoneDeviceNumber = MicrophoneInput.ClampDevice(Context.Profile.MicrophoneDeviceNumber);
        s_selected = Math.Clamp(s_selected, 0, Math.Max(0, Context.Songs.Count - 1));
        EnsureVisible();
        if (_microphones.Count == 0)
        {
            SetStatus("No microphone detected — plug one in, then press M.");
        }
    }

    private SongEntry? Selected => Context.Songs.Count == 0 ? null : Context.Songs[s_selected];

    public override void Draw(Graphics g)
    {
        Stage.Draw(g, Time, 0.2f, crowd: false);
        DrawHeader(g);
        DrawSongList(g);
        DrawDetails(g);

        if (Time - _statusTime < 6)
        {
            var alpha = (int)(255 * Math.Clamp(6 - (Time - _statusTime), 0, 1));
            Ui.FillRounded(g, Color.FromArgb(Math.Min(alpha, 220), 20, 10, 40), new RectangleF(40, 628, 1200, 40), 12);
            Ui.Text(g, _status, Ui.Body, Color.FromArgb(alpha, Color.White), new RectangleF(52, 628, 1180, 40), Ui.Left, shadow: false);
        }

        Ui.KeyHints(g, "↑↓ choose  •  ENTER sing  •  S Spotify  •  F5 sync Spotify  •  W wallet  •  N name  •  M mic  •  O songs folder  •  ESC back");
    }

    private void DrawHeader(Graphics g)
    {
        Ui.Text(g, "Choose a Mýa song", Ui.Title, Color.White, 40, 18);
        var profile = Context.Profile;
        var mic = _microphones.Count == 0 ? "none" : _microphones[profile.MicrophoneDeviceNumber];
        var earned = profile.History.Count(h => h.RewardIssued);
        var lines = new[]
        {
            $"Singer: {profile.PlayerName}",
            $"Wallet: {Ui.ShortAddress(profile.WalletAddress)}   •   {Context.Settings.Rewards.TokenSymbol} rewards earned: {earned}",
            $"Mic: {mic}",
        };
        for (var i = 0; i < lines.Length; i++)
        {
            Ui.Text(g, lines[i], Ui.Small, Ui.Muted, new RectangleF(640, 14 + i * 22, 600, 22), Ui.Right);
        }
    }

    private void DrawSongList(Graphics g)
    {
        var panel = new RectangleF(40, 90, 720, RowHeight * VisibleRows + 20);
        Ui.FillRounded(g, Ui.Panel, panel, 18);
        var songs = Context.Songs;
        for (var row = 0; row < VisibleRows && _scroll + row < songs.Count; row++)
        {
            var index = _scroll + row;
            var entry = songs[index];
            var r = new RectangleF(panel.X + 10, panel.Y + 10 + row * RowHeight, panel.Width - 20, RowHeight - 4);
            if (index == s_selected)
            {
                Ui.FillRounded(g, Color.FromArgb(200, Ui.Pink), r, 10);
            }

            Ui.Text(g, entry.Song.DisplayTitle, Ui.BodyBold, Color.White, new RectangleF(r.X + 12, r.Y, r.Width - 230, r.Height), Ui.LeftEllipsis, shadow: false);
            var tags = string.Join(" ", new[]
            {
                entry.HasLyrics ? "LYRICS" : null,
                entry.HasBackingTrack ? "TRACK" : null,
                entry.HasSpotify ? "SPOTIFY" : null,
            }.Where(t => t is not null));
            Ui.Text(g, tags, Ui.Small, index == s_selected ? Color.White : Ui.Teal, new RectangleF(r.Right - 210, r.Y, 200, r.Height), Ui.Right, shadow: false);
        }

        if (songs.Count > VisibleRows)
        {
            var track = new RectangleF(panel.Right - 8, panel.Y + 12, 4, panel.Height - 24);
            var thumbHeight = track.Height * VisibleRows / songs.Count;
            var thumbY = track.Y + (track.Height - thumbHeight) * _scroll / Math.Max(1, songs.Count - VisibleRows);
            Ui.FillRounded(g, Color.FromArgb(200, Color.White), new RectangleF(track.X, thumbY, 4, thumbHeight), 2);
        }
    }

    private void DrawDetails(Graphics g)
    {
        var panel = new RectangleF(780, 90, 460, RowHeight * VisibleRows + 20);
        Ui.FillRounded(g, Ui.Panel, panel, 18);
        if (Selected is not { } entry)
        {
            return;
        }

        var song = entry.Song;
        var y = panel.Y + 16;
        g.DrawString(song.Title, Ui.Heading, Brushes.White, new RectangleF(panel.X + 20, y, panel.Width - 40, 72), Ui.Wrap);
        y += 76;
        foreach (var line in new[]
                 {
                     song.Credit,
                     string.IsNullOrWhiteSpace(song.Album) ? null : $"{song.Album}{(song.Year is { } year ? $" ({year})" : string.Empty)}",
                     Context.Profile.BestScore(song.Id) is var best and > 0 ? $"Your best: {best}/100" : "Not sung yet — be the first!",
                 })
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                Ui.Text(g, line, Ui.Body, Ui.Muted, panel.X + 20, y, shadow: false);
                y += 28;
            }
        }

        y += 8;
        DrawCheck(g, panel.X + 20, ref y, entry.HasLyrics, "Synced lyrics (lyrics.lrc)");
        DrawCheck(g, panel.X + 20, ref y, entry.HasBackingTrack, "Backing track (backing.mp3/.wav)");
        DrawCheck(g, panel.X + 20, ref y, entry.HasMelody, "Melody guide (melody.json)");
        DrawCheck(g, panel.X + 20, ref y, entry.HasSpotify, "Playable on Spotify");

        MyaSprite.Draw(g, panel.Right - 62, panel.Bottom - 8, 0.4f, MyaPose.Idle, Time, 0f);
        using var pathBrush = new SolidBrush(Ui.Gold);
        g.DrawString($"Add files in:\nSongs\\{song.Id}\\", Ui.Small, pathBrush, new RectangleF(panel.X + 20, y + 6, panel.Width - 150, 60), Ui.Wrap);
    }

    private static void DrawCheck(Graphics g, float x, ref float y, bool ok, string label)
    {
        var box = new RectangleF(x, y + 4, 18, 18);
        Ui.FillRounded(g, ok ? Ui.Teal : Color.FromArgb(70, 255, 255, 255), box, 5);
        if (ok)
        {
            using var tick = new Pen(Ui.Night, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            g.DrawLines(tick, new PointF[] { new(box.X + 4, box.Y + 9), new(box.X + 8, box.Y + 13), new(box.X + 14, box.Y + 5) });
        }

        Ui.Text(g, label, Ui.Body, ok ? Color.White : Ui.Muted, x + 30, y, shadow: false);
        y += 30;
    }

    public override void OnKey(Keys key)
    {
        var count = Context.Songs.Count;
        switch (key)
        {
            case Keys.Up:
                Move(-1);
                break;
            case Keys.Down:
                Move(1);
                break;
            case Keys.PageUp:
                Move(-VisibleRows);
                break;
            case Keys.PageDown:
                Move(VisibleRows);
                break;
            case Keys.Home:
                Move(-count);
                break;
            case Keys.End:
                Move(count);
                break;
            case Keys.Enter or Keys.Space when Selected is { } entry:
                Context.SaveProfile();
                Game.ChangeScene(new SingScene(Game, entry));
                break;
            case Keys.S when Selected is { } entry:
                if (!entry.HasSpotify)
                {
                    SetStatus("No Spotify link for this song yet — press F5 to sync Mýa's discography.");
                }
                else if (!GameContext.OpenInSpotify(entry.Song))
                {
                    SetStatus("Couldn't open Spotify on this PC.");
                }

                break;
            case Keys.F5:
                SyncSpotify();
                break;
            case Keys.W:
                EditWallet();
                break;
            case Keys.N:
                EditName();
                break;
            case Keys.M:
                CycleMicrophone();
                break;
            case Keys.O:
                if (!GameContext.OpenExternal(Context.SongsDirectory))
                {
                    SetStatus($"Songs folder: {Context.SongsDirectory}");
                }

                break;
            case Keys.Escape:
                Game.ChangeScene(new TitleScene(Game));
                break;
        }
    }

    private void Move(int delta)
    {
        if (Context.Songs.Count == 0)
        {
            return;
        }

        s_selected = Math.Clamp(s_selected + delta, 0, Context.Songs.Count - 1);
        EnsureVisible();
    }

    private void EnsureVisible()
    {
        if (s_selected < _scroll)
        {
            _scroll = s_selected;
        }
        else if (s_selected >= _scroll + VisibleRows)
        {
            _scroll = s_selected - VisibleRows + 1;
        }

        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, Context.Songs.Count - VisibleRows));
    }

    private void SetStatus(string message)
    {
        _status = message;
        _statusTime = Time;
    }

    private void EditWallet()
    {
        var value = Game.Prompt(
            "Arcade1870 wallet",
            $"Your Ethereum wallet address (0x…) for {Context.Settings.Rewards.TokenSymbol} rewards. Leave empty to clear.",
            Context.Profile.WalletAddress,
            42);
        if (value is null)
        {
            return;
        }

        if (value.Length > 0 && !RewardClaimEncoder.IsValidAddress(value))
        {
            SetStatus("That doesn't look like a wallet address (0x followed by 40 hex characters).");
            return;
        }

        Context.Profile.WalletAddress = value;
        Context.SaveProfile();
        SetStatus(value.Length == 0 ? "Wallet cleared." : $"Wallet saved: {Ui.ShortAddress(value)}");
    }

    private void EditName()
    {
        var value = Game.Prompt("Singer name", "What should Mýa call you?", Context.Profile.PlayerName, 24);
        if (!string.IsNullOrWhiteSpace(value))
        {
            Context.Profile.PlayerName = value;
            Context.SaveProfile();
            SetStatus($"Nice to meet you, {value}!");
        }
    }

    private void CycleMicrophone()
    {
        _microphones = MicrophoneInput.GetDeviceNames();
        if (_microphones.Count == 0)
        {
            SetStatus("No microphone detected.");
            return;
        }

        Context.Profile.MicrophoneDeviceNumber = (Context.Profile.MicrophoneDeviceNumber + 1) % _microphones.Count;
        Context.SaveProfile();
        SetStatus($"Microphone: {_microphones[Context.Profile.MicrophoneDeviceNumber]}");
    }

    private async void SyncSpotify()
    {
        if (_syncing)
        {
            return;
        }

        if (!Context.Spotify.IsConfigured)
        {
            SetStatus($"Add Spotify ClientId/ClientSecret to appsettings.json (or {SpotifyOptions.ClientIdEnvironmentVariable}/{SpotifyOptions.ClientSecretEnvironmentVariable}).");
            return;
        }

        _syncing = true;
        SetStatus("Syncing Mýa's discography from Spotify…");
        var selectedId = Selected?.Song.Id;
        try
        {
            var count = await Context.SyncSpotifyAsync();
            var index = Context.Songs.ToList().FindIndex(e => e.Song.Id == selectedId);
            s_selected = Math.Max(0, index);
            EnsureVisible();
            SetStatus($"Spotify sync complete: {count} tracks • {Context.Songs.Count} songs in your library.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or System.Text.Json.JsonException)
        {
            SetStatus($"Spotify sync failed: {ex.Message}");
        }
        finally
        {
            _syncing = false;
        }
    }
}
