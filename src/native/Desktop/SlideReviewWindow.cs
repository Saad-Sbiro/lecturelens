using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShapePath = System.Windows.Shapes.Path;
using LectureLens.Desktop.Adapters;

namespace LectureLens.Desktop;

internal sealed class SlideReviewWindow : UserControl
{
    private readonly string sessionFolder;
    private readonly WrapPanel cards = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock count = new();
    private readonly Button undo;
    private readonly Button restore;
    private readonly HashSet<int> excluded;
    private readonly Stack<int> undoStack = new();
    private SlideItem[] slides = [];
    private readonly CancellationTokenSource loading = new();

    private sealed record SlideItem(int Number, double AtMs, BitmapSource Image);

    internal static void ShowFor(FrameworkElement ownerView, string folder)
    {
        if (ownerView is not Grid shell || shell.Children.OfType<SlideReviewWindow>().Any()) return;
        var previousFocus = Keyboard.FocusedElement;
        var content = shell.Children.Cast<UIElement>().Where(element => Grid.GetRow(element) > 0)
            .Select(element => (Element: element, element.Visibility)).ToArray();
        var page = new SlideReviewWindow(folder, ownerView);
        void Back()
        {
            shell.Children.Remove(page);
            foreach (var item in content) item.Element.Visibility = item.Visibility;
            if (previousFocus is not null) Keyboard.Focus(previousFocus);
        }
        page.GoBack = Back;
        foreach (var item in content) item.Element.Visibility = Visibility.Collapsed;
        Grid.SetRow(page, 1);
        Grid.SetRowSpan(page, 2);
        Grid.SetColumnSpan(page, 4);
        shell.Children.Add(page);
        page.Focus();
    }

    private Action? GoBack;

    private SlideReviewWindow(string folder, FrameworkElement ownerView)
    {
        foreach (var type in new[] { typeof(Button), typeof(TextBlock) })
            if (ownerView.TryFindResource(type) is Style style) Resources[type] = style;
        sessionFolder = folder;
        excluded = SlideSelection.Load(folder);
        Focusable = true;
        SetResourceReference(BackgroundProperty, "AppBackground");
        SetResourceReference(ForegroundProperty, "AppForeground");
        SetResourceReference(FontFamilyProperty, "AppFont");

        var root = new DockPanel { Margin = new Thickness(24) };
        var back = ActionButton("M14,6 L8,12 L14,18 M8,12 H21", "Back to lesson", () => GoBack?.Invoke());
        back.Name = "BackToLesson";
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Margin = new Thickness(0, 0, 0, 18);
        DockPanel.SetDock(back, Dock.Top);
        root.Children.Add(back);
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var copy = new StackPanel();
        copy.Children.Add(new TextBlock { Text = "Review recorded slides", FontSize = 26, FontWeight = FontWeights.Bold });
        copy.Children.Add(count);
        header.Children.Add(copy);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        undo = ActionButton("M9,14 L5,10 L9,6 M5,10 H16 C21,10 21,18 16,18 H15", "Undo  Ctrl+Z", Undo);
        restore = ActionButton("M4,9 V4 M4,9 H9 M4,9 C6,4 12,2 17,5 C22,8 22,16 17,19 C13,22 7,20 5,16", "Restore all", RestoreAll);
        actions.Children.Add(undo);
        actions.Children.Add(restore);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var scroll = new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        Content = root;
        PreviewKeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape) { GoBack?.Invoke(); eventArgs.Handled = true; return; }
            if (eventArgs.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Undo();
                eventArgs.Handled = true;
            }
        };
        RenderCards();
        Loaded += async (_, _) =>
        {
            count.Text = "Loading recorded slides…";
            try
            {
                // Disk reads and image decoding must never block the WPF dispatcher.
                slides = await Task.Run(() => LoadSlides(folder, loading.Token));
                if (!loading.IsCancellationRequested) RenderCards();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { count.Text = "Could not load slides: " + error.Message; }
        };
        Unloaded += (_, _) => loading.Cancel();
    }

    private Button ActionButton(string geometry, string label, Action action)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = VectorIcon(geometry, 16);
        icon.Margin = new Thickness(0, 0, 7, 0);
        panel.Children.Add(icon);
        panel.Children.Add(new TextBlock { Text = label });
        var button = new Button { Content = panel, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 9, 12, 9) };
        button.Click += (_, _) => action();
        return button;
    }

    private void RenderCards()
    {
        cards.Children.Clear();
        foreach (var slide in slides.Where(slide => !excluded.Contains(slide.Number))) cards.Children.Add(Card(slide));
        var visible = slides.Length - excluded.Count(number => slides.Any(slide => slide.Number == number));
        count.Text = $"{visible} of {slides.Length} slides will appear in the PDF. Hover a slide to remove it.";
        count.SetResourceReference(TextBlock.ForegroundProperty, "AppMuted");
        count.Margin = new Thickness(0, 7, 0, 0);
        undo.IsEnabled = undoStack.Count > 0;
        restore.IsEnabled = excluded.Count > 0;
    }

    private Border Card(SlideItem slide)
    {
        var image = new Image { Source = slide.Image, Stretch = Stretch.Uniform, Height = 146 };
        var trashIcon = VectorIcon("M4,7 H20 M10,11 V17 M14,11 V17 M5,7 L6,19 C6,20 7,21 8,21 H16 C17,21 18,20 18,19 L19,7 M9,7 V4 C9,3 10,3 11,3 H13 C14,3 15,3 15,4 V7", 17, null);
        ((ShapePath)((Canvas)((Viewbox)trashIcon).Child).Children[0]).Stroke = Brushes.White;
        var trash = new Button
        {
            Content = trashIcon,
            ToolTip = "Remove from this lesson",
            Padding = new Thickness(9),
            Margin = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Hidden,
            Background = new SolidColorBrush(Color.FromRgb(132, 38, 42)),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent
        };
        var imageLayer = new Grid();
        imageLayer.Children.Add(image);
        imageLayer.Children.Add(trash);
        var body = new StackPanel();
        body.Children.Add(imageLayer);
        var label = new TextBlock
        {
            Text = $"Slide {slide.Number + 1}  ·  {TimeSpan.FromMilliseconds(slide.AtMs).ToString(@"hh\:mm\:ss")}",
            Margin = new Thickness(12, 10, 12, 11),
            FontWeight = FontWeights.SemiBold
        };
        body.Children.Add(label);
        var card = new Border
        {
            Child = body,
            Width = 270,
            Margin = new Thickness(0, 0, 16, 16),
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        card.SetResourceReference(BackgroundProperty, "PanelBackground");
        card.SetResourceReference(BorderBrushProperty, "AppBorder");
        card.MouseEnter += (_, _) =>
        {
            card.Background = new SolidColorBrush(Color.FromArgb(44, 214, 67, 72));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(167, 57, 62));
            trash.Visibility = Visibility.Visible;
        };
        card.MouseLeave += (_, _) =>
        {
            card.SetResourceReference(BackgroundProperty, "PanelBackground");
            card.SetResourceReference(BorderBrushProperty, "AppBorder");
            trash.Visibility = Visibility.Hidden;
        };
        trash.Click += (_, _) => Remove(slide.Number);
        return card;
    }

    private void Remove(int number)
    {
        if (!excluded.Add(number)) return;
        undoStack.Push(number);
        SlideSelection.Save(sessionFolder, excluded);
        RenderCards();
    }

    private void Undo()
    {
        if (undoStack.Count == 0) return;
        excluded.Remove(undoStack.Pop());
        SlideSelection.Save(sessionFolder, excluded);
        RenderCards();
    }

    private void RestoreAll()
    {
        excluded.Clear();
        undoStack.Clear();
        SlideSelection.Save(sessionFolder, excluded);
        RenderCards();
    }

    private static async Task<SlideItem[]> LoadSlides(string folder, CancellationToken cancel)
    {
        var slideFolder = Path.Combine(folder, "slides");
        if (!Directory.Exists(slideFolder)) return [];
        var result = new List<SlideItem>();
        foreach (var path in Directory.GetFiles(slideFolder, "*.slide.json").Order(StringComparer.Ordinal))
        {
            cancel.ThrowIfCancellationRequested();
            var slide = await BrowserPdf.ReadSlide(path, cancel).ConfigureAwait(false);
            result.Add(new SlideItem(slide.Number, slide.AtMs, Thumbnail(Convert.FromBase64String(slide.Image))));
        }
        return result.ToArray();
    }

    private static BitmapSource Thumbnail(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 520;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Viewbox VectorIcon(string data, double size, string? brushResource = "AppForeground")
    {
        var path = new ShapePath
        {
            Data = Geometry.Parse(data),
            Fill = Brushes.Transparent,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        if (brushResource is not null) path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushResource);
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }
}
