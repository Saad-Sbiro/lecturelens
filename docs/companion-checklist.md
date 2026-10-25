# Lumi companion

Original native vector artwork; no downloaded assets, services or dependencies. Replaces the paper preview in the upper-right study studio. All text lives in `config/native-ui.en.json`.

## Offline verification

Run `.tools/dotnet/dotnet.exe run --project tests/TermsChecks/TermsChecks.csproj` (or use an installed .NET 10 SDK). Companion checks cover 11 distinct gestures, full-window gaze mapping, layered tracking, interrupted-pose continuity, repeated selections and event deduplication, artwork bounds in five gaze directions, rest/wake, reduced motion, actual dispatcher animation, minimize, hidden state and unload/reload. A two-theme pose gallery and native animation frames are written to `artifacts/companion/`.

## Owner check (no recording needed)

1. Reopen with `npm run dev`. Move the pointer across the entire workspace, including the distant left side: gaze should keep changing. Eyes lead, followed by the head, body, ears and tail. Check again after resizing the window.
2. Click Lumi, or focus it with Tab and press Enter/Space: wave, double hop, peek, wiggle, high-five, wink, stretch and star greeting cycle in order. The next cycle changes gesture directions. Click rapidly: poses blend without snapping or queueing.
3. Click workspace buttons or empty space: a new playful move. Select a lesson (including the already selected lesson), change a language or use stop/delete/cancel: nod, ear perk and head tilt cycle. A single selection click should cause only one response. Typing gets a gentle reaction that can finish while you continue typing.
4. Leave it alone for 25 seconds: sleepy eyes. Move again: it wakes. Minimize or switch away: animation pauses; returning resumes it.
5. Turn Windows animation effects off: Lumi remains still. Check both app themes and 125–200% display scaling. Its focus border and the studio's scrolling controls should remain usable.

The character expresses attention and affection, not success, recording activity or processing progress. It observes only events inside this window and never reads typed text. Capture, storage and model work do not depend on it.
