using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
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
    private bool detached;
    private string? selectionId;
    public void Attach()
    {
        ((Button)view.FindName("RenameLesson")).Click += (_, _) => Rename();
        ((Button)view.FindName("DesktopCapture")).Click += async (_, _) => await Run(() => { DesktopCapture.Show(view); return Task.CompletedTask; });
        ((Button)view.FindName("MergeRecordings")).Click += async (_, _) => await Run(async () => {
            if (NotesStudio.IsGenerating) return;
            var lessons = await Task.Run(store.List);
            var merged = await MergeWindow.Show(view, lessons);
            if (merged != null) { selectionId = merged; sessions.SelectedItem = null; await Refresh(); message.Text = (string)view.FindResource("mergeDone"); }
        });
        ((Button)view.FindName("ViewSlides")).Click += async (_, _) => await Run(() =>
        {
            if (sessions.SelectedItem is SessionInfo selected)
            {
                var folder = Path.Combine(store.Root, selected.Id);
                var slides = Path.Combine(folder, "slides");
                if (Directory.Exists(slides) && Directory.EnumerateFiles(slides, "*.slide.json").Any()) SlideReviewWindow.ShowFor(view, folder);
                else message.Text = (string)view.FindResource("slidesPending");
            }
            return Task.CompletedTask;
        });
        ((Button)view.FindName("Refresh")).Click += async (_, _) => await Run(async () => { await Refresh(); });
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
            await Task.Run(() => store.Delete(selected.Id)); await Refresh(); message.Text = (string)view.FindResource("lessonDeleted");
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
        Directory.CreateDirectory(store.Root);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += async (_, _) => { if (NotesStudio.IsGenerating || busy) return; timer.Stop(); await Run(Refresh); };
        var watcher = new FileSystemWatcher(store.Root) { IncludeSubdirectories = true, EnableRaisingEvents = true };
        void Schedule(object? sender, FileSystemEventArgs e) {
            if (Path.GetFileName(e.FullPath) is "manifest.json" or "finished.json")
                view.Dispatcher.BeginInvoke(() => { timer.Stop(); timer.Start(); });
        }
        watcher.Created += Schedule;
        watcher.Renamed += (sender, e) => Schedule(sender, e);
        view.Unloaded += (_, _) => { detached = true; watcher.Dispose(); timer.Stop(); };
        // Attach can run after Loaded (including replacing content in a visible
        // window). Queue the initial scan explicitly instead of missing that event.
        view.Dispatcher.InvokeAsync(async () => { if (!detached) await Run(Refresh); }, DispatcherPriority.Loaded);
        ((TextBox)view.FindName("SearchLessons")).TextChanged += (_, _) => Filter();
    }
    private async Task Refresh()
    {
        selectionId = (sessions.SelectedItem as SessionInfo)?.Id ?? selectionId;
        var items = await Task.Run(store.List).ConfigureAwait(false);
        await view.Dispatcher.InvokeAsync(() => {
            if (detached) return;
            sessions.ItemsSource = items;
            Filter();
            sessions.SelectedItem = items.FirstOrDefault(x => x.Id == selectionId);
        });
    }
    private void Filter()
    {
        if (sessions.ItemsSource == null) return;
        var query = ((TextBox)view.FindName("SearchLessons")).Text.Trim();
        CollectionViewSource.GetDefaultView(sessions.ItemsSource).Filter = item =>
            item is SessionInfo lesson && (query.Length == 0 || lesson.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }
    private void Rename()
    {
        if (NotesStudio.IsGenerating) return;
        if (sessions.SelectedItem is not SessionInfo lesson) { message.Text = (string)view.FindResource("chooseSession"); return; }
        var dialog = new Window { Owner=Window.GetWindow(view), Title=(string)view.FindResource("renameTitle"), Width=430, Height=215,
            WindowStartupLocation=WindowStartupLocation.CenterOwner, ResizeMode=ResizeMode.NoResize };
        var panel = new StackPanel { Margin=new Thickness(22) };
        panel.Children.Add(new TextBlock { Text=(string)view.FindResource("renameHint"), Margin=new Thickness(0,0,0,10) });
        var input = new TextBox { Text=lesson.Title, MaxLength=120, Padding=new Thickness(10) }; panel.Children.Add(input);
        var save = new Button { Content=(string)view.FindResource("save"), Padding=new Thickness(18,8,18,8), Margin=new Thickness(0,15,0,0), IsDefault=true };
        panel.Children.Add(save); dialog.Content=panel;
        save.Click += async (_, _) => {
            try { store.Rename(lesson.Id,input.Text); dialog.Close(); await Run(Refresh); message.Text = (string)view.FindResource("lessonRenamed"); }
            catch(Exception e) { message.Text=e.Message; }
        };
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        dialog.ShowDialog();
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

