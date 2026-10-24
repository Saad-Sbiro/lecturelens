using System.Windows;
using System.Windows.Controls;

namespace LectureLens.Desktop;

internal static class WorkspaceMotion
{
    internal static void Attach(FrameworkElement view)
    {
        void Arrive()
        {
            ControlMotion.AttachTree(view);
            if (view is not Grid root) return;
            foreach (FrameworkElement panel in root.Children)
                if (Grid.GetRow(panel) == 1 && panel.Visibility == Visibility.Visible && panel.Name is not ("DialogOverlay" or "NotificationToast")) SpatialMotion.Reveal(panel);
        }
        if (view.IsLoaded) Arrive(); else view.Loaded += (_, _) => Arrive();
        if (view.FindName("StudyPreview") is FrameworkElement preview)
        {
            preview.MouseEnter += (_, _) => { if (Motion.Enabled) SpatialMotion.To(preview, 1.025, y: -3); };
            preview.MouseLeave += (_, _) => SpatialMotion.To(preview);
        }
    }
}
