# Browser-tab capture spike — owner test

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
