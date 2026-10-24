using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LectureLens.Desktop;
using LectureLens.Storage;

internal static class Program
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    [STAThread]
    static void Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "lecturelens-terms-ui-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("LECTURELENS_DATA_ROOT", root);
        try
        {
            var app = new Application();
            app.Resources["PanelBackground"] = new SolidColorBrush(Color.FromRgb(32, 35, 43));
            app.Resources["Danger"] = Brushes.Salmon;
            app.Resources["AppForeground"] = Brushes.White;
            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 10, 16, 10)));
            buttonStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 10, 0)));
            app.Resources[typeof(Button)] = buttonStyle;
            var primary = new Style(typeof(Button), buttonStyle);
            primary.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Goldenrod));
            app.Resources["Primary"] = primary;
            var shell = new Grid { Background = new SolidColorBrush(Color.FromRgb(25, 27, 33)), Width = 1000, Height = 700 };
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); shell.RowDefinitions.Add(new RowDefinition());
            var content = new Button { Content = "Existing app content" }; Grid.SetRow(content, 1); shell.Children.Add(content);
            var window = new Window { Content = shell, Foreground = Brushes.White, FontFamily = new FontFamily("Segoe UI"), FontSize = 14 };
            StackPanel Panel() => (StackPanel)((Border)shell.Children.OfType<Grid>().Single(g => g.Name == "DialogOverlay").Children[0]).Child;
            bool Open() => shell.Children.OfType<Grid>().Any(g => g.Name == "DialogOverlay");
            TermsNotice.Attach(shell, false);
            Check(Open() && !content.IsEnabled, "First run must show modal terms.");
            var panel = Panel(); var check = panel.Children.OfType<CheckBox>().Single();
            var accept = panel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().First();
            Check(!accept.IsEnabled && check.IsChecked != true, "Acceptance must be explicit.");
            shell.Measure(new Size(1000, 700)); shell.Arrange(new Rect(0, 0, 1000, 700)); shell.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1000, 700, 96, 96, PixelFormats.Pbgra32); bitmap.Render(shell);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory("artifacts/terms-preview");
            using (var file = File.Create("artifacts/terms-preview/desktop.png")) encoder.Save(file);
            check.IsChecked = true; Check(accept.IsEnabled, "Checkbox must enable acceptance.");
            var path = Path.Combine(LocalStore.DefaultRoot, "terms-acceptance.json");
            Directory.CreateDirectory(path); // Force a failed atomic save without touching real user data.
            accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Open() && !new TermsAgreement(LocalStore.DefaultRoot).IsAccepted(), "Failed save must not dismiss terms.");
            Check(Panel().Children.OfType<TextBlock>().Any(t => t.Text == TermsAgreement.Document.SaveError), "Save failure must be visible.");
            Directory.Delete(path);
            accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!Open() && content.IsEnabled, "Successful acceptance must close terms and restore app controls.");
            TermsNotice.Attach(shell, false); Check(!Open(), "Accepted terms must not reappear.");
            TermsNotice.Show(shell, false); Check(Open(), "Terms must remain available to read.");
            Check(Panel().Children.OfType<CheckBox>().Single().Visibility == Visibility.Collapsed, "Reading must not ask for acceptance again.");
            Panel().Children.OfType<WrapPanel>().Single().Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!Open(), "Read-only terms must close.");
            Console.WriteLine("PASS: desktop first run, explicit acceptance, failed-save retry, persistent dismissal, read-only terms and screenshot.");
            window.Close();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
