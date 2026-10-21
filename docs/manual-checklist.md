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
