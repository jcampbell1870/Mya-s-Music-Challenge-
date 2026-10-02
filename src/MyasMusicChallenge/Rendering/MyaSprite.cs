using System.Drawing.Drawing2D;

namespace MyasMusicChallenge.Rendering;

public enum MyaPose
{
    Idle,
    Sing,
    Talk,
    Cheer,
}

/// <summary>
/// Procedurally drawn 2D cartoon of Mýa: long dark wavy hair with caramel highlights, gold hoops,
/// a sparkly crop top, high-waisted silver pants and a microphone. Origin is between her feet.
/// </summary>
public static class MyaSprite
{
    private static readonly Color Skin = Color.FromArgb(198, 134, 92);
    private static readonly Color SkinShade = Color.FromArgb(168, 108, 72);
    private static readonly Color Hair = Color.FromArgb(46, 28, 20);
    private static readonly Color HairHighlight = Color.FromArgb(150, 96, 52);
    private static readonly Color Lips = Color.FromArgb(176, 64, 96);
    private static readonly Color Pants = Color.FromArgb(214, 214, 232);
    private static readonly Color Boots = Color.FromArgb(70, 30, 90);

    public static void Draw(Graphics g, float x, float y, float scale, MyaPose pose, double time, float mouthOpen)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(x, y);
        g.ScaleTransform(scale, scale);

        using (var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
        {
            g.FillEllipse(shadow, -70, -12, 140, 24);
        }

        var bounce = pose == MyaPose.Cheer ? (float)-Math.Abs(Math.Sin(time * 6)) * 18 : 0f;
        var sway = pose switch
        {
            MyaPose.Sing => (float)Math.Sin(time * 3.2) * 3.5f,
            MyaPose.Cheer => (float)Math.Sin(time * 6) * 4f,
            _ => (float)Math.Sin(time * 1.8) * 2f,
        };
        g.TranslateTransform(0, bounce);
        g.RotateTransform(sway);

        DrawBackHair(g, time);
        DrawLegs(g, pose, time);
        DrawTorso(g, time);
        DrawFreeArm(g, pose, time);
        DrawHead(g, pose, time, mouthOpen);
        DrawMicArm(g, pose, time);

        g.Restore(state);
    }

    private static void DrawBackHair(Graphics g, double time)
    {
        var flow = (float)Math.Sin(time * 2.1) * 4;
        PointF[] hair =
        [
            new(-58, -440), new(0, -462), new(58, -440), new(74, -380), new(80 + flow, -320),
            new(70 + flow, -262), new(40, -282), new(30, -340), new(-30, -340), new(-40, -282),
            new(-70 - flow, -262), new(-80 - flow, -320), new(-74, -380),
        ];
        using var brush = new SolidBrush(Hair);
        g.FillClosedCurve(brush, hair, FillMode.Winding, 0.5f);
        using var highlight = new Pen(HairHighlight, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawCurve(highlight, new PointF[] { new(-62, -400), new(-70 - flow, -330), new(-62 - flow, -280) }, 0.6f);
        g.DrawCurve(highlight, new PointF[] { new(62, -400), new(72 + flow, -330), new(62 + flow, -280) }, 0.6f);
    }

    private static void DrawLegs(Graphics g, MyaPose pose, double time)
    {
        var step = pose == MyaPose.Sing ? (float)Math.Sin(time * 3.2) * 6 : 0f;
        using var pants = new Pen(Pants, 34) { StartCap = LineCap.Round, EndCap = LineCap.Flat };
        g.DrawLine(pants, -20, -205, -26 - step, -58);
        g.DrawLine(pants, 20, -205, 26 + step, -58);

        using var hips = new SolidBrush(Pants);
        g.FillPie(hips, -42, -240, 84, 80, 0, 180);
        using var shine = new Pen(Color.FromArgb(150, 255, 255, 255), 4);
        g.DrawLine(shine, -28, -190, -32 - step, -70);
        g.DrawLine(shine, 12, -190, 16 + step, -70);

        using var boots = new SolidBrush(Boots);
        g.FillRectangle(boots, -44 - step, -64, 34, 54);
        g.FillRectangle(boots, 10 + step, -64, 34, 54);
        g.FillEllipse(boots, -54 - step, -20, 46, 20);
        g.FillEllipse(boots, 8 + step, -20, 46, 20);
    }

    private static void DrawTorso(Graphics g, double time)
    {
        using var skin = new SolidBrush(Skin);
        g.FillRectangle(skin, -11, -350, 22, 32);
        g.FillRectangle(skin, -32, -262, 64, 46);
        using (var navel = new SolidBrush(SkinShade))
        {
            g.FillEllipse(navel, -2, -236, 4, 6);
        }

        PointF[] top = [new(-48, -322), new(48, -322), new(38, -258), new(-38, -258)];
        using var topBrush = new LinearGradientBrush(new RectangleF(-48, -322, 96, 64), Ui.Pink, Ui.Purple, LinearGradientMode.ForwardDiagonal);
        g.FillPolygon(topBrush, top);

        using var belt = new SolidBrush(Ui.Gold);
        g.FillRectangle(belt, -36, -222, 72, 8);

        for (var i = 0; i < 9; i++)
        {
            var sx = -34 + i * 37 % 68;
            var sy = -312 + i * 23 % 48;
            var twinkle = (float)(0.5 + 0.5 * Math.Sin(time * 5 + i * 1.7));
            DrawSparkle(g, sx, sy, 3 + twinkle * 4, Color.FromArgb((int)(120 + 135 * twinkle), 255, 255, 255));
        }
    }

    private static void DrawFreeArm(Graphics g, MyaPose pose, double time)
    {
        var wave = (float)Math.Sin(time * 3.2);
        (PointF elbow, PointF hand) = pose switch
        {
            MyaPose.Sing => (new PointF(-88, -296), new PointF(-132, -322 + wave * 18)),
            MyaPose.Talk => (new PointF(-70, -268), new PointF(-96 + wave * 6, -306)),
            MyaPose.Cheer => (new PointF(-78, -372), new PointF(-92 + wave * 8, -446)),
            _ => (new PointF(-60, -258), new PointF(-60 + wave * 4, -200)),
        };
        DrawArm(g, new PointF(-42, -314), elbow, hand);
    }

    private static void DrawMicArm(Graphics g, MyaPose pose, double time)
    {
        (PointF elbow, PointF hand, float micAngle) = pose switch
        {
            MyaPose.Sing => (new PointF(58, -276), new PointF(22, -346), -150f),
            MyaPose.Cheer => (new PointF(78, -372), new PointF(92 + (float)Math.Sin(time * 6) * 8, -446), -95f),
            _ => (new PointF(58, -256), new PointF(54, -206), -100f),
        };
        DrawArm(g, new PointF(42, -314), elbow, hand);

        var state = g.Save();
        g.TranslateTransform(hand.X, hand.Y);
        g.RotateTransform(micAngle);
        using var handle = new SolidBrush(Color.FromArgb(30, 30, 36));
        g.FillRectangle(handle, -2, -5, 36, 10);
        using var head = new LinearGradientBrush(new RectangleF(30, -11, 22, 22), Color.Silver, Color.FromArgb(90, 90, 100), LinearGradientMode.Vertical);
        g.FillEllipse(head, 30, -11, 22, 22);
        g.Restore(state);

        using var fingers = new SolidBrush(Skin);
        g.FillEllipse(fingers, hand.X - 9, hand.Y - 9, 18, 18);
    }

    private static void DrawArm(Graphics g, PointF shoulder, PointF elbow, PointF hand)
    {
        using var arm = new Pen(Skin, 15) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(arm, [shoulder, elbow, hand]);
        using var bangle = new Pen(Ui.Gold, 4);
        var bx = elbow.X + (hand.X - elbow.X) * 0.8f;
        var by = elbow.Y + (hand.Y - elbow.Y) * 0.8f;
        g.DrawEllipse(bangle, bx - 8, by - 8, 16, 16);
    }

    private static void DrawHead(Graphics g, MyaPose pose, double time, float mouthOpen)
    {
        using var skin = new SolidBrush(Skin);
        g.FillEllipse(skin, -40, -438, 80, 98);

        using (var hoop = new Pen(Ui.Gold, 3))
        {
            g.DrawEllipse(hoop, -50, -382, 16, 22);
            g.DrawEllipse(hoop, 34, -382, 16, 22);
        }

        var blinking = time % 4.2 < 0.12;
        var eyesClosed = blinking || (pose == MyaPose.Sing && mouthOpen > 0.75f);
        foreach (var ex in new[] { -16f, 16f })
        {
            if (eyesClosed)
            {
                using var lid = new Pen(Hair, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(lid, ex - 8, -400, 16, 8, 0, 180);
                continue;
            }

            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, ex - 9, -402, 18, 11);
            using var iris = new SolidBrush(Color.FromArgb(60, 32, 20));
            g.FillEllipse(iris, ex - 4.5f, -401, 9, 9);
            using var glint = new SolidBrush(Color.White);
            g.FillEllipse(glint, ex - 1, -399, 3, 3);
            using var lash = new Pen(Color.Black, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(lash, ex - 10, -404, 20, 12, 190, 160);
        }

        using (var brow = new Pen(Hair, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            var lift = pose is MyaPose.Cheer or MyaPose.Talk ? -3 : 0;
            g.DrawArc(brow, -27, -418 + lift, 22, 10, 200, 130);
            g.DrawArc(brow, 5, -418 + lift, 22, 10, 210, 130);
        }

        using (var blush = new SolidBrush(Color.FromArgb(70, 255, 90, 130)))
        {
            g.FillEllipse(blush, -32, -384, 16, 9);
            g.FillEllipse(blush, 16, -384, 16, 9);
        }

        using (var nose = new Pen(SkinShade, 2.5f))
        {
            g.DrawArc(nose, -5, -388, 10, 8, 20, 140);
        }

        var open = Math.Clamp(mouthOpen, 0f, 1f);
        using var lips = new SolidBrush(Lips);
        if (open < 0.08f)
        {
            if (pose is MyaPose.Cheer or MyaPose.Talk or MyaPose.Idle)
            {
                using var smile = new Pen(Lips, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(smile, -13, -378, 26, 14, 15, 150);
            }
            else
            {
                g.FillEllipse(lips, -11, -368, 22, 7);
            }
        }
        else
        {
            var h = 5 + open * 16;
            g.FillEllipse(lips, -13, -370, 26, h + 5);
            using var mouth = new SolidBrush(Color.FromArgb(80, 20, 30));
            g.FillEllipse(mouth, -9, -367, 18, h);
            using var teeth = new SolidBrush(Color.White);
            g.FillRectangle(teeth, -6, -367, 12, Math.Min(3, h / 3));
        }

        PointF[] bangs =
        [
            new(-44, -404), new(-38, -440), new(-6, -452), new(30, -446), new(46, -416),
            new(42, -380), new(34, -414), new(10, -428), new(-20, -424),
        ];
        using var hair = new SolidBrush(Hair);
        g.FillClosedCurve(hair, bangs, FillMode.Winding, 0.45f);
        using var shine = new Pen(HairHighlight, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawCurve(shine, [new(-30, -432), new(0, -444), new(28, -436)], 0.5f);
    }

    public static void DrawSparkle(Graphics g, float x, float y, float size, Color color)
    {
        using var brush = new SolidBrush(color);
        PointF[] star =
        [
            new(x, y - size), new(x + size * 0.25f, y - size * 0.25f), new(x + size, y),
            new(x + size * 0.25f, y + size * 0.25f), new(x, y + size), new(x - size * 0.25f, y + size * 0.25f),
            new(x - size, y), new(x - size * 0.25f, y - size * 0.25f),
        ];
        g.FillPolygon(brush, star);
    }
}
