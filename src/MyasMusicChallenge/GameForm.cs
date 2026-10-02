using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using MyasMusicChallenge.Rendering;
using MyasMusicChallenge.Scenes;

namespace MyasMusicChallenge;

/// <summary>The game window: a 60 fps loop that letterboxes a 1280×720 canvas and routes keys to the active scene.</summary>
public sealed class GameForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTick;
    private Scene _scene;
    private FormWindowState _restoreState = FormWindowState.Normal;

    public GameForm(GameContext context)
    {
        Context = context;
        Text = "Mýa's Music Challenge";
        ClientSize = new Size(Ui.Width, Ui.Height);
        MinimumSize = new Size(660, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.Black;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;

        _scene = new TitleScene(this);
        _scene.Enter();
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public GameContext Context { get; }

    public void ChangeScene(Scene next)
    {
        _scene.Leave();
        _scene = next;
        _scene.Enter();
        Invalidate();
    }

    /// <summary>Shows a modal text prompt; the game loop pauses while it is open.</summary>
    public string? Prompt(string title, string message, string initialValue, int maxLength) =>
        TextPromptDialog.Show(this, title, message, initialValue, maxLength);

    private void Tick()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var delta = Math.Min(0.1, now - _lastTick);
        _lastTick = now;
        _scene.Update(delta);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);
        var scale = Math.Min(ClientSize.Width / (float)Ui.Width, ClientSize.Height / (float)Ui.Height);
        if (scale <= 0)
        {
            return;
        }

        g.TranslateTransform((ClientSize.Width - Ui.Width * scale) / 2, (ClientSize.Height - Ui.Height * scale) / 2);
        g.ScaleTransform(scale, scale);
        g.SetClip(new Rectangle(0, 0, Ui.Width, Ui.Height));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        _scene.Draw(g);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Arrow keys, Enter, Tab and Escape are dialog keys in WinForms; route them to the scene instead.
        var key = keyData & Keys.KeyCode;
        if ((keyData & (Keys.Control | Keys.Alt)) == 0 &&
            key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Enter or Keys.Escape or Keys.Tab)
        {
            _scene.OnKey(key);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F11 || (e.Alt && e.KeyCode == Keys.Enter))
        {
            ToggleFullScreen();
        }
        else
        {
            _scene.OnKey(e.KeyCode);
        }

        e.Handled = true;
    }

    private void ToggleFullScreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = _restoreState;
        }
        else
        {
            _restoreState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _scene.Leave();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
