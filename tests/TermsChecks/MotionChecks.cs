using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using LectureLens.Desktop;

internal static class MotionChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Advance(int milliseconds = 230)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    internal static void Run()
    {
        var surface = new Border { Opacity = .42 };
        var host = new Window { Content = surface, Width = 60, Height = 60, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        host.Show();
        try {
        Advance();
        Console.WriteLine($"Windows animation preference enabled: {Motion.Enabled}");
        SpatialMotion.Reveal(surface, true);
        Advance(40);
        if (Motion.Enabled) Check(surface.RenderTransform.Value.M22 < 1, "Dialog must visibly scale at entry.");
        Advance(); Advance();
        Check(surface.RenderTransform.Value.IsIdentity, "Spatial entry must settle back to original geometry.");
        SpatialMotion.To(surface, .96, y: -2);
        SpatialMotion.Reset(surface);
        Check(surface.RenderTransform.Value.IsIdentity, "Reset must immediately restore geometry.");
        Motion.Fade(surface, .82, 1, 110, false);
        Check(!surface.HasAnimatedProperties && surface.Opacity == .42, "Reduced motion must preserve base styling without clocks.");
        for (var i = 0; i < 30; i++) Motion.Fade(surface, .82, 1, 110, true);
        Advance();
        Check(!surface.HasAnimatedProperties && surface.Opacity == .42, "Rapid transitions must release clocks and restore base opacity.");
        Motion.Fade(surface, .82, 1, 110, true);
        Motion.Fade(surface, .82, 1, 110, false);
        Check(!surface.HasAnimatedProperties, "Reduced motion must cancel an in-flight transition.");
        var button = new Button { Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button"><Border x:Name="Surface"><ContentPresenter/></Border></ControlTemplate>
            """) };
        host.Content = button;
        button.ApplyTemplate();
        var face = (Border)button.Template.FindName("Surface", button);
        Motion.Initialize(); Motion.Initialize();
        ControlMotion.AttachTree(button);
        button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Advance(40);
        if (Motion.Enabled) Check(button.RenderTransform.Value.OffsetY < 0, "Hover must visibly lift the button.");
        button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        Advance(40);
        if (Motion.Enabled) Check(button.RenderTransform.Value.M22 < 1, "Press must visibly compress the button.");
        button.IsEnabled = false;
        Check(button.RenderTransform.Value.IsIdentity, "Disabling a control must reset its motion.");
        button.IsEnabled = true;
        Motion.Fade(face, 1, .8, 110, true);
        button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });
        Check(!face.HasAnimatedProperties, "Press feedback must not wait for hover animation.");
        Motion.Fade(face, 1, .8, 110, true);
        button.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check(!face.HasAnimatedProperties, "Unloading must release button animation clocks.");
        var scroll = new ScrollViewer { Content = new Border { Height = 1500 }, CanContentScroll = false };
        host.Content = scroll; host.UpdateLayout(); Advance(); ControlMotion.AttachTree(scroll);
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        scroll.RaiseEvent(wheel); Advance(40);
        if (Motion.Enabled) Check(wheel.Handled && scroll.VerticalOffset > 0, "Wheel must move through intermediate offsets.");
        var beforeCancel = scroll.VerticalOffset;
        SmoothScroll.Cancel(scroll); Advance();
        Check(Math.Abs(scroll.VerticalOffset - beforeCancel) < 1, "Cancelling scroll must preserve its current position.");
        } finally { host.Close(); }
        Console.WriteLine("PASS: visible dialog scale, button lift/press, smooth scroll, reduced motion, rapid replacement and cleanup.");
    }
}
