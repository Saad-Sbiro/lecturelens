using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LectureLens.Desktop;

internal static class SmoothScroll
{
    static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached("Offset", typeof(double), typeof(SmoothScroll),
        new PropertyMetadata(0d, (owner, change) => ((ScrollViewer)owner).ScrollToVerticalOffset((double)change.NewValue)));
    internal static void Attach(ScrollViewer scroll)
    {
        scroll.PreviewMouseWheel += (_, e) => {
            // Preserve item-based virtualization, zoom shortcuts and native scrolling at boundaries.
            if (!Motion.Enabled || scroll.CanContentScroll || Keyboard.Modifiers != ModifierKeys.None || scroll.ScrollableHeight <= 0) return;
            var child = e.OriginalSource as DependencyObject;
            while (child != null && child is not ScrollViewer) child = child is Visual ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
            if (child != scroll) return;
            var current = scroll.VerticalOffset;
            var pending = (double)scroll.GetAnimationBaseValue(OffsetProperty);
            var start = scroll.HasAnimatedProperties ? pending : current;
            var lines = SystemParameters.WheelScrollLines;
            var step = lines < 0 ? scroll.ViewportHeight : lines * 20d;
            var target = Math.Clamp(start - e.Delta / 120d * step, 0, scroll.ScrollableHeight);
            if (Math.Abs(target - current) < .5) return;
            Cancel(scroll); scroll.SetValue(OffsetProperty, target);
            var animation = new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(180)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (_, _) => scroll.BeginAnimation(OffsetProperty, null);
            scroll.BeginAnimation(OffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
            e.Handled = true;
        };
        scroll.PreviewMouseDown += (_, _) => Cancel(scroll);
        scroll.PreviewKeyDown += (_, _) => Cancel(scroll);
        scroll.Unloaded += (_, _) => Cancel(scroll);
    }
    internal static void Cancel(ScrollViewer scroll)
    {
        var current = scroll.VerticalOffset;
        scroll.SetValue(OffsetProperty, current); scroll.BeginAnimation(OffsetProperty, null);
    }
}
