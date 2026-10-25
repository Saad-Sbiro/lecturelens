# Lumi companion

Original native vector artwork; no downloaded assets, services or dependencies. Replaces the paper preview in the upper-right study studio. All text lives in `config/native-ui.en.json`.

## Offline verification

Run `.tools/dotnet/dotnet.exe run --project tests/TermsChecks/TermsChecks.csproj` (or use an installed .NET 10 SDK). Companion checks cover 11 distinct gestures, full-window gaze mapping, layered tracking, completed-move gating, 70% button-only reaction sampling and static card press, artwork bounds in five gaze directions, rest/wake, reduced motion, actual dispatcher animation, minimize, hidden state and unload/reload. A two-theme pose gallery and native animation frames are written to `artifacts/companion/`.

## Owner check (no recording needed)

1. Reopen with `npm run dev`. Move the pointer across the entire workspace, including the distant left side: gaze should keep changing. Eyes lead, followed by the head, body, ears and tail. Check again after resizing the window.
2. Verify that the caption contains only Lumi, the forehead shows the app logo, the feet join the body and no tooltip appears. The card must stay still on press. Click Lumi, or focus it with Tab and press Enter/Space: wave, double hop, peek, wiggle, high-five, wink, stretch and star greeting cycle in order. The next cycle changes gesture directions. Click rapidly: the current move finishes; extra reactions are ignored and never queued. Click again after it finishes to start the next variant.
3. Enabled buttons have a 70% chance of starting a gesture while Lumi is idle. Some clicks should get no reaction. Background clicks, typing and selecting lessons/languages must not trigger gestures. A click during a move must still perform its app action without restarting or queueing a gesture.
4. Leave it alone for 25 seconds: sleepy eyes. Move again: it wakes. Minimize or switch away: animation pauses; returning resumes it.
5. Turn Windows animation effects off: Lumi remains still. Check both app themes and 125–200% display scaling. Its focus border and the studio's scrolling controls should remain usable.

The character expresses attention and affection, not success, recording activity or processing progress. It observes only events inside this window and never reads typed text. Capture, storage and model work do not depend on it.

## Rename button regression check

Run `.tools/dotnet/dotnet.exe run --project tests/DesktopChecks/DesktopChecks.csproj -c Release`. Uses the production XAML, SessionPanel and storage with an isolated synthetic lesson. Checks the real Rename button, blank-name rejection, persisted metadata, retained selection/recording ID, name-only companion caption and absent tooltip. No capture or model calls. Manually select a lesson, Rename it and verify both its library row and workspace heading update.
