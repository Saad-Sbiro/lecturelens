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
        // Class handlers also cover buttons created later in dialogs, without per-view subscriptions.
        EventManager.RegisterClassHandler(typeof(Button), Mouse.MouseEnterEvent, new MouseEventHandler((sender, _) => Hover((Button)sender, true)));
        EventManager.RegisterClassHandler(typeof(Button), Mouse.MouseLeaveEvent, new MouseEventHandler((sender, _) => Hover((Button)sender, false)));
        EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((sender, _) => ClearSurface((Button)sender)));
        EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewKeyDownEvent, new KeyEventHandler((sender, e) => { if (e.Key is Key.Space or Key.Enter) ClearSurface((Button)sender); }));
        EventManager.RegisterClassHandler(typeof(Button), FrameworkElement.UnloadedEvent, new RoutedEventHandler((sender, _) => ClearSurface((Button)sender)));
    }

    static FrameworkElement? Surface(Button button) => button.Template?.FindName("Surface", button) as FrameworkElement;
    static void ClearSurface(Button button) { Surface(button)?.BeginAnimation(UIElement.OpacityProperty, null); }
    static void Hover(Button button, bool over)
    {
        var surface = Surface(button);
        if (surface == null || !button.IsEnabled || button.IsPressed) return;
        // Preserve the existing template's exact hover/resting opacity and focus border.
        var from = surface.HasAnimatedProperties ? surface.Opacity : over ? 1 : .8;
        Fade(surface, from, over ? .8 : 1, 110, Enabled);
    }

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
