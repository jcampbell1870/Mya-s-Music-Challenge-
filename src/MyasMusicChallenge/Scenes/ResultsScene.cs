using MyasMusicChallenge.Core.Assessment;
using MyasMusicChallenge.Core.Configuration;
using MyasMusicChallenge.Core.Rewards;
using MyasMusicChallenge.Core.Songs;
using MyasMusicChallenge.Rendering;

namespace MyasMusicChallenge.Scenes;

/// <summary>Mýa delivers her professional assessment and the player collects their Arcade1870 reward.</summary>
public sealed class ResultsScene : Scene
{
    private const int MaxHistory = 200;
    private const double CharactersPerSecond = 48;

    private readonly SongEntry _entry;
    private readonly VocalAssessment _assessment;
    private readonly double _playedSeconds;
    private readonly List<string> _speech;
    private readonly RewardGameProof _proof;
    private readonly PerformanceRecord _record;
    private int _lineIndex;
    private double _lineStart;
    private RewardState _rewardState;
    private string _rewardMessage = string.Empty;
    private RewardClaimTransaction? _claim;

    public ResultsScene(GameForm game, SongEntry entry, VocalAssessment assessment, double playedSeconds)
        : base(game)
    {
        _entry = entry;
        _assessment = assessment;
        _playedSeconds = playedSeconds;
        _speech = [.. assessment.SpeechLines()];
        _proof = PlayReward.CreateProof(entry.Song.Id, assessment.OverallScore, DateTime.UtcNow);
        _record = new PerformanceRecord
        {
            GameId = _proof.GameId,
            SongId = entry.Song.Id,
            SongTitle = entry.Song.DisplayTitle,
            Score = assessment.OverallScore,
            Grade = assessment.Grade,
            PlayedAt = _proof.CompletedAt,
        };
    }

    private enum RewardState
    {
        Disabled,
        TooShort,
        NeedsWallet,
        Requesting,
        Ready,
        Opened,
        Failed,
    }

    private RewardTreasuryOptions Rewards => Context.Settings.Rewards;

    private bool LineFullyShown => (Time - _lineStart) * CharactersPerSecond >= _speech[_lineIndex].Length;

    public override void Enter()
    {
        var history = Context.Profile.History;
        history.Add(_record);
        if (history.Count > MaxHistory)
        {
            history.RemoveRange(0, history.Count - MaxHistory);
        }

        Context.SaveProfile();

        if (!Rewards.Enabled)
        {
            _rewardState = RewardState.Disabled;
        }
        else if (!PlayReward.IsEligible(_playedSeconds, Rewards))
        {
            _rewardState = RewardState.TooShort;
        }
        else
        {
            RequestReward();
        }
    }

    private async void RequestReward()
    {
        if (!RewardClaimEncoder.IsValidAddress(Context.Profile.WalletAddress))
        {
            _rewardState = RewardState.NeedsWallet;
            return;
        }

        _rewardState = RewardState.Requesting;
        RewardClaimResult result;
        try
        {
            result = await Context.Rewards.RequestClaimAsync(Context.Profile.WalletAddress, _proof);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or InvalidOperationException)
        {
            result = RewardClaimResult.Failure(ex.Message);
        }

        if (result is { IsSuccessful: true, Transaction: { } transaction })
        {
            _claim = transaction;
            _rewardState = RewardState.Ready;
            _record.RewardIssued = true;
            Context.SaveProfile();
        }
        else
        {
            _rewardState = RewardState.Failed;
            _rewardMessage = result.ErrorMessage ?? "The reward treasury didn't respond.";
        }
    }

    public override void Draw(Graphics g)
    {
        var great = _assessment.OverallScore >= 80;
        Stage.Draw(g, Time, great ? 0.8f : 0.35f);

        var talking = !LineFullyShown;
        var pose = talking ? MyaPose.Talk : great ? MyaPose.Cheer : MyaPose.Idle;
        var mouth = talking ? (float)Math.Abs(Math.Sin(Time * 14)) * 0.6f : 0f;
        MyaSprite.Draw(g, 160, 640, 0.95f, pose, Time, mouth);

        var shown = (int)Math.Min(_speech[_lineIndex].Length, (Time - _lineStart) * CharactersPerSecond);
        Ui.SpeechBubble(g, new RectangleF(300, 30, 560, 250), new PointF(222, 240), _speech[_lineIndex][..shown], Ui.Body);
        Ui.Text(g, $"Mýa • {_lineIndex + 1}/{_speech.Count}  (SPACE ▸)", Ui.Small, Ui.Muted, new RectangleF(300, 282, 560, 22), Ui.Right, shadow: false);

        DrawScore(g);
        DrawCategories(g);
        DrawStatistics(g);
        DrawReward(g);

        Ui.KeyHints(g, "SPACE/→ next comment  •  ← previous  •  C claim A1870  •  W wallet  •  R retry reward  •  A sing again  •  ENTER songs");
    }

    private void DrawScore(Graphics g)
    {
        var panel = new RectangleF(880, 30, 360, 250);
        Ui.FillRounded(g, Ui.Panel, panel, 18);
        Ui.Text(g, _assessment.Grade, Ui.Huge, Ui.Gold, new RectangleF(panel.X, panel.Y + 10, 150, 110), Ui.Center);
        Ui.Text(g, $"{_assessment.OverallScore}", Ui.Huge, Color.White, new RectangleF(panel.X + 140, panel.Y + 10, 200, 110), Ui.Center);
        Ui.Text(g, "/100", Ui.Body, Ui.Muted, new RectangleF(panel.X + 270, panel.Y + 90, 80, 30), Ui.Left, shadow: false);
        Ui.Text(g, _assessment.Title, Ui.Heading, Ui.Pink, new RectangleF(panel.X, panel.Y + 130, panel.Width, 40), Ui.Center);
        g.DrawString(_entry.Song.DisplayTitle, Ui.Body, Brushes.White, new RectangleF(panel.X + 16, panel.Y + 178, panel.Width - 32, 60), Ui.Center);
    }

    private void DrawCategories(Graphics g)
    {
        var panel = new RectangleF(300, 312, 560, 238);
        Ui.FillRounded(g, Ui.Panel, panel, 18);
        Ui.Text(g, "Mýa's scorecard", Ui.BodyBold, Ui.Gold, panel.X + 18, panel.Y + 8);
        for (var i = 0; i < _assessment.Categories.Count; i++)
        {
            var category = _assessment.Categories[i];
            var y = panel.Y + 42 + i * 32;
            Ui.Text(g, category.Name, Ui.Small, Color.White, new RectangleF(panel.X + 18, y, 180, 24), Ui.Left, shadow: false);
            Ui.Bar(g, new RectangleF(panel.X + 200, y + 6, 290, 12), category.Score / 100.0, Ui.Teal);
            Ui.Text(g, category.Score.ToString(), Ui.BodyBold, Color.White, new RectangleF(panel.X + 496, y, 50, 24), Ui.Right, shadow: false);
        }
    }

    private void DrawStatistics(Graphics g)
    {
        var panel = new RectangleF(880, 312, 360, 238);
        Ui.FillRounded(g, Ui.Panel, panel, 18);
        Ui.Text(g, "Vocal stats", Ui.BodyBold, Ui.Gold, panel.X + 18, panel.Y + 8);
        var s = _assessment.Statistics;
        var rows = new (string, string)[]
        {
            ("Range", s.LowestNote is null ? "—" : $"{s.LowestNote} – {s.HighestNote} ({s.RangeSemitones:0} st)"),
            ("Key", s.DetectedKey ?? "—"),
            ("Avg. pitch offset", $"{s.AverageCentsOff:0} cents"),
            ("Longest phrase", $"{s.LongestPhraseSeconds:0.0} s"),
            ("Vibrato", s.VibratoDetected ? "Yes ✨" : "Not yet"),
            ("Singing time", $"{Ui.Clock(s.VoicedSeconds)} of {Ui.Clock(s.DurationSeconds)}"),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var y = panel.Y + 42 + i * 32;
            Ui.Text(g, rows[i].Item1, Ui.Small, Ui.Muted, new RectangleF(panel.X + 18, y, 150, 24), Ui.Left, shadow: false);
            Ui.Text(g, rows[i].Item2, Ui.Small, Color.White, new RectangleF(panel.X + 160, y, 186, 24), Ui.Right, shadow: false);
        }
    }

    private void DrawReward(Graphics g)
    {
        var panel = new RectangleF(300, 566, 940, 104);
        Ui.FillRounded(g, Color.FromArgb(220, 40, 24, 8), panel, 18);
        Ui.OutlineRounded(g, Ui.Gold, 2, panel, 18);

        var coin = new RectangleF(panel.X + 18, panel.Y + 20, 64, 64);
        using (var gold = new SolidBrush(Ui.Gold))
        {
            g.FillEllipse(gold, coin);
        }

        Ui.Text(g, "A", Ui.Heading, Color.FromArgb(120, 70, 0), coin, Ui.Center, shadow: false);

        var symbol = Rewards.TokenSymbol;
        var (headline, detail) = _rewardState switch
        {
            RewardState.Disabled => ("Arcade1870 rewards are turned off", "Enable Rewards in appsettings.json to earn tokens."),
            RewardState.TooShort => ($"Sing for at least {Rewards.MinimumPlaySeconds} seconds to earn {symbol}", "Every full performance earns tokens — win or lose!"),
            RewardState.NeedsWallet => ($"You earned {Rewards.RewardAmount} {symbol}!", "Press W to add your wallet address and collect it from the Arcade1870 treasury."),
            RewardState.Requesting => ($"Requesting {Rewards.RewardAmount} {symbol} from the Arcade1870 treasury…", "Shared reward vault with Crypto Hockey."),
            RewardState.Ready => ($"{_claim!.DisplayAmount} {symbol} ready for {Ui.ShortAddress(_claim.Recipient)}!", "Press C to claim with MetaMask in your browser (don't leave before claiming)."),
            RewardState.Opened => ($"Claim page opened for {_claim!.DisplayAmount} {symbol}", "Approve the transaction in MetaMask. Press C to reopen the page."),
            _ => ("Couldn't get your reward yet", $"{_rewardMessage}  Press R to retry."),
        };
        Ui.Text(g, headline, Ui.Heading, Ui.Gold, new RectangleF(panel.X + 100, panel.Y + 12, panel.Width - 120, 40), Ui.Left);
        Ui.Text(g, detail, Ui.Body, Color.White, new RectangleF(panel.X + 100, panel.Y + 54, panel.Width - 120, 36), Ui.Left, shadow: false);
    }

    public override void OnKey(Keys key)
    {
        switch (key)
        {
            case Keys.Space or Keys.Right:
                if (!LineFullyShown)
                {
                    _lineStart = double.MinValue / 4;
                }
                else if (_lineIndex < _speech.Count - 1)
                {
                    _lineIndex++;
                    _lineStart = Time;
                }

                break;
            case Keys.Left when _lineIndex > 0:
                _lineIndex--;
                _lineStart = double.MinValue / 4;
                break;
            case Keys.C when _claim is not null:
                Context.PublishClaimPage(_claim, _entry.Song.DisplayTitle);
                _rewardState = RewardState.Opened;
                break;
            case Keys.W:
                EditWallet();
                break;
            case Keys.R when _rewardState is RewardState.Failed:
                RequestReward();
                break;
            case Keys.A:
                Game.ChangeScene(new SingScene(Game, _entry));
                break;
            case Keys.Enter or Keys.Escape:
                Game.ChangeScene(new SongSelectScene(Game));
                break;
        }
    }

    private void EditWallet()
    {
        if (_rewardState is RewardState.Requesting or RewardState.Ready or RewardState.Opened)
        {
            return;
        }

        var value = Game.Prompt(
            "Arcade1870 wallet",
            $"Your Ethereum wallet address (0x…) to receive {Rewards.TokenSymbol}:",
            Context.Profile.WalletAddress,
            42);
        if (value is null || !RewardClaimEncoder.IsValidAddress(value))
        {
            return;
        }

        Context.Profile.WalletAddress = value;
        Context.SaveProfile();
        if (_rewardState is RewardState.NeedsWallet or RewardState.Failed)
        {
            RequestReward();
        }
    }
}
