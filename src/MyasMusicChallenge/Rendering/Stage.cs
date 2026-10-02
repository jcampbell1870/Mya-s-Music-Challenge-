using System.Drawing.Drawing2D;

namespace MyasMusicChallenge.Rendering;

/// <summary>The concert stage backdrop: gradient sky, sweeping spotlights, equalizer lights and a cheering crowd.</summary>
public static class Stage
{
    private static readonly (float X, float Y, float Size, float Phase)[] Stars = CreateStars();
    private static readonly (float X, float Y, float Size, float Phase)[] Motes = CreateMotes();

    public static void Draw(Graphics g, double time, float energy = 0.3f, bool crowd = true)
    {
        using (var sky = new LinearGradientBrush(new Rectangle(0, 0, Ui.Width, Ui.Height), Color.FromArgb(40, 12, 80), Ui.Night, LinearGradientMode.Vertical))
        {
            g.FillRectangle(sky, 0, 0, Ui.Width, Ui.Height);
        }

        foreach (var (x, y, size, phase) in Stars)
        {
            var alpha = (int)(120 + 120 * Math.Sin(time * 2 + phase));
            using var star = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 0, 255), 255, 255, 255));
            g.FillEllipse(star, x, y, size, size);
        }

        DrawStageRig(g, time);
        var lightAlpha = Math.Clamp(45 + (int)(energy * 35), 45, 110);
        DrawSpotlight(g, 220, (float)(Math.Sin(time * 0.7) * 260), Color.FromArgb(lightAlpha, Ui.Pink));
        DrawSpotlight(g, Ui.Width - 220, (float)(Math.Sin(time * 0.6 + 2) * 260), Color.FromArgb(lightAlpha, Ui.Teal));
        DrawSpotlight(g, Ui.Width / 2f, (float)(Math.Sin(time * 0.5 + 4) * 200), Color.FromArgb(lightAlpha - 10, Ui.Gold));

        DrawEqualizer(g, time, energy);

        using (var floor = new LinearGradientBrush(new Rectangle(0, 560, Ui.Width, 160), Color.FromArgb(70, 30, 110), Color.FromArgb(20, 8, 40), LinearGradientMode.Vertical))
        {
            g.FillRectangle(floor, 0, 560, Ui.Width, 160);
        }

        using (var edge = new Pen(Color.FromArgb(200, Ui.Pink), 3))
        {
            g.DrawLine(edge, 0, 560, Ui.Width, 560);
        }

        if (crowd)
        {
            DrawCrowd(g, time, energy);
        }

        DrawMotes(g, time, energy);
    }

    private static void DrawStageRig(Graphics g, double time)
    {
        using var rail = new LinearGradientBrush(new Rectangle(0, 0, Ui.Width, 64), Color.FromArgb(245, 33, 23, 58), Color.FromArgb(0, 18, 10, 40), LinearGradientMode.Vertical);
        g.FillRectangle(rail, 0, 0, Ui.Width, 64);
        using var frame = new Pen(Color.FromArgb(120, 220, 185, 255), 2);
        g.DrawLine(frame, 48, 34, Ui.Width - 48, 34);
        g.DrawLine(frame, 96, 34, 96, 76);
        g.DrawLine(frame, Ui.Width - 96, 34, Ui.Width - 96, 76);

        var colors = new[] { Ui.Pink, Ui.Teal, Ui.Gold, Ui.Purple };
        for (var i = 0; i < 16; i++)
        {
            var pulse = (int)(125 + 100 * (0.5 + 0.5 * Math.Sin(time * 3 + i * 0.8)));
            using var lamp = new SolidBrush(Color.FromArgb(pulse, colors[i % colors.Length]));
            g.FillEllipse(lamp, 112 + i * 70, 29, 10, 10);
        }
    }

    private static void DrawMotes(Graphics g, double time, float energy)
    {
        using var glow = new SolidBrush(Color.FromArgb(Math.Clamp(35 + (int)(energy * 110), 35, 145), 255, 232, 255));
        foreach (var (x, y, size, phase) in Motes)
        {
            var driftY = (float)((y + time * (8 + energy * 16) + phase * 70) % 510);
            var shimmer = 0.35 + 0.65 * Math.Abs(Math.Sin(time * 2 + phase));
            var moteSize = size * (float)shimmer;
            g.FillEllipse(glow, x, 75 + driftY, moteSize, moteSize);
        }
    }

    private static void DrawSpotlight(Graphics g, float originX, float sweep, Color color)
    {
        var target = originX + sweep;
        PointF[] beam = [new(originX - 12, -10), new(originX + 12, -10), new(target + 140, 600), new(target - 140, 600)];
        using var path = new GraphicsPath();
        path.AddPolygon(beam);
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(originX, 0),
            CenterColor = color,
            SurroundColors = [Color.FromArgb(0, color)],
        };
        g.FillPath(brush, path);
    }

    private static void DrawEqualizer(Graphics g, double time, float energy)
    {
        const int bars = 32;
        var barWidth = Ui.Width / (float)bars;
        for (var i = 0; i < bars; i++)
        {
            var level = 0.25 + 0.75 * Math.Abs(Math.Sin(time * (2.2 + i % 5 * 0.4) + i * 0.7)) * (0.35 + energy);
            var h = (float)(level * 120);
            using var brush = new SolidBrush(Color.FromArgb(40, i % 2 == 0 ? Ui.Pink : Ui.Purple));
            g.FillRectangle(brush, i * barWidth + 4, 560 - h, barWidth - 8, h);
        }
    }

    private static void DrawCrowd(Graphics g, double time, float energy)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 10, 4, 22));
        for (var i = 0; i < 26; i++)
        {
            var x = i * 52f - 10;
            var bounce = (float)(Math.Abs(Math.Sin(time * (3 + energy * 3) + i * 1.3)) * (6 + energy * 14));
            var y = 650 - bounce + i % 3 * 8;
            g.FillEllipse(brush, x, y, 40, 44);
            g.FillEllipse(brush, x - 8, y + 34, 56, 70);
            if (i % 4 == 0)
            {
                // Raised hands and phone lights.
                g.FillRectangle(brush, x + 30, y - 30, 8, 40);
                using var light = new SolidBrush(Color.FromArgb(200, 255, 250, 200));
                g.FillEllipse(light, x + 28, y - 38, 12, 12);
            }
        }
    }

    private static (float, float, float, float)[] CreateStars()
    {
        var random = new Random(1870);
        return Enumerable.Range(0, 70)
            .Select(_ => ((float)random.NextDouble() * Ui.Width, (float)random.NextDouble() * 380, 1.5f + (float)random.NextDouble() * 2.5f, (float)(random.NextDouble() * Math.PI * 2)))
            .ToArray();
    }

    private static (float, float, float, float)[] CreateMotes()
    {
        var random = new Random(1871);
        return Enumerable.Range(0, 46)
            .Select(_ => ((float)random.NextDouble() * Ui.Width, (float)random.NextDouble() * 510, 1f + (float)random.NextDouble() * 2.4f, (float)random.NextDouble()))
            .ToArray();
    }
}
