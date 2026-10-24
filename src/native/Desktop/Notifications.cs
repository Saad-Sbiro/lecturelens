using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LectureLens.Desktop;

internal static class Notifications
{
    internal static void Attach(FrameworkElement view)
    {
        var message = (TextBlock)view.FindName("SessionMessage");
        var toast = (Border)view.FindName("NotificationToast");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
        timer.Tick += (_, _) => { timer.Stop(); toast.Visibility = Visibility.Collapsed; };
        var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        EventHandler changed = (_, _) => {
            timer.Stop();
            var wasVisible = toast.Visibility == Visibility.Visible;
            toast.Visibility = string.IsNullOrWhiteSpace(message.Text) ? Visibility.Collapsed : Visibility.Visible;
            if (toast.Visibility == Visibility.Visible) { if (!wasVisible) Motion.Enter(toast); timer.Start(); }
        };
        descriptor.AddValueChanged(message, changed);
        ((Button)view.FindName("DismissNotification")).Click += (_, _) => { timer.Stop(); toast.Visibility = Visibility.Collapsed; };
        view.Unloaded += (_, _) => { timer.Stop(); toast.BeginAnimation(UIElement.OpacityProperty, null); descriptor.RemoveValueChanged(message, changed); };
    }

    internal static void PdfReady(FrameworkElement view, string path)
    {
        string S(string key) => (string)view.FindResource(key);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = S("done"), FontSize = 24, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = S("pdfReadyHint"), Margin = new Thickness(0, 12, 0, 20) });
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(feedback);
        var actions = new WrapPanel(); panel.Children.Add(actions);
        var dialog = new InAppDialog(view, panel);
        dialog.Dismiss = dialog.Close;
        void Add(string key, Action action) {
            var button = new Button { Content = S(key) }; actions.Children.Add(button);
            button.Click += (_, _) => { try { action(); } catch (Exception e) { feedback.Text = e.Message; } };
        }
        Add("openInBrowser", () => {
            var browser = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
            }.FirstOrDefault(File.Exists) ?? throw new IOException(S("browserUnavailable"));
            var start = new ProcessStartInfo(browser); start.ArgumentList.Add(new Uri(path).AbsoluteUri); Process.Start(start);
        });
        Add("openFileLocation", () => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }));
        Add("close", dialog.Close);
    }
}
