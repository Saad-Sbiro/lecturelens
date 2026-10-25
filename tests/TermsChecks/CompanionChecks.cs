using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LectureLens.Desktop;

internal static class CompanionChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Finish(CompanionPose pose)
    {
        for (var i = 0; i < 60 && pose.Move != CompanionMove.None; i++) pose.Step(.05, true);
    }
    static void Advance()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    internal static void Run()
    {
        var pose = new CompanionPose();
        pose.Look(-100, 100);
        for (var i = 0; i < 100; i++) pose.Step(.033, true);
        Check(pose.LookX is >= -1 and < -.99 && pose.LookY is <= 1 and > .99, "Gaze must follow and clamp to the cursor direction.");
        Check(CompanionPose.TrackAxis(200, 1700, 1920) < CompanionPose.TrackAxis(900, 1700, 1920), "Far-left workspace movement must not saturate gaze.");
        Check(CompanionPose.TrackAxis(1700, 1700, 1920) == 0 && CompanionPose.TrackAxis(1920, 1700, 1920) == 1, "Tracking must use the actual card position and window edges.");
        var response = new CompanionPose(); response.Look(1, -1); response.Step(.033, true);
        Check(response.LookX > response.HeadX && response.HeadX > response.BodyX && response.BodyX > 0, "Eyes must lead the head, then the body.");
        var moves = new HashSet<CompanionMove>();
        var silhouettes = new HashSet<CompanionGesture>();
        for (var i = 0; i < 8; i++)
        {
            Finish(pose); pose.React(personal: true); moves.Add(pose.Move);
            silhouettes.Add(CompanionGesture.Sample(pose.Move, .38, 1));
        }
        Check(moves.Count == 8 && silhouettes.Count == 8, "Eight clicks after completed moves must select eight distinct choreographies.");
        for (var i = 0; i < 3; i++) { Finish(pose); pose.React(quiet: true); moves.Add(pose.Move); }
        Check(moves.Count == 11, "Quiet controls must have three distinct appropriate reactions.");
        foreach (var move in moves)
        {
            Check(CompanionGesture.Sample(move, 0, 1) == default, "Every move must begin at rest.");
            var ending = CompanionGesture.Sample(move, 1, 1);
            Check(Math.Abs(ending.Y) < .001 && Math.Abs(ending.PawLeft) < .001 && Math.Abs(ending.HeadRoll) < .001, "Every move must settle at rest.");
        }
        Finish(pose);
        pose.React(); for (var i = 0; i < 20; i++) pose.Step(.033, true);
        var before = pose.Gesture; var chosen = pose.Move; var progress = pose.Progress;
        var personal = pose.Personal; var variation = pose.Variation;
        for (var i = 0; i < 200; i++) pose.React(personal: true, quiet: i % 2 == 0);
        Check(pose.Gesture == before && pose.Move == chosen && pose.Progress == progress && pose.Personal == personal && pose.Variation == variation,
            "Additional reactions during a move must neither replace nor restart it.");
        Finish(pose);
        Check(pose.Move == CompanionMove.None && pose.Gesture == default, "Ignored clicks must not queue a later reaction.");
        pose.React();
        Check(pose.Move == CompanionMove.Hop, "The first fresh click after completion must start the next move without skipping variants.");
        Finish(pose);
        for (var i = 0; i < 900; i++) pose.Step(.033, true);
        Check(pose.Rest > .99, "An idle companion should doze.");
        pose.Look(1, 0);
        for (var i = 0; i < 30; i++) pose.Step(.033, true);
        Check(pose.Rest < .01, "Activity must wake the companion.");
        pose.React(true); pose.Step(.033, false);
        Check(pose.LookX == 0 && pose.HeadX == 0 && pose.BodyX == 0 && pose.Gesture == default && pose.Time == 0, "Reduced motion must immediately reset moving parts.");

        var root = new StackPanel();
        NameScope.SetNameScope(root, new NameScope());
        var art = new ContentControl { Height = 176 };
        var button = new Button { Name = "CompanionButton", Content = art, Width = 240 };
        var action = new Button { Name = "GeneratePdf" };
        var lessons = new ListBox { Name = "Sessions" };
        lessons.Items.Add("fixture-a"); lessons.Items.Add("fixture-b");
        root.Children.Add(button); root.Children.Add(action); root.Children.Add(lessons);
        root.RegisterName("CompanionButton", button); root.RegisterName("CompanionArt", art);
        double chance = 0;
        var companion = StudyCompanion.Attach(root, () => chance)!;
        Check(ReferenceEquals(companion, StudyCompanion.Attach(root)), "Attaching twice must not duplicate event subscriptions.");
        var host = new Window { Content = root, Width = 280, Height = 320, Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
        host.Show(); Advance();
        try
        {
            host.Activate(); Advance();
            Check(host.IsActive, "The synthetic host must activate to exercise real timer ticks.");
            if (Motion.Enabled) Check(companion.Running && companion.Pose.Time > 0, "An active visible companion must animate on real dispatcher ticks.");
            host.WindowState = WindowState.Minimized; Advance();
            Check(!companion.Running, "A minimized companion must stop rendering.");
            host.WindowState = WindowState.Normal; host.Activate(); Advance();
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            Check(companion.Pose.Personal && companion.Pose.Move != CompanionMove.None, "Companion activation must produce a personal greeting (mouse or keyboard Click).");
            var previous = companion.Pose.Move;
            action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, action));
            Check(companion.Pose.Move == previous && companion.Pose.Personal, "A real routed click must not interrupt the active greeting.");
            Finish(companion.Pose);
            action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, action));
            Check(companion.Pose.Move != previous && !companion.Pose.Personal, "Workspace action must produce a different response.");
            Finish(companion.Pose);
            lessons.SelectedIndex = 1;
            lessons.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            root.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            root.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host), 0, Key.A) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(companion.Pose.Move == CompanionMove.None, "Selections, background clicks and typing must not trigger moves.");
            var accepted = 0;
            for (var i = 0; i < 10; i++)
            {
                chance = i / 10.0;
                var click = new RoutedEventArgs(Button.ClickEvent, action);
                action.RaiseEvent(click);
                if (companion.Pose.Move != CompanionMove.None) accepted++;
                Check(!click.Handled, "Companion reactions must not consume button actions.");
                Finish(companion.Pose);
            }
            Check(accepted == 7, "The 70% threshold must accept seven of ten evenly spaced probability samples.");
            chance = 0;
            ControlMotion.AttachTree(root);
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            Advance();
            Check(button.RenderTransform.Value.IsIdentity, "Lumi's card must not compress or move on press.");
            host.Content = null; Advance();
            companion.Pose.Step(0, false);
            action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, action));
            Check(!companion.Running && companion.Pose.Move == CompanionMove.None, "Unloading must stop the timer and detach root handlers.");
            host.Content = root; Advance();
            action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, action));
            Check(companion.Pose.Move != CompanionMove.None, "Reloading must reattach interactions.");
            button.Visibility = Visibility.Collapsed;
            Check(!companion.Running, "Hidden companions must stop rendering.");
        }
        finally { host.Close(); }
        CheckArtworkBounds();
        RenderGallery();
        Console.WriteLine("PASS: 11 gestures, full-workspace tracking, layered following, finish-before-next reaction, 70% button-only reactions, static card press, artwork bounds, rest/wake, reduced motion, timer lifecycle and two-theme gallery.");
    }

    static void CheckArtworkBounds()
    {
        foreach (var direction in new[] { new Point(-1, -1), new Point(-1, 1), new Point(1, -1), new Point(1, 1), new Point(0, 0) })
        for (var move = 0; move < 11; move++)
        {
            var drawing = new CompanionDrawing { Width = 240, Height = 176 };
            drawing.Measure(new Size(240, 176)); drawing.Arrange(new Rect(0, 0, 240, 176));
            drawing.Pose.Look(direction.X, direction.Y);
            if (move < 8) for (var n = 0; n <= move; n++) { Finish(drawing.Pose); drawing.Pose.React(true); }
            else for (var n = 8; n <= move; n++) { Finish(drawing.Pose); drawing.Pose.React(quiet: true); }
            for (var frame = 0; frame < 60; frame++)
            {
                drawing.Pose.Step(.033, true);
                if (frame % 6 != 0) continue;
                drawing.InvalidateVisual(); drawing.UpdateLayout();
                var bounds = VisualTreeHelper.GetDrawing(drawing).Bounds;
                Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= 240 && bounds.Bottom <= 176,
                    $"Artwork must fit its card: move {move}, direction {direction}, frame {frame}, bounds {bounds}.");
            }
        }
    }

    static void RenderGallery()
    {
        var gallery = new Grid { Width = 960, Height = 1320 };
        for (var i = 0; i < 4; i++) gallery.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 6; i++) gallery.RowDefinitions.Add(new RowDefinition());
        for (var i = 0; i < 24; i++)
        {
            var drawing = new CompanionDrawing { Height = 190 };
            var state = i % 12;
            if (state < 8) for (var n = 0; n <= state; n++) { Finish(drawing.Pose); drawing.Pose.React(personal: true); }
            else if (state < 11) for (var n = 8; n <= state; n++) { Finish(drawing.Pose); drawing.Pose.React(quiet: true); }
            if (state == 11) { drawing.Pose.Look(-.85, .65); for (var n = 0; n < 50; n++) drawing.Pose.Step(.033, true); }
            else for (var n = 0; n < (state is 0 or 6 ? 21 : state >= 8 ? 13 : 17); n++) drawing.Pose.Step(.033, true);
            var panel = new StackPanel(); panel.Children.Add(drawing);
            panel.Children.Add(new TextBlock { Text = "Lumi · " + (state == 11 ? "following" : drawing.Pose.Move.ToString()), HorizontalAlignment = HorizontalAlignment.Center, Foreground = i < 12 ? Brushes.Wheat : Brushes.SaddleBrown });
            var background = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(i < 12 ? "#202127" : "#F8F4ED")), Child = panel };
            Grid.SetColumn(background, i % 4); Grid.SetRow(background, i / 4); gallery.Children.Add(background);
        }
        gallery.Measure(new Size(960, 1320)); gallery.Arrange(new Rect(0, 0, 960, 1320)); gallery.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1920, 2640, 192, 192, PixelFormats.Pbgra32); bitmap.Render(gallery);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory("artifacts/companion");
        using var output = File.Create("artifacts/companion/gallery.png"); png.Save(output);
        RenderAnimation();
    }

    static void RenderAnimation()
    {
        var drawing = new CompanionDrawing { Width = 240, Height = 176 };
        var card = new Border { Width = 260, Height = 196, Padding = new Thickness(10), Background = new SolidColorBrush(Color.FromRgb(32, 33, 39)), Child = drawing };
        card.Measure(new Size(260, 196)); card.Arrange(new Rect(0, 0, 260, 196)); card.UpdateLayout();
        Directory.CreateDirectory("artifacts/companion/frames");
        for (var i = 0; i < 400; i++)
        {
            if (i == 10) drawing.Pose.Look(-1, .5);
            if (i == 30) drawing.Pose.Look(1, -.4);
            if (i >= 50 && (i - 50) % 50 == 0) { drawing.Pose.Look(0, 0); drawing.Pose.React(true); }
            drawing.Pose.Step(.04, true); drawing.InvalidateVisual(); card.UpdateLayout();
            var bitmap = new RenderTargetBitmap(520, 392, 192, 192, PixelFormats.Pbgra32); bitmap.Render(card);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create($"artifacts/companion/frames/{i:D3}.png"); encoder.Save(file);
        }
    }
}
