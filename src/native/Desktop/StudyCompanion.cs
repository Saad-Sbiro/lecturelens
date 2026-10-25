using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace LectureLens.Desktop;

internal sealed class StudyCompanion
{
    readonly FrameworkElement root;
    readonly Button button;
    readonly CompanionDrawing drawing = new();
    readonly DispatcherTimer timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
    readonly Stopwatch elapsed = new();
    Window? window;
    bool attached;
    readonly Func<double> nextChance;
    internal CompanionPose Pose => drawing.Pose;
    internal bool Running => timer.IsEnabled;

    internal static StudyCompanion? Attach(FrameworkElement root, Func<double>? nextChance = null)
    {
        if (root.FindName("CompanionButton") is not Button button) return null;
        if (button.Tag is StudyCompanion existing) return existing;
        var companion = new StudyCompanion(root, button, nextChance ?? Random.Shared.NextDouble);
        button.Tag = companion;
        return companion;
    }

    StudyCompanion(FrameworkElement root, Button button, Func<double> nextChance)
    {
        this.root = root;
        this.button = button;
        this.nextChance = nextChance;
        ((ContentControl)root.FindName("CompanionArt")).Content = drawing;
        button.Loaded += Load;
        button.Unloaded += Unload;
        button.IsVisibleChanged += VisibilityChanged;
        timer.Tick += Tick;
        if (button.IsLoaded) Load(button, new RoutedEventArgs());
    }

    void Load(object sender, RoutedEventArgs e)
    {
        if (attached) return;
        attached = true;
        window = Window.GetWindow(button);
        root.PreviewMouseMove += Follow;
        root.MouseLeave += Leave;
        root.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Click), true);
        SystemParameters.StaticPropertyChanged += PreferencesChanged;
        if (window != null)
        {
            window.StateChanged += WindowChanged;
            window.Activated += WindowChanged;
            window.Deactivated += WindowChanged;
        }
        UpdateTimer();
    }

    void Unload(object sender, RoutedEventArgs e)
    {
        attached = false;
        timer.Stop();
        elapsed.Reset();
        root.PreviewMouseMove -= Follow;
        root.MouseLeave -= Leave;
        root.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Click));
        SystemParameters.StaticPropertyChanged -= PreferencesChanged;
        if (window != null)
        {
            window.StateChanged -= WindowChanged;
            window.Activated -= WindowChanged;
            window.Deactivated -= WindowChanged;
            window = null;
        }
    }

    void Follow(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(root);
        var center = drawing.TranslatePoint(new Point(drawing.ActualWidth / 2, drawing.ActualHeight * .5), root);
        Pose.Look(CompanionPose.TrackAxis(point.X, center.X, root.ActualWidth),
            CompanionPose.TrackAxis(point.Y, center.Y, root.ActualHeight));
    }
    void Leave(object sender, MouseEventArgs e) => Pose.Look(0, 0);
    void Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not ButtonBase source || !source.IsEnabled) return;
        if (source.Name is "CloseWindow" or "MinimizeWindow" or "MaximizeWindow") return;
        if (Pose.Move != CompanionMove.None || nextChance() >= .7) return;
        Pose.React(personal: ReferenceEquals(source, button), quiet: source.Name is "Delete" or "Stop" or "CancelGeneration");
    }
    void PreferencesChanged(object? sender, PropertyChangedEventArgs e) => button.Dispatcher.InvokeAsync(UpdateTimer);
    void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateTimer();
    void WindowChanged(object? sender, EventArgs e) => UpdateTimer();

    void UpdateTimer()
    {
        if (!attached) return;
        if (Motion.Enabled && button.IsVisible && window is { IsActive: true } && window.WindowState != WindowState.Minimized)
        {
            elapsed.Restart();
            timer.Start();
        }
        else
        {
            timer.Stop();
            elapsed.Reset();
            Pose.Step(0, false);
            drawing.InvalidateVisual();
        }
    }
    void Tick(object? sender, EventArgs e)
    {
        Pose.Step(elapsed.Elapsed.TotalSeconds, Motion.Enabled);
        elapsed.Restart();
        drawing.InvalidateVisual();
    }
}
