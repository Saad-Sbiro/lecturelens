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
        StudyCompanion.Attach(view);
    }
}
