namespace LectureLens.Desktop;

internal enum CompanionMove { None, Wave, Hop, Wiggle, HighFive, Peek, Stretch, Wink, Spark, Nod, Tilt, Perk }

// Presentation only: elapsed time is supplied so reactions can be replayed offline.
internal sealed class CompanionPose
{
    internal double LookX { get; private set; }
    internal double LookY { get; private set; }
    internal double HeadX { get; private set; }
    internal double HeadY { get; private set; }
    internal double BodyX { get; private set; }
    internal double BodyY { get; private set; }
    internal double Time { get; private set; }
    internal double Rest { get; private set; }
    internal bool Reduced { get; private set; }
    internal CompanionMove Move { get; private set; }
    internal CompanionGesture Gesture { get; private set; }
    internal bool Personal { get; private set; }
    internal double Progress => Math.Clamp(reactionTime / duration, 0, 1);
    internal int Variation { get; private set; }
    static readonly CompanionMove[] playful = [CompanionMove.Wave, CompanionMove.Hop, CompanionMove.Peek, CompanionMove.Wiggle, CompanionMove.HighFive, CompanionMove.Wink, CompanionMove.Stretch, CompanionMove.Spark];
    static readonly CompanionMove[] gentle = [CompanionMove.Nod, CompanionMove.Perk, CompanionMove.Tilt];
    double targetX, targetY, idle, reactionTime, duration = 1, lastNotice = -10;
    int playfulIndex, gentleIndex, serial;
    CompanionGesture interrupted;

    // Normalize each side independently: the distant left side of the workspace must
    // keep changing the gaze, instead of saturating a few pixels outside the card.
    internal static double TrackAxis(double pointer, double center, double extent)
    {
        var delta = pointer - center;
        var reach = Math.Max(1, delta < 0 ? center : extent - center);
        return Math.Sign(delta) * Math.Pow(Math.Clamp(Math.Abs(delta) / reach, 0, 1), .65);
    }

    internal void Look(double x, double y)
    {
        targetX = Math.Clamp(x, -1, 1);
        targetY = Math.Clamp(y, -1, 1);
        idle = 0;
    }

    internal void React(bool personal = false, bool quiet = false)
    {
        idle = 0;
        var moves = quiet ? gentle : playful;
        var index = quiet ? gentleIndex++ : playfulIndex++;
        interrupted = Gesture;
        Move = moves[index % moves.Length];
        Variation = (serial + serial / playful.Length) % 2 == 0 ? 1 : -1;
        serial++;
        Personal = personal;
        reactionTime = 0;
        duration = quiet ? 1.1 : Move is CompanionMove.Wave or CompanionMove.Stretch ? 1.85 : 1.5;
    }

    internal void NoticeTyping()
    {
        idle = 0;
        // Let a gesture finish while typing continuously, and preserve deliberate clicks.
        if (Time - lastNotice < 1.8 || Move != CompanionMove.None) return;
        lastNotice = Time;
        React(quiet: true);
    }

    internal void Step(double seconds, bool animate)
    {
        var dt = Math.Clamp(seconds, 0, .05);
        Reduced = !animate;
        if (!animate)
        {
            LookX = LookY = HeadX = HeadY = BodyX = BodyY = Rest = Time = 0;
            Move = CompanionMove.None;
            Gesture = interrupted = default;
            lastNotice = -10;
            return;
        }
        Time += dt;
        idle += dt;
        // Eyes arrive first, then the head and body catch up with separate damping.
        LookX = Follow(LookX, targetX, dt, 19);
        LookY = Follow(LookY, targetY, dt, 19);
        HeadX = Follow(HeadX, LookX, dt, 10);
        HeadY = Follow(HeadY, LookY, dt, 10);
        BodyX = Follow(BodyX, HeadX, dt, 6);
        BodyY = Follow(BodyY, HeadY, dt, 6);
        Rest = Follow(Rest, idle > 24 ? 1 : 0, dt, 8);
        if (Move != CompanionMove.None)
        {
            reactionTime += dt;
            var next = CompanionGesture.Sample(Move, Progress, Variation);
            Gesture = CompanionGesture.Blend(interrupted, next, Math.Clamp(reactionTime / .16, 0, 1));
            if (Progress >= 1) { Move = CompanionMove.None; Gesture = default; }
        }
    }

    static double Follow(double from, double to, double dt, double speed) => from + (to - from) * (1 - Math.Exp(-dt * speed));
}
