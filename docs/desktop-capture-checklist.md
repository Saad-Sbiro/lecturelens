# Desktop lessons — owner-run acceptance

Status: live capture NOT RUN. The adapter is experimental. Automated tests use synthetic media and temporary storage; shell smoke tests do not start recording.

Run `npm run dev`, then **Record a desktop app**. For distribution use `npm run package:native` and keep the whole output folder together. The capture helper runs independently of WPF UI reloads. Browser lessons should continue using the Chrome extension for tab/audio isolation.

1. Open permitted sample content in Zoom, Microsoft Teams or Discord. Select the window containing the shared slides, not a separate chat or meeting-controls window.
2. Read the consent notice. This adapter records ALL sound from the Windows output device, including notifications and unrelated apps. Silence other apps. Your microphone is not captured. No cloud image analysis is used.
3. Preview: verify the picture updates and the sound meter reacts to the speaker. Start must stay disabled without recent sound. Drag a slide rectangle, or explicitly leave the full window selected. Audio-only mode skips slides.
4. Record at least 2 minutes, change a slide, Pause, change it again while paused, Resume, then Stop and save. In the main app select the recording, recover/export, review slides, transcribe and generate notes using your configured adapters. Check that paused content is absent and image/audio alignment is reasonable.
5. Repeat while the capture helper and main app are minimized, while another window covers the lesson, and on another monitor. Keep the SOURCE window restored for normal use; minimize/restore it separately to identify frozen/black frames. Protected video or minimized windows may not update.
6. Close the selected source window. Check graceful saving. If the app replaces its shared-content window or switches pop-outs, stop and select the new window; selection must never silently jump elsewhere.
7. Test main-app **Stop recording**, output-device changes, long silence and low-storage warnings using non-sensitive material. Do not deliberately fill your real disk. Verify the warning and recovered audio before trusting a full lesson.
8. With a synthetic lesson, end the helper process after several saved packets. Reopen LectureLens and recover the interrupted recording. Verify saved audio and slides remain available.

| App/version | Audio + slides | Pause/resume | Covered / other monitor | Source closes | Helper minimized | 15-minute run |
|---|---|---|---|---|---|---|
| Zoom | Pending | Pending | Pending | Pending | Pending | Pending |
| Microsoft Teams | Pending | Pending | Pending | Pending | Pending | Pending |
| Discord | Pending | Pending | Pending | Pending | Pending | Pending |

Record Windows build, audio output device, app version and any error. No per-app audio isolation, microphone mixing, DRM bypass or reliable minimized-source capture is claimed. Speaker changes are normal; source changes require a fresh preview. The helper does not auto-transcribe or upload media; use the existing notes flow after saving.
