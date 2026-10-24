using System.IO;
using System.Windows;
using System.Windows.Controls;
using LectureLens.Storage;

namespace LectureLens.Desktop;

internal static class TermsNotice
{
    internal static void Attach(FrameworkElement view, bool smoke)
    {
        if (smoke) return;
        if (!new TermsAgreement(LocalStore.DefaultRoot).IsAccepted()) Show(view, true);
    }

    internal static void Show(FrameworkElement view, bool required)
    {
        var terms = TermsAgreement.Document;
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = terms.Title, FontSize = 26, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = terms.Intro, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 16) });
        var sections = new StackPanel();
        foreach (var section in terms.Sections)
        {
            sections.Children.Add(new TextBlock { Text = section.Title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 5) });
            sections.Children.Add(new TextBlock { Text = section.Body, TextWrapping = TextWrapping.Wrap });
        }
        panel.Children.Add(new ScrollViewer { Content = sections, MaxHeight = 230, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var check = new CheckBox { Content = terms.Agreement, Margin = new Thickness(0, 18, 0, 0), Visibility = required ? Visibility.Visible : Visibility.Collapsed };
        check.SetResourceReference(Control.ForegroundProperty, "AppForeground");
        panel.Children.Add(check);
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        feedback.SetResourceReference(TextBlock.ForegroundProperty, "Danger"); panel.Children.Add(feedback);
        var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) }; panel.Children.Add(actions);
        var accept = new Button { Content = terms.Accept, IsEnabled = false, Style = (Style)view.FindResource("Primary"), Visibility = required ? Visibility.Visible : Visibility.Collapsed };
        var close = new Button { Content = required ? terms.Decline : terms.Close };
        actions.Children.Add(accept); actions.Children.Add(close);
        var dialog = new InAppDialog(view, panel, 720);
        void Dismiss() { if (required) Window.GetWindow(view)?.Close(); else dialog.Close(); }
        dialog.Dismiss = Dismiss; close.Click += (_, _) => Dismiss();
        check.Checked += (_, _) => accept.IsEnabled = true;
        check.Unchecked += (_, _) => accept.IsEnabled = false;
        accept.Click += (_, _) =>
        {
            if (check.IsChecked != true) return;
            try { new TermsAgreement(LocalStore.DefaultRoot).Accept(terms.Version); dialog.Close(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { feedback.Text = terms.SaveError; }
        };
        if (required) check.Focus(); else close.Focus();
    }
}
