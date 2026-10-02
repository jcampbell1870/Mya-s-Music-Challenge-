using System.Drawing.Drawing2D;
using MyasMusicChallenge.Rendering;

namespace MyasMusicChallenge.Scenes;

public sealed class TitleScene(GameForm game) : Scene(game)
{
    public override void Draw(Graphics g)
    {
        Stage.Draw(g, Time, 0.5f);
        MyaSprite.Draw(g, 250, 610, 1.15f, MyaPose.Cheer, Time, 0f);

        var titleRect = new RectangleF(420, 110, 820, 110);
        using (var glow = new SolidBrush(Color.FromArgb(90, Ui.Pink)))
        {
            for (var i = 6; i > 0; i -= 2)
            {
                var r = titleRect;
                r.Inflate(i, i);
                g.DrawString("Mýa's Music Challenge", Ui.Huge, glow, r, Ui.Center);
            }
        }

        using (var brush = new LinearGradientBrush(titleRect, Color.White, Ui.Gold, LinearGradientMode.Vertical))
        {
            g.DrawString("Mýa's Music Challenge", Ui.Huge, brush, titleRect, Ui.Center);
        }

        Ui.Text(g, "Sing Mýa's hits on the big stage • Get a pro vocal assessment from Mýa herself",
            Ui.Body, Ui.Muted, new RectangleF(420, 225, 820, 30), Ui.Center);
        Ui.Text(g, $"Earn Arcade1870 ({Context.Settings.Rewards.TokenSymbol}) just for playing — same reward treasury as Crypto Hockey",
            Ui.Body, Ui.Gold, new RectangleF(420, 258, 820, 30), Ui.Center);

        var pulse = (int)(160 + 95 * Math.Sin(Time * 4));
        Ui.Text(g, "Press ENTER to start", Ui.Heading, Color.FromArgb(Math.Clamp(pulse, 0, 255), Color.White),
            new RectangleF(420, 360, 820, 50), Ui.Center);

        Ui.Text(g, $"Welcome back, {Context.Profile.PlayerName}!  •  {Context.Songs.Count} songs ready",
            Ui.Body, Color.White, new RectangleF(420, 420, 820, 30), Ui.Center);

        Ui.KeyHints(g, "ENTER start   •   F11 full screen   •   ESC quit   •   Use headphones so the mic only hears you");
    }

    public override void OnKey(Keys key)
    {
        switch (key)
        {
            case Keys.Enter or Keys.Space:
                Game.ChangeScene(new SongSelectScene(Game));
                break;
            case Keys.Escape:
                Game.Close();
                break;
        }
    }
}
