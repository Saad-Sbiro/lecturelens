# Browser-tab capture spike — owner test

## Current sparse-slide mode

Stop any existing recording, rerun `npm run dev`, and Reload the extension. **Automatic region tracking** is the default: Preview then Start. It checks geometry every sample and requires two matching proposals before relocating the crop. If uncertain it retains full-frame images for review. **Manual crop (locked)** lets you drag a rectangle and Confirm slide crop before Start; automatic detection never overrides it. Manual selection uses normalized coordinates, so re-preview after layout changes if necessary. Automatic tracking continues through frame-size changes without stopping audio.

New checks: record with automatic mode, change the presentation layout and wait at least 10–15 seconds; check that the region moves and uncertain candidates are labelled review. Repeat in manual mode and verify the rectangle stays fixed. Blank screens should not create slides. A structured unrelated page may still be kept: this is visual triage, not semantic lesson recognition. Check that geometry lookup continues to target the lesson when another tab is active. Reloading adds Chrome's scripting capability under the existing activeTab scope; only element rectangles are returned.

New sessions save **audio.webm** (continuous sound, no video) and a **slides/** folder. Candidate images are sampled every 5 seconds from a source requested at up to 1 fps; only distinct crops are persisted. After End & save, choose Refresh, select the session and **View slides** in WPF, or open `slides/index.html`. JPEGs are accompanied by atomic `.slide.json` envelopes and `visual-events.ndjson` logs. Recover / export rebuilds images/gallery and audio from committed data. Old video sessions remain recoverable.

Test at least three slides held for 10+ seconds each, a return to the first slide, chat activity and cursor movement. Check readability and duplicates. Repeat with minimized Chrome and verify actual sampled timestamps; the prior successful video test does not establish timer-based image sampling reliability. Five-second sampling can miss very short slides. Borderless/ambiguous layouts may need manual crop. No transcription or PDF exists yet. The older video-specific steps below describe the previous mode only.

The agent ran offline/mocked tests, not real Chrome capture. Treat tab isolation, real audio, background cadence and BigBlueButton compatibility as unverified until this checklist passes. Use synthetic or consented lesson content, never private tabs for the first test.

## Start development

1. Stop any existing capture, then stop the old development command with Ctrl+C.
2. Run `npm run dev`. This builds the extension and small C# native-messaging helper, registers only `HKCU\Software\Google\Chrome\NativeMessagingHosts\local.lecturelens.capture`, and opens WPF. It does not create a release installer or start recording.
3. In Chrome, open `chrome://extensions`, turn on **Developer mode**, choose **Load unpacked**, and select the repository's `dist/extension` folder. The desktop's **Open extension folder** button opens the correct directory.
4. Expected extension ID: `ejamnckgmmhdfeoildogjgaihppiajah`. Pin **LectureLens Tab Capture** in Chrome for a visible recording badge. Do not enable it in incognito.
5. After later source edits, stop recording, rerun `npm run dev`, and click **Reload** on the extension card. XAML reloads automatically; extension code requires Chrome Reload. Do not rebuild/reload during a recording.

Chrome 116+ is required. Other browsers have not been registered/tested. New developer machines need the .NET 10 Windows SDK; the local `.tools/dotnet` SDK is used on this machine. No API key, cloud service or extension-store payment is involved.

## Record a short sample

1. Open your HTTPS BigBlueButton tab and play consented class audio.
2. Click the extension, check the consent box, then **Preview selected tab**. Preview requests only that tab's video/audio; it does not save media.
3. Verify the thumbnail shows the lesson and the audio meter moves. You should still hear the lesson. Click **Start recording**. Starting is blocked without recent audio or video frames.
4. The popup can close while recording continues. The extension badge says REC or PAUSE; open it again to pause/resume or **End & save**. Closing the WPF window does not stop Chrome capture.
5. After End & save completes, open the desktop recordings folder. The new session folder contains `recording.webm`, verified packet files and timeline events. Play `recording.webm` and check picture/audio. Desktop **Refresh** verifies packets and shows saved sessions; **Recover / export** reconstructs a selected interrupted session.

The raw packets and exported recording remain on this device until explicitly deleted. PDF generation and automatic post-PDF cleanup do not exist yet. Recording reserves space for both packets and an export (roughly 3 GB for the initial two-hour estimate).

## Acceptance checks

- Record tab A displaying a visible label and playing voice/tone A. Switch to a harmless tab B with a different label and sound B. The saved recording must show A only and contain A's sound only. Audio isolation must be checked by listening to the file, not merely looking at the meter.
- Move between tabs repeatedly and minimize the WPF app for ten minutes. Check continuity, timestamps, picture updates and speech. Browser throttling is still unverified.
- Pause, wait, resume, then stop. The saved timeline must show the gap and the file must play.
- Closing the selected tab should stop gracefully. Navigating inside that same tab also requests a stop; event-delivery races are not a zero-frame privacy guarantee. Do not navigate the selected tab to private content. Other-tab navigation must not stop or retarget it.
- Stop from WPF: select the recording after Refresh, choose Request stop, keep Chrome open for final writes, then Refresh again.
- With a synthetic sample only, terminate `LectureLens.NativeHost` in Task Manager after several packets. The extension should report failure; desktop Refresh should show an interrupted session. Recover it and confirm previously acknowledged content survives. Encoder buffers/unacknowledged bytes can be lost.
- Delete a completed synthetic session through the desktop and confirm the consent dialog. Active sessions must not be deletable.

If the extension reports a native connection error: rerun `npm run dev`, verify the extension ID and loaded folder, then reload the extension. If media capture fails or the meter remains silent, stop and report the exact message; there is no fallback to whole-screen or system-audio capture.

## Offline checks

`npm run typecheck:extension`, `npm run test:extension`, `npm run test:native`, `npm run smoke:native`.

Native tests require `npm run browser:setup` once after changes to the helper. They launch the actual C# helper against temporary synthetic storage. Extension tests use mocked Chrome/media APIs; they do not prove real browser isolation. The existing `npm test` suite still covers the earlier replay/storage contracts.

## Large manual crop editor (2026-10-22)
- Stop capture, close the old desktop window, run `npm run dev`, then reload the extension.
- Preview a lesson; Start stays disabled until a crop is saved. Choose slide area opens a maximized separate window with a frozen high-resolution frame.
- Drag around the slide, resize the editor, and verify the outline stays aligned. Save, reopen the extension, then start.
- Cancel must leave the previously saved crop unchanged. Refresh image updates the frozen frame. Full frame resets the selection.
- Test a non-16:9 lesson viewport: neither preview nor saved slide should stretch. Verify saved slide edges, isolated tab audio, pause/resume and end/save.
- The editor never selects another tab. If the lesson layout changes, end capture and select the new region before restarting.
