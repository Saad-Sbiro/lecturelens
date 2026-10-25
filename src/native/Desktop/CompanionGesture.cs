namespace LectureLens.Desktop;

// All gestures start and finish at rest. Pose blending preserves continuity on interruption.
internal readonly record struct CompanionGesture(double X = 0, double Y = 0, double Roll = 0,
    double Stretch = 0, double HeadRoll = 0, double HeadY = 0, double EarLeft = 0,
    double EarRight = 0, double PawLeft = 0, double PawRight = 0, double Smile = 0,
    double Wink = 0, double Spark = 0)
{
    internal static CompanionGesture Sample(CompanionMove move, double progress, int side)
    {
        var p = Math.Clamp(progress, 0, 1);
        var envelope = Math.Pow(Math.Sin(Math.PI * p), 2);
        var sway = Math.Sin(p * Math.PI * 6) * envelope;
        var lift = Math.Sin(p * Math.PI * 4) * envelope;
        return move switch
        {
            CompanionMove.Wave => new(Roll: side * -3 * envelope, HeadRoll: side * 5 * envelope,
                PawLeft: side < 0 ? (110 + 24 * Math.Sin(p * Math.PI * 8)) * envelope : 0,
                PawRight: side > 0 ? (110 + 24 * Math.Sin(p * Math.PI * 8)) * envelope : 0, Smile: .6 * envelope),
            CompanionMove.Hop => new(Y: -13 * Math.Abs(lift), Stretch: -.08 * lift, EarLeft: 12 * lift,
                EarRight: -12 * lift, PawLeft: 30 * envelope, PawRight: 30 * envelope, Smile: envelope),
            CompanionMove.Wiggle => new(X: sway * 5, Roll: sway * 9, HeadRoll: -sway * 7,
                PawLeft: (30 + 25 * sway) * envelope, PawRight: (30 - 25 * sway) * envelope, Smile: .7 * envelope),
            CompanionMove.HighFive => new(X: side * -3 * envelope, Roll: side * -6 * envelope,
                HeadRoll: side * 7 * envelope, PawLeft: side < 0 ? 155 * envelope : 0,
                PawRight: side > 0 ? 155 * envelope : 0, Smile: .45 * envelope, Spark: .5 * envelope),
            CompanionMove.Peek => new(Y: 5 * envelope, Roll: side * 11 * envelope, Stretch: -.04 * envelope,
                HeadRoll: side * -17 * envelope, EarLeft: 28 * envelope, EarRight: -15 * envelope,
                PawLeft: 75 * envelope, PawRight: 75 * envelope),
            CompanionMove.Stretch => new(Y: -3 * envelope, Stretch: .055 * envelope,
                EarLeft: -10 * envelope, EarRight: -10 * envelope, PawLeft: 140 * envelope,
                PawRight: 140 * envelope, Smile: .4 * envelope),
            CompanionMove.Wink => new(Roll: side * 4 * envelope, HeadRoll: side * -8 * envelope,
                PawRight: 45 * envelope, Wink: envelope, Smile: .3 * envelope),
            CompanionMove.Spark => new(Y: -5 * envelope, PawLeft: 90 * envelope, PawRight: 90 * envelope,
                EarLeft: -12 * envelope, EarRight: -12 * envelope, Smile: envelope, Spark: envelope),
            CompanionMove.Nod => new(HeadY: 4 * lift, HeadRoll: 2 * envelope),
            CompanionMove.Tilt => new(HeadRoll: side * 13 * envelope, EarLeft: 18 * envelope, EarRight: -8 * envelope),
            CompanionMove.Perk => new(HeadY: -3 * envelope, EarLeft: -18 * envelope, EarRight: 20 * envelope),
            _ => default
        };
    }

    internal static CompanionGesture Blend(CompanionGesture a, CompanionGesture b, double amount)
    {
        var t = amount * amount * (3 - 2 * amount);
        double Mix(double x, double y) => x + (y - x) * t;
        return new(Mix(a.X, b.X), Mix(a.Y, b.Y), Mix(a.Roll, b.Roll), Mix(a.Stretch, b.Stretch),
            Mix(a.HeadRoll, b.HeadRoll), Mix(a.HeadY, b.HeadY), Mix(a.EarLeft, b.EarLeft),
            Mix(a.EarRight, b.EarRight), Mix(a.PawLeft, b.PawLeft), Mix(a.PawRight, b.PawRight),
            Mix(a.Smile, b.Smile), Mix(a.Wink, b.Wink), Mix(a.Spark, b.Spark));
    }
}
