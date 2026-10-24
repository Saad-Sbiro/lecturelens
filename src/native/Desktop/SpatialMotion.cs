using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LectureLens.Desktop;

internal static class SpatialMotion
{
    sealed class Pose
    {
        internal readonly ScaleTransform Scale = new();
        internal readonly TranslateTransform Shift = new();
    }
    static readonly ConditionalWeakTable<FrameworkElement, Pose> poses = new();
    internal static void ResetAll() { foreach (var entry in poses) Reset(entry.Key); }
    static Pose For(FrameworkElement element) => poses.GetValue(element, view => {
        var pose = new Pose();
        var group = new TransformGroup();
        if (view.RenderTransform != Transform.Identity) group.Children.Add(view.RenderTransform);
        group.Children.Add(pose.Scale); group.Children.Add(pose.Shift);
        view.RenderTransform = group; view.RenderTransformOrigin = new Point(.5, .5);
        view.Unloaded += (_, _) => Reset(view);
        return pose;
    });

    internal static void To(FrameworkElement view, double scale = 1, double x = 0, double y = 0, int duration = 240)
    {
        if (!Motion.Enabled) { Reset(view); return; }
        var pose = For(view);
        Animate(pose.Scale, ScaleTransform.ScaleXProperty, scale, duration);
        Animate(pose.Scale, ScaleTransform.ScaleYProperty, scale, duration);
        Animate(pose.Shift, TranslateTransform.XProperty, x, duration);
        Animate(pose.Shift, TranslateTransform.YProperty, y, duration);
    }
    static void Animate(Animatable target, DependencyProperty property, double to, int duration)
    {
        var from = (double)target.GetValue(property);
        target.BeginAnimation(property, null); target.SetValue(property, to);
        if (!Motion.Enabled || Math.Abs(from - to) < .0001) return;
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(duration)) {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => target.BeginAnimation(property, null);
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
    internal static void Reveal(FrameworkElement view, bool dialog = false)
    {
        if (!Motion.Enabled) { Reset(view); return; }
        var pose = For(view);
        Reset(view);
        pose.Scale.ScaleX = pose.Scale.ScaleY = dialog ? .95 : 1;
        pose.Shift.Y = dialog ? 18 : 12;
        To(view, duration: 320);
        Motion.Fade(view, .15, 1, 240, true);
    }
    internal static void Reset(FrameworkElement view)
    {
        if (!poses.TryGetValue(view, out var pose)) return;
        pose.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); pose.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        pose.Shift.BeginAnimation(TranslateTransform.XProperty, null); pose.Shift.BeginAnimation(TranslateTransform.YProperty, null);
        pose.Scale.ScaleX = pose.Scale.ScaleY = 1; pose.Shift.X = pose.Shift.Y = 0;
        view.BeginAnimation(UIElement.OpacityProperty, null);
    }
}
