using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LectureLens.Desktop;

internal sealed class InAppDialog
{
    readonly Grid shell;
    readonly Grid overlay;
    readonly UIElement[] disabled;
    readonly IInputElement? previousFocus;
    bool closed;
    internal Action? Dismiss { get; set; }
    internal InAppDialog(FrameworkElement view, UIElement content, double width = 680)
    {
        shell = (Grid)view;
        previousFocus = Keyboard.FocusedElement;
        disabled = shell.Children.Cast<UIElement>().Where(x => Grid.GetRow(x) > 0 && x.IsEnabled).ToArray();
        foreach (var item in disabled) item.IsEnabled = false;
        overlay = new Grid { Name = "DialogOverlay", Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), Focusable = true };
        var card = new Border { MaxWidth = width, Margin = new Thickness(24), Padding = new Thickness(24), CornerRadius = new CornerRadius(18), VerticalAlignment = VerticalAlignment.Center, Child = content };
        card.SetResourceReference(Border.BackgroundProperty, "PanelBackground");
        card.Loaded += (_, _) => ControlMotion.AttachTree(card);
        overlay.Children.Add(card);
        Grid.SetRow(overlay, 1); Grid.SetRowSpan(overlay, 2); Grid.SetColumnSpan(overlay, 4); Panel.SetZIndex(overlay, 100);
        KeyboardNavigation.SetTabNavigation(overlay, KeyboardNavigationMode.Cycle);
        overlay.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss?.Invoke(); e.Handled = true; } };
        shell.Children.Add(overlay); overlay.Focus();
        SpatialMotion.Reveal(card, true);
    }
    internal void Close()
    {
        if (closed) return;
        closed = true;
        foreach (FrameworkElement child in overlay.Children) SpatialMotion.Reset(child);
        shell.Children.Remove(overlay);
        foreach (var item in disabled) item.IsEnabled = true;
        if (previousFocus != null) Keyboard.Focus(previousFocus);
    }
}
