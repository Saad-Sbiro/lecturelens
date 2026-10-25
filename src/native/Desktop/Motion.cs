using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LectureLens.Desktop;

internal static class Motion
{
    static bool initialized;
    internal static bool Enabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        SystemParameters.StaticPropertyChanged += (_, _) => {
            if (!Enabled) Application.Current?.Dispatcher.InvokeAsync(ControlMotion.ReduceMotion);
        };
        // Press feedback stays responsive; hover styling is immediate and does not animate.
        EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((sender, _) => ClearSurface((Button)sender)));
        EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewKeyDownEvent, new KeyEventHandler((sender, e) => { if (e.Key is Key.Space or Key.Enter) ClearSurface((Button)sender); }));
        EventManager.RegisterClassHandler(typeof(Button), FrameworkElement.UnloadedEvent, new RoutedEventHandler((sender, _) => ClearSurface((Button)sender)));
    }

    static FrameworkElement? Surface(Button button) => button.Template?.FindName("Surface", button) as FrameworkElement;
    static void ClearSurface(Button button) { Surface(button)?.BeginAnimation(UIElement.OpacityProperty, null); }
    internal static void Enter(UIElement element)
    {
        if (element is FrameworkElement view) SpatialMotion.Reveal(view);
    }

    internal static void Fade(UIElement element, double from, double to, int milliseconds, bool enabled)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (!enabled) return;
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => element.BeginAnimation(UIElement.OpacityProperty, null);
        element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
