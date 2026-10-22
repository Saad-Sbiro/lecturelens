using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LectureLens.Core;
using LectureLens.Storage;

namespace LectureLens.Desktop;

internal sealed class SessionPanel(FrameworkElement view)
{
    private readonly LocalStore store = new(LocalStore.DefaultRoot);
    private readonly DataGrid sessions = (DataGrid)view.FindName("Sessions");
    private readonly TextBlock message = (TextBlock)view.FindName("SessionMessage");
    private bool busy;
    public void Attach()
    {
        ((Button)view.FindName("ViewSlides")).Click += async (_, _) => await Run(() =>
        {
            if (sessions.SelectedItem is SessionInfo selected)
            {
                var path = Path.Combine(store.Root, selected.Id, "slides", "index.html");
                if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else message.Text = (string)view.FindResource("slidesPending");
            }
            return Task.CompletedTask;
        });
        ((Button)view.FindName("Refresh")).Click += async (_, _) => await Run(async () => { sessions.ItemsSource = await Task.Run(store.List); });
        ((Button)view.FindName("Recover")).Click += async (_, _) => await Run(async () =>
        {
            if (sessions.SelectedItem is not SessionInfo selected) return;
            var path = await Task.Run(() => store.Recover(selected.Id));
            message.Text = path;
        });
        ((Button)view.FindName("Stop")).Click += async (_, _) => await Run(() =>
        {
            if (sessions.SelectedItem is SessionInfo selected) { store.RequestStop(selected.Id); message.Text = (string)view.FindResource("stopRequested"); }
            return Task.CompletedTask;
        });
        ((Button)view.FindName("Delete")).Click += async (_, _) => await Run(async () =>
        {
            if (sessions.SelectedItem is not SessionInfo selected) return;
            if (MessageBox.Show((string)view.FindResource("deleteConfirm"), "LectureLens", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await Task.Run(() => store.Delete(selected.Id)); sessions.ItemsSource = await Task.Run(store.List);
        });
        ((Button)view.FindName("ExtensionFolder")).Click += (_, _) =>
        {
            var path = Environment.GetEnvironmentVariable("LECTURELENS_EXTENSION");
                        if (path == null || !Directory.Exists(path)) { message.Text = (string)view.FindResource("extensionUnavailable"); return; }
            try {
                Clipboard.SetText(Path.GetFullPath(path));
                var chrome = new[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
                }.FirstOrDefault(File.Exists);
                if (chrome != null) {
                    var start = new ProcessStartInfo(chrome) { UseShellExecute = false };
                    start.ArgumentList.Add("chrome://extensions");
                    Process.Start(start);
                }
                message.Text = (string)view.FindResource("extensionHelp");
                MessageBox.Show(message.Text, "LectureLens");
            } catch (Exception e) { message.Text = e.Message; }
        };
        // Verify packets on explicit refresh, never repeatedly scan hours of media on a UI timer.
        _ = Run(async () => { sessions.ItemsSource = await Task.Run(store.List); });
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return;
        busy = true; message.Text = "";
        try { await action(); }
        catch (Exception e) { message.Text = e.Message; }
        finally { busy = false; }
    }
}
