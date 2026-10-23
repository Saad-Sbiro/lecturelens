using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LectureLens.Core;
using LectureLens.Storage;
using LectureLens.Desktop.Adapters;

namespace LectureLens.Desktop;

internal static class MergeWindow
{
    internal static string? Show(FrameworkElement view, IReadOnlyList<SessionInfo> lessons)
    {
        string S(string key) => (string)view.FindResource(key);
        var window = new Window { Owner = Window.GetWindow(view), Title = S("mergeTitle"), Width = 740, Height = 720,
            MinWidth = 600, MinHeight = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Resources = view.Resources, Background = (view as Panel)?.Background, Foreground = TextElement.GetForeground(view) };
        var panel = new StackPanel { Margin = new Thickness(24) };
        var layout = new DockPanel(); window.Content = layout;
        var footer = new StackPanel { Margin = new Thickness(24, 0, 24, 18) };
        DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(new TextBlock { Text = S("mergeTitle"), FontSize = 23, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = S("mergeHint"), Margin = new Thickness(0, 10, 0, 18) });
        var name = new TextBox { Text = S("mergeDefaultName"), MaxLength = 120 };
        panel.Children.Add(new Label { Content = S("mergeName"), Target = name, Foreground = window.Foreground }); panel.Children.Add(name);
        var available = new ComboBox { ItemsSource = lessons.Where(s => s.State is "finished" or "interrupted" && s.Chunks > 0 && s.Problems.Length == 0).ToArray(), DisplayMemberPath = "Title" };
        panel.Children.Add(new Label { Content = S("mergeChoose"), Target = available, Foreground = window.Foreground }); panel.Children.Add(available);
        Button Action(string key, Panel parent) {
            var button = new Button { Content = S(key) }; parent.Children.Add(button); return button;
        }
        var add = Action("mergeAdd", panel);
        var order = new List<SessionInfo>();
        var list = new ListBox { Height = 120, Margin = new Thickness(0, 12, 0, 12), Background = Brushes.Transparent, Foreground = window.Foreground };
        list.ItemContainerStyle = new Style(typeof(ListBoxItem));
        list.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        System.Windows.Automation.AutomationProperties.SetName(list, S("mergeOrder"));
        panel.Children.Add(new TextBlock { Text = S("mergeOrder"), Margin = new Thickness(0, 10, 0, 0) }); panel.Children.Add(list);
        var actions = new WrapPanel(); footer.Children.Add(actions);
        var up = Action("mergeUp", actions); var down = Action("mergeDown", actions); var remove = Action("mergeRemove", actions);
        var status = new TextBlock { Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap }; footer.Children.Add(status);
        var bottom = new WrapPanel(); footer.Children.Add(bottom);
        var merge = Action("mergeCreate", bottom); merge.Style = (Style)view.FindResource("Primary");
        var cancel = Action("cancelGeneration", bottom);
        CancellationTokenSource? running = null; string? result = null;
        void Buttons() {
            var selected = list.SelectedIndex; var editable = running == null;
            available.IsEnabled = name.IsEnabled = list.IsEnabled = editable;
            add.IsEnabled = editable && order.Count < 3 && available.SelectedItem is SessionInfo candidate && order.All(s => s.Id != candidate.Id);
            up.IsEnabled = editable && selected > 0;
            down.IsEnabled = editable && selected >= 0 && selected < order.Count - 1;
            remove.IsEnabled = editable && selected >= 0;
            merge.IsEnabled = editable && order.Count is 2 or 3 && !string.IsNullOrWhiteSpace(name.Text);
        }
        void Refresh(int selected) { list.ItemsSource = order.Select((s, i) => $"{i + 1}. {s.Title} · {s.DisplayDate}").ToArray(); list.SelectedIndex = selected; Buttons(); }
        available.SelectionChanged += (_, _) => Buttons(); list.SelectionChanged += (_, _) => Buttons(); name.TextChanged += (_, _) => Buttons();
        add.Click += (_, _) => { if (available.SelectedItem is SessionInfo item && order.Count < 3 && order.All(s => s.Id != item.Id)) { order.Add(item); Refresh(order.Count - 1); } };
        void Move(int delta) { int index = list.SelectedIndex, next = index + delta; if (index < 0 || next < 0 || next >= order.Count) return; (order[index], order[next]) = (order[next], order[index]); Refresh(next); }
        up.Click += (_, _) => Move(-1); down.Click += (_, _) => Move(1);
        remove.Click += (_, _) => { int index = list.SelectedIndex; if (index < 0) return; order.RemoveAt(index); Refresh(Math.Min(index, order.Count - 1)); };
        cancel.Click += (_, _) => { if (running != null) running.Cancel(); else window.Close(); };
        window.Closing += (_, e) => { if (running != null) { e.Cancel = true; running.Cancel(); } };
        merge.Click += async (_, _) => {
            var ids = order.Select(s => s.Id).ToArray(); var title = name.Text;
            running = new CancellationTokenSource(); Buttons(); status.Text = S("mergeWorking");
            try {
                result = await Task.Run(() => new RecordingMerge().Run(LocalStore.DefaultRoot, ids, title,
                    new Progress<int>(part => window.Dispatcher.Invoke(() => status.Text = S("mergeWorking") + $" {part}/{ids.Length}")), running.Token));
            }
            catch (OperationCanceledException) { status.Text = S("mergeCancelled"); }
            catch (Exception e) { status.Text = S("mergeFailed") + " " + e.Message; }
            finally { running.Dispose(); running = null; Buttons(); }
            if (result != null) window.Close();
        };
        Buttons(); window.ShowDialog(); return result;
    }
}
