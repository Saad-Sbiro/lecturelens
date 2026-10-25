using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    long selectionTouch;
    internal CompanionPose Pose => drawing.Pose;
    internal bool Running => timer.IsEnabled;

    internal static StudyCompanion? Attach(FrameworkElement root)
    {
        if (root.FindName("CompanionButton") is not Button button) return null;
        if (button.Tag is StudyCompanion existing) return existing;
        var companion = new StudyCompanion(root, button);
        button.Tag = companion;
        return companion;
    }

    StudyCompanion(FrameworkElement root, Button button)
    {
        this.root = root;
        this.button = button;
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
        root.PreviewMouseDown += Touch;
        root.MouseLeave += Leave;
        root.PreviewKeyDown += Key;
        root.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Click), true);
        root.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(Selection), true);
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
        root.PreviewMouseDown -= Touch;
        root.MouseLeave -= Leave;
        root.PreviewKeyDown -= Key;
        root.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Click));
        root.RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(Selection));
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
    void Touch(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // Button/selection activation has its own routed event, including keyboard use.
        // React to other workspace clicks without consuming them or double-triggering.
        for (var node = e.OriginalSource as DependencyObject; node != null;)
        {
            if (node is ButtonBase || node is FrameworkElement { Name: "TitleBar" }) return;
            if (node is Selector)
            {
                selectionTouch = Stopwatch.GetTimestamp();
                Pose.React(quiet: true);
                return;
            }
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        Pose.React();
    }
    void Leave(object sender, MouseEventArgs e) => Pose.Look(0, 0);
    void Key(object sender, KeyEventArgs e)
    {
        // React to activity, never read or retain the user's text.
        if (!e.IsRepeat && e.Key is not (System.Windows.Input.Key.Tab or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift))
            Pose.NoticeTyping();
    }
    void Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not ButtonBase source || !source.IsEnabled) return;
        if (source.Name is "CloseWindow" or "MinimizeWindow" or "MaximizeWindow") return;
        Pose.React(personal: ReferenceEquals(source, button), quiet: source.Name is "Delete" or "Stop" or "CancelGeneration");
    }
    void Selection(object sender, SelectionChangedEventArgs e)
    {
        if (selectionTouch != 0 && Stopwatch.GetElapsedTime(selectionTouch).TotalMilliseconds < 150) return;
        if (e.OriginalSource is FrameworkElement element && element.Name is "Sessions" or "TeacherLanguage" or "PdfLanguage" or "Detail")
            Pose.React(quiet: true);
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
