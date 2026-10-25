using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LectureLens.Desktop;

internal static class ControlMotion
{
    static readonly ConditionalWeakTable<FrameworkElement, object> attached = new();
    internal static void ReduceMotion()
    {
        SpatialMotion.ResetAll();
        foreach (var entry in attached) if (entry.Key is ScrollViewer scroll) SmoothScroll.Cancel(scroll);
    }
    internal static void AttachTree(DependencyObject root)
    {
        if (root is FrameworkElement view) Attach(view);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) AttachTree(VisualTreeHelper.GetChild(root, i));
    }
    static void Attach(FrameworkElement view)
    {
        if (view is not Button && view is not DataGrid && view is not DataGridRow && view is not CheckBox && view is not ComboBox && view is not ScrollViewer) return;
        if (attached.TryGetValue(view, out _)) return;
        attached.Add(view, new object());
        switch (view)
        {
            case DataGrid grid:
                grid.LoadingRow += (_, e) => Attach(e.Row);
                break;
            case Button button when button.Name is not ("CloseWindow" or "MaximizeWindow" or "MinimizeWindow" or "Stop" or "CancelGeneration"):
                button.PreviewMouseLeftButtonDown += (_, _) => { if (button.IsEnabled && Motion.Enabled) SpatialMotion.To(button, .96, duration: 80); };
                button.PreviewMouseLeftButtonUp += (_, _) => SpatialMotion.To(button);
                button.PreviewKeyDown += (_, e) => { if (e.Key == Key.Space && Motion.Enabled) SpatialMotion.To(button, .96, duration: 80); };
                button.PreviewKeyUp += (_, e) => { if (e.Key == Key.Space) SpatialMotion.To(button); };
                button.LostMouseCapture += (_, _) => SpatialMotion.To(button);
                button.LostKeyboardFocus += (_, _) => SpatialMotion.To(button);
                button.IsEnabledChanged += (_, _) => SpatialMotion.Reset(button);
                break;
            case DataGridRow row:
                row.Loaded += (_, _) => AttachTree(row);
                row.MouseEnter += (_, _) => { if (Motion.Enabled) SpatialMotion.To(row, x: 3); };
                row.MouseLeave += (_, _) => SpatialMotion.To(row);
                row.Selected += (_, _) => SpatialMotion.Reveal(row);
                break;
            case CheckBox check:
                check.Click += (_, _) => {
                    if (check.Template?.FindName("CheckSurface", check) is FrameworkElement box) SpatialMotion.Reveal(box, true);
                };
                break;
            case ComboBox combo:
                combo.DropDownOpened += (_, _) => {
                    if (combo.Template?.FindName("PART_Popup", combo) is Popup popup && popup.Child is FrameworkElement child) SpatialMotion.Reveal(child);
                };
                break;
            case ScrollViewer scroll:
                SmoothScroll.Attach(scroll);
                break;
        }
    }
}
