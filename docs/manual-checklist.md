# S0/S1 — owner-run capture checks

**Status: NOT RUN.** The agent does not run live captures. Use the packaged app for long tests: UI/shell edits during development can interrupt capture. Scope: Windows 10 Pro build 19045, Chrome + BigBlueButton; repeat with Edge. Record browser versions and the BigBlueButton server version when visible; do not publish private class URLs or credentials.

## Quick first check

1. Run `npm run dev`, or open the unsigned installer in `release/` and launch LectureLens.
2. Put the class in its own browser window. Play a permitted sample with speech. Use headphones and silence other apps.
3. Read/check consent, click **List windows**, choose the class, then **Preview with sound check**. Confirm the live picture changes and the sound meter responds to speech.
4. Confirm Start remains disabled during initial silence (or after 5 seconds without a new signal). When class audio plays, click **Start recording**; verify saved chunks increase and the visible status/title changes.
5. After one minute, click **End & save**, then **Recover saved chunks** and **Open recordings folder**. Each UUID folder contains `recording.webm`. Open it in a video player and check both picture and intelligible audio.

Do not mark S0 capture passed until this works. If there is no audio signal, stop: record the output device, selected source and exact error. This Windows route uses system loopback; the microphone is never requested. Switching audio devices during capture is not yet supported or tested.

## Browser matrix

Use at least 15 minutes of real permitted speech per browser. For the minimization test use moving slides/a visible timer so frozen pixels are recognizable. Write down the recording's elapsed start/end times for every case.

| Case | Chrome + BBB | Edge + BBB | Evidence / failure |
|---|---|---|---|
| Class visible; ≥15 minutes of complete, intelligible speech | Pending | Pending | |
| Class covered by another window | Pending | Pending | |
| Class minimized, then restored | Pending | Pending | |
| Switch class-window tabs, then return; title warning visible | Pending | Pending | |
| Close class window; saved chunks remain recoverable | Pending | Pending | |
| Minimize LectureLens for ≥10 minutes; continuous picture + sound | Pending | Pending | |
| Pause/resume by button, then Ctrl+Shift+Space; paused content absent | Pending | Pending | |
| Move class to another monitor (if available) | Pending | Pending | |
| Play another app's sound; document whether it appears | Pending | Pending | |
| Denied permission / missing or silent audio blocks Start | Pending | Pending | |

Do not infer ≥90% frame/audio cadence from the chunk counter. `events.ndjson` records application diagnostics and `tools/session-report` reports container delivery only. Encoded frame/sample counts and intelligibility still need media inspection; S1 remains pending until they are measured. Expected video is 10 fps (requested, actual source settings can differ). Exclude intentional pause intervals and explain any gap. Whole-system audio mixing is the selected baseline; isolated per-application loopback is **not implemented**.

To summarize local diagnostics: `npm run session:report -- "<absolute session directory>"`. This reads only the selected local folder and does not upload it.

## One-hour resource and recovery checks

- [ ] Record for one hour. Note responsiveness, CPU and memory at 0, 15, 30, 45 and 60 minutes. The app saves per-process CPU percentage and working-set KB every 5 seconds in `events.ndjson`. A steadily rising footprint is a failure to investigate; do not use a synthetic write test as capture-load evidence.
- [ ] During a permitted sample, kill LectureLens in Task Manager. Reopen, recover the interrupted session and verify its saved media. Compare final saved time with the time of the kill. The last unflushed encoder/IPC buffer can be missing; record that loss, do not claim zero.
- [ ] Check recording chunks stop growing during pause (a final pre-pause flush is expected), and the pause/resume events delimit the missing content.
- [ ] Close the app normally while recording. It first ends/saves; close it again after completion.
- [ ] Verify low free space blocks Start; do not deliberately fill the owner's disk to test this.
- [ ] Check the local deletion action removes only the selected session; keep permitted fixtures elsewhere before deleting.

The automated `npm run test:durability` writes 3,600 synthetic 399,000-byte chunks (1.4364 GB; two hours at the configured 1.596 Mbps), kills the writer, reconstructs all acknowledged bytes and checks them. It runs faster than real time and does **not** prove two hours of capture, decoder continuity after a real crash, power-loss durability, or speech joins.

## Option B: embedded BigBlueButton experiment

1. Expand **Alternative: embedded class window**. Enter the actual HTTPS class URL yourself and open it. No URL is preconfigured or logged by LectureLens.
2. Try sign-in, joining as a listen-only participant, slides and sound. The isolated window has no Node/preload access; microphone, camera, pop-ups and screen-sharing requests are denied in this spike. A blocked sign-in pop-up counts as a compatibility limitation of this prototype, not proof that all embedded designs fail.
3. In LectureLens, list windows and choose the special first entry for the embedded class. Consent, preview, check sound, then save a short permitted sample.
4. Play audio in an unrelated app. Verify whether only the embedded class is saved and class playback remains audible. Close the embedded window and verify recording ends safely.
5. Record pass/fail and exact failure. Do not weaken security or spoof a browser to bypass platform restrictions. Window capture remains the provisional fallback.

## Fixtures and remaining spikes

- [ ] Save 3–5 permitted samples under ignored `fixtures/private/`, including mixed-language speech, silence, slide transitions, webcam overlay and revisited slides.
- [ ] Keep fixture consent/source notes locally. Do not commit class voices, teacher names, meeting links or authentication data.
- [ ] S2 visual scoring, S3 transcription/chunk joins, S4 local model benchmarks, S5 PDF mixed-direction layout and S6 owner notes ratings remain pending.
- [ ] Run the unsigned installer on the owner's account; launch it and uninstall after verifying retained recordings. Installer creation alone is not an installation test.

## Voice-only lesson (2026-10-24)
- Reload LectureLens in Chrome's extensions page after building. Open an HTTPS lesson tab you have permission to record.
- Choose **Voice only — This lesson has no slides**. Accept Terms of Service once if prompted. Verify the source notice describes audio only.
- Check that you have recording permission, then choose **Check lesson audio**, play speech and check the sound meter. Start without selecting a slide area. Confirm lesson sound remains audible.
- Reopen the extension: voice-only remains selected and locked. Switch to another browser tab and confirm the original lesson remains the source. Pause/resume, then **End & save**; wait for **Saved locally**.
- In the desktop app, check the saved audio and generate notes/PDF without slides. Confirm there are no screenshot files and visual events contain `voice-only-lesson-slides-disabled`.
- Repeat a short permitted recording with **Slides + voice**: preview, confirm crop, start, pause/resume and save. Check slide capture still works.
- Enable the OS reduced-motion preference: decorative bars and hover movement should stop. Real tab behavior and final voice-only PDF must be verified manually; automated tests use mocks only.

## One-time Terms of Service (2026-10-24)
- Close the old desktop app, then run `npm run dev` and reload the Chrome extension. On a fresh profile, the desktop must show Terms of Service with an unchecked agreement and disabled Accept button. Not now/Escape closes the app without saving agreement.
- Read and accept in either the desktop or extension. Reopen both: the agreement should stay hidden. Use desktop Settings > Terms of Service or the extension footer to read the same document without accepting again.
- Before acceptance, extension preview is unavailable. If local acceptance cannot be saved, an error remains visible and capture stays unavailable.
- Slides + voice: cloud auto-crop starts unchecked. Leave it off and select an area manually; there must be no Gemini request. To use automatic crop, explicitly enable the image-upload option before preview. After ending a session, it resets to off. Voice only never uploads images.
- Do not delete a real user's acceptance to test onboarding. Automated checks use isolated temporary data: `.tools/dotnet/dotnet.exe run --project tests/TermsChecks/TermsChecks.csproj`, `npm run test:native`, and the mocked extension UI check documented in decisions.

## Native motion polish (2026-10-24)
- Restart the desktop after rebuilding. Confirm the existing layout, colors and fonts are unchanged in both themes.
- Move over buttons and click rapidly: hover fades briefly, while press and actions respond immediately. Use Tab/Space too; focus remains visible.
- Switch between saved lessons, open Settings/Terms, and dismiss immediately: entry should soften without delaying interaction. Repeated progress notifications should remain steady.
- Turn off Windows animation effects and repeat: transitions should be immediate. Capture reliability is covered by offline checks; do not record a real class just to test animation.

## Visible motion pass
- In the updated desktop, hover/press buttons, select another lesson, open Settings, and open a language dropdown. Expect visible lift/compression, slide/scale entry and immediate input response.
- Scroll a long transcript and the export panel quickly, then use the scrollbar or keyboard. Motion must stop at the current position before direct interaction; virtualized lesson lists retain normal scrolling.
- Check processing indicators only while a real job is running. Stop/cancel controls remain stationary and respond immediately.
- Disable Windows animations while a button is hovered: geometry should reset. Reopen dialogs and repeat scrolling; motion should stay disabled.
