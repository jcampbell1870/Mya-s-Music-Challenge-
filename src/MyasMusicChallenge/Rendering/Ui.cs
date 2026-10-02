using System.Drawing.Drawing2D;

namespace MyasMusicChallenge.Rendering;

/// <summary>Shared fonts, colours and drawing helpers. All coordinates are in the 1280×720 virtual canvas.</summary>
public static class Ui
{
    public const int Width = 1280;
    public const int Height = 720;

    public static readonly Color Pink = Color.FromArgb(255, 79, 163);
    public static readonly Color Purple = Color.FromArgb(124, 58, 237);
    public static readonly Color Gold = Color.FromArgb(255, 200, 61);
    public static readonly Color Teal = Color.FromArgb(45, 212, 191);
    public static readonly Color Night = Color.FromArgb(18, 10, 40);
    public static readonly Color Panel = Color.FromArgb(200, 22, 14, 48);
    public static readonly Color Muted = Color.FromArgb(190, 180, 220);

    public static readonly Font Huge = new("Segoe UI", 72, FontStyle.Bold, GraphicsUnit.Pixel);
    public static readonly Font Title = new("Segoe UI", 44, FontStyle.Bold, GraphicsUnit.Pixel);
    public static readonly Font Heading = new("Segoe UI", 28, FontStyle.Bold, GraphicsUnit.Pixel);
    public static readonly Font Body = new("Segoe UI", 20, FontStyle.Regular, GraphicsUnit.Pixel);
    public static readonly Font BodyBold = new("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
    public static readonly Font Small = new("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel);
    public static readonly Font Lyric = new("Segoe UI", 40, FontStyle.Bold, GraphicsUnit.Pixel);
    public static readonly Font LyricNext = new("Segoe UI", 26, FontStyle.Regular, GraphicsUnit.Pixel);

    public static readonly StringFormat Center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    public static readonly StringFormat Left = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    public static readonly StringFormat Right = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
    public static readonly StringFormat LeftEllipsis = new(StringFormatFlags.NoWrap)
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
    };

    public static readonly StringFormat CenterNoWrap = new(StringFormatFlags.NoWrap)
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
    };

    public static readonly StringFormat Wrap = new() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.Word };

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }

        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void OutlineRounded(Graphics g, Color color, float width, RectangleF r, float radius)
    {
        using var path = RoundedRect(r, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void Text(Graphics g, string text, Font font, Color color, RectangleF r, StringFormat format, bool shadow = true)
    {
        if (shadow)
        {
            using var shadowBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            var s = r;
            s.Offset(2, 2);
            g.DrawString(text, font, shadowBrush, s, format);
        }

        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, r, format);
    }

    public static void Text(Graphics g, string text, Font font, Color color, float x, float y, bool shadow = true)
    {
        if (shadow)
        {
            using var shadowBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            g.DrawString(text, font, shadowBrush, x + 2, y + 2);
        }

        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y);
    }

    public static void Bar(Graphics g, RectangleF r, double fraction, Color color)
    {
        FillRounded(g, Color.FromArgb(70, 255, 255, 255), r, r.Height / 2);
        var filled = new RectangleF(r.X, r.Y, (float)(r.Width * Math.Clamp(fraction, 0, 1)), r.Height);
        if (filled.Width >= 2)
        {
            using var path = RoundedRect(filled, r.Height / 2);
            using var brush = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width + 1, r.Height), color, Pink, LinearGradientMode.Horizontal);
            g.FillPath(brush, path);
        }
    }

    public static void SpeechBubble(Graphics g, RectangleF r, PointF tail, string text, Font font)
    {
        using var path = RoundedRect(r, 24);
        using var fill = new SolidBrush(Color.FromArgb(245, 255, 255, 255));
        using var border = new Pen(Pink, 4);
        var tailBaseY = Math.Clamp(tail.Y, r.Top + 30, r.Bottom - 50);
        PointF[] tailPoints = [new(r.Left + 2, tailBaseY), tail, new(r.Left + 2, tailBaseY + 34)];
        g.FillPolygon(fill, tailPoints);
        g.DrawLines(border, tailPoints);
        g.FillPath(fill, path);
        g.DrawPath(border, path);
        using var cover = new SolidBrush(Color.FromArgb(245, 255, 255, 255));
        g.FillRectangle(cover, r.Left - 1, tailBaseY + 3, 6, 28);
        using var textBrush = new SolidBrush(Color.FromArgb(40, 20, 60));
        g.DrawString(text, font, textBrush, RectangleF.Inflate(r, -22, -18), Wrap);
    }

    public static void KeyHints(Graphics g, string hints)
    {
        var r = new RectangleF(0, Height - 40, Width, 40);
        using var brush = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(brush, r);
        Text(g, hints, Small, Muted, r, CenterNoWrap, shadow: false);
    }

    public static string ShortAddress(string? address) =>
        string.IsNullOrWhiteSpace(address) || address.Length < 12
            ? "not set"
            : $"{address[..6]}…{address[^4..]}";

    public static string Clock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}
