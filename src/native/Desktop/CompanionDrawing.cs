using System.Windows;
using System.Windows.Media;

namespace LectureLens.Desktop;

// Original vector artwork: independent body, ears, paws and gaze stay crisp at any DPI.
internal sealed class CompanionDrawing : FrameworkElement
{
    internal CompanionPose Pose { get; } = new();
    static readonly Dictionary<string, Geometry> shapes = new();
    static readonly Dictionary<string, SolidColorBrush> colors = new();
    static readonly Brush fur = Gradient("#FFF8E7", "#EAC9A5");
    static readonly Brush face = Gradient("#FFFCF0", "#F6DEC0");
    static readonly Brush ear = Gradient("#E8CCA6", "#CBA57C");
    static readonly Brush scarf = Gradient("#7FA9A6", "#365F69");
    static readonly Brush eye = Gradient("#543E38", "#261F26");

    static Brush Gradient(string from, string to)
    {
        var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to), 90);
        brush.Freeze();
        return brush;
    }
    static SolidColorBrush Ink(string color)
    {
        if (!colors.TryGetValue(color, out var brush)) { brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); colors.Add(color, brush); }
        return brush;
    }
    static Geometry Shape(string path)
    {
        if (!shapes.TryGetValue(path, out var geometry)) { geometry = Geometry.Parse(path); geometry.Freeze(); shapes.Add(path, geometry); }
        return geometry;
    }
    static void Path(DrawingContext d, string path, Brush fill) => d.DrawGeometry(fill, null, Shape(path));
    static void Line(DrawingContext d, string path, string color, double width = 1.5)
        => d.DrawGeometry(null, new Pen(Ink(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Shape(path));
    static void Oval(DrawingContext d, double x, double y, double rx, double ry, Brush fill) => d.DrawEllipse(fill, null, new Point(x, y), rx, ry);

    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        var scale = Math.Min(ActualWidth / 240, ActualHeight / 176);
        if (scale <= 0) return;
        d.PushTransform(new TranslateTransform((ActualWidth - 240 * scale) / 2, (ActualHeight - 176 * scale) / 2));
        d.PushTransform(new ScaleTransform(scale, scale));
        // Leave headroom for hops and leaning ears without entering the caption or card edge.
        d.PushTransform(new ScaleTransform(.88, .88, 120, 190));
        var t = Pose.Time;
        var gesture = Pose.Gesture;
        var joy = gesture.Smile;
        var breathe = Pose.Reduced ? 0 : Math.Sin(t * 2.2);
        var bob = breathe * 1.1 + gesture.Y;

        // The halo is ambient decoration, never a progress or recording indicator.
        Oval(d, 120, 91, 83, 73, Ink("#0FF0BD73"));
        Oval(d, 120, 99, 65, 59, Ink("#0CF0BD73"));
        Line(d, "M44,103 C25,61 57,26 94,24 M151,22 C188,26 209,50 204,75", "#26D9AF78", 1);
        Star(d, 47, 52, 5, "#D9B375", .8);
        Star(d, 192, 102, 4, "#BEA078", .6);
        Oval(d, 182, 40, 2, 2, Ink("#99C0A17A"));
        Oval(d, 57, 122, 1.6, 1.6, Ink("#99C0A17A"));
        Oval(d, 119, 161, 48 + Math.Min(0, gesture.Y) * .5, 6, Ink("#200F1118"));
        Oval(d, 119, 160, 32 + Math.Min(0, gesture.Y) * .3, 3, Ink("#150F1118"));

        d.PushTransform(new TranslateTransform(Pose.BodyX * 6 + gesture.X, bob + Pose.BodyY * 2));
        d.PushTransform(new RotateTransform(Pose.BodyX * 6 + gesture.Roll, 120, 145));
        d.PushTransform(new ScaleTransform(1 - gesture.Stretch * .6, 1 + gesture.Stretch, 120, 154));
        d.PushTransform(new RotateTransform(breathe * 4 + Pose.BodyX * -9 + gesture.Roll * -1.2, 149, 134));
        Path(d, "M142,150 C188,154 194,119 179,105 C184,129 160,115 150,123 C142,129 139,140 142,150Z", fur);
        Path(d, "M179,105 C184,127 163,117 158,121 C172,132 190,126 179,105Z", face);
        d.Pop();
        Oval(d, 101, 153, 13, 7, fur);
        Oval(d, 139, 153, 13, 7, fur);
        Line(d, "M96,154 L96,156 M101,154 L101,156 M139,154 L139,156 M144,154 L144,156", "#C5A58C", 1);
        Path(d, "M90,113 C88,130 83,146 99,151 C109,155 133,155 144,149 C155,141 150,125 146,112Z", fur);
        Oval(d, 120, 136, 19, 15, face);

        Path(d, "M95,109 C105,118 139,118 147,109 L146,119 C134,129 106,128 94,118Z", scarf);
        d.PushTransform(new RotateTransform(-Pose.BodyX * 7 + gesture.Roll * -.7 + breathe * 2, 136, 122));
        Path(d, "M131,121 Q141,120 142,125 L139,143 Q133,145 128,139Z", scarf);
        Line(d, "M133,138 L138,140", "#A3CDC1", 1.5);
        d.Pop();
        Star(d, 117, 123, 4, "#F4D593", 1);

        d.PushTransform(new TranslateTransform(Pose.HeadX * 5, Pose.HeadY * 4 + gesture.HeadY));
        d.PushTransform(new RotateTransform(Pose.HeadX * 7 + gesture.HeadRoll, 120, 103));
        DrawEar(d, false, -4 - Pose.HeadX * 8 + gesture.EarLeft + breathe * 2);
        DrawEar(d, true, 7 - Pose.HeadX * 8 + gesture.EarRight - breathe * 2);
        Path(d, "M75,79 C72,58 92,49 111,52 Q120,46 129,52 C150,48 166,61 165,79 L175,91 L167,94 L171,101 C159,122 84,122 71,101 L75,95 L66,92Z", fur);
        Path(d, "M81,83 C84,70 102,72 120,83 C138,72 156,70 160,83 C170,113 143,115 120,114 C96,115 72,110 81,83Z", face);
        // Tiny forehead tuft and a warm star are Lumi's identifying marks.
        Line(d, "M113,58 Q116,61 120,58 Q122,62 126,58", "#D8B897", 1.2);
        Star(d, 120, 68, 5.5, "#DFA653", 1);
        Oval(d, 88, 99, 7, 3, Ink("#38D9AE7E"));
        Oval(d, 152, 99, 7, 3, Ink("#38D9AE7E"));
        var blinkPhase = t % 4.7;
        var blink = !Pose.Reduced && blinkPhase > 4.48 ? Math.Sin((blinkPhase - 4.48) / .22 * Math.PI) : 0;
        DrawEye(d, 101, 89, Pose.Variation < 0 ? Math.Max(blink, gesture.Wink) : blink, joy);
        DrawEye(d, 139, 89, Pose.Variation > 0 ? Math.Max(blink, gesture.Wink) : blink, joy);
        Path(d, "M116,98 Q120,95 124,98 Q123,102 120,102 Q117,102 116,98Z", Ink("#855A50"));
        if (joy > .35)
        {
            Path(d, "M115,104 Q120,107 125,104 Q125,113 120,113 Q115,113 115,104Z", Ink("#69473F"));
            Oval(d, 120, 110.5, 3.2, 1.8, Ink("#DB9E97"));
        }
        else Line(d, "M113,104 Q116,108 120,104 Q124,108 127,104", "#855A50", 1.6);
        d.Pop(); d.Pop();
        DrawPaw(d, false, gesture.PawLeft);
        DrawPaw(d, true, gesture.PawRight);
        d.Pop(); d.Pop(); d.Pop();

        if (gesture.Spark > .01 && !Pose.Reduced)
        {
            var rise = Pose.Progress * 18;
            d.PushOpacity(gesture.Spark);
            Star(d, 54, 68 - rise, 6, "#E9BD78", 1);
            Star(d, 192, 84 - rise, 7, "#8CB7BA", 1);
            Star(d, 171, 42 - rise * .5, 4, "#E9BD78", 1);
            if (Pose.Personal && Pose.Move == CompanionMove.Spark) Heart(d, 66, 39 - rise * .3, .5);
            d.Pop();
        }
        d.Pop(); d.Pop(); d.Pop();
    }

    void DrawEye(DrawingContext d, double x, double y, double blink, double joy)
    {
        if (Pose.Rest > .7 || joy > .55)
        {
            d.PushTransform(new TranslateTransform(x, y));
            Line(d, joy > .55 ? "M-7,1 Q0,-7 7,1" : "M-7,1 Q0,6 7,1", "#543E38", 2.5);
            d.Pop();
            return;
        }
        d.PushTransform(new TranslateTransform(x + Pose.LookX * 5, y + Pose.LookY * 3.5));
        d.PushTransform(new ScaleTransform(1, Math.Max(.07, 1 - blink)));
        Oval(d, 0, 0, 7.5, 10, eye);
        Oval(d, 1, 5, 4.5, 3, Ink("#956D50"));
        Oval(d, -2.5, -4, 2.6, 3, Ink("#FFFDF4"));
        Oval(d, 3, .5, 1.2, 1.4, Ink("#FFFDF4"));
        d.Pop(); d.Pop();
    }

    static void DrawPaw(DrawingContext d, bool right, double lift)
    {
        var side = right ? 1 : -1;
        var amount = lift / 155;
        d.PushTransform(new TranslateTransform(side * amount * 11, -amount * 4));
        d.PushTransform(new RotateTransform(side * (-12 - lift), right ? 149 : 91, 119));
        Oval(d, right ? 152 : 88, 131, 8, 15, fur);
        Oval(d, right ? 153 : 87, 136, 3.8, 4.5, Ink("#CDA67E"));
        d.Pop(); d.Pop();
    }

    static void DrawEar(DrawingContext d, bool right, double angle)
    {
        d.PushTransform(new TranslateTransform(right ? 148 : 92, 64));
        if (right) d.PushTransform(new ScaleTransform(-1, 1));
        d.PushTransform(new RotateTransform(angle));
        Path(d, "M-13,7 C-26,-7 -39,-45 -26,-49 C-12,-52 4,-19 10,3Z", fur);
        Path(d, "M-12,-1 C-22,-15 -31,-38 -26,-40 C-20,-42 -6,-15 -4,-3Z", ear);
        Line(d, "M-24,-36 Q-18,-22 -13,-14", "#F9D5C6", 1.6);
        d.Pop();
        if (right) d.Pop();
        d.Pop();
    }

    static void Star(DrawingContext d, double x, double y, double radius, string color, double opacity)
    {
        d.PushOpacity(opacity);
        d.PushTransform(new TranslateTransform(x, y));
        d.PushTransform(new ScaleTransform(radius, radius));
        Path(d, "M0,-1 Q.2,-.2 1,0 Q.2,.2 0,1 Q-.2,.2 -1,0 Q-.2,-.2 0,-1Z", Ink(color));
        d.Pop(); d.Pop(); d.Pop();
    }
    static void Heart(DrawingContext d, double x, double y, double scale)
    {
        d.PushTransform(new TranslateTransform(x, y));
        d.PushTransform(new ScaleTransform(scale, scale));
        Path(d, "M0,8 C-18,-3 -7,-12 0,-5 C7,-12 18,-3 0,8Z", Ink("#DEA498"));
        d.Pop(); d.Pop();
    }
}
