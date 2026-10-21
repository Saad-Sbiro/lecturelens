# Phase 0 plan and evidence

Automatic-region update: passed 16 domain/replay tests plus seven extension/sampler tests, extension typecheck and full dev/WPF smoke. Covers region hysteresis, manual lock, visual triage and selected-tab-only layout lookup. No live capture was run; semantic lesson relevance and crop accuracy across real layouts remain unverified.

## S2 initial sparse-image slice

Current development mode stores continuous audio plus unique cropped images sampled every five seconds. Automatic geometric crop proposal is confirmed/corrected in the extension before recording. Image envelopes survive host interruption and regenerate a local gallery. Passed 13 domain/replay tests, six extension/sampler tests, two actual C# host tests, typechecks and WPF dev smoke. The owner's supplied screenshot produced the inner PowerPoint slide without chat/toolbars; no real capture was run. Live sparse cadence/minimized audio, varied crop layouts and labelled S2 accuracy remain unverified. Full S2 is not complete; see `docs/visual-spike-plan.md`.

## Current browser-only direction (2026-10-21)

Owner's first extension test exposed an offscreen API error (`connectNative` unavailable). Corrected the architecture so only the service worker opens the native port, with an offscreen-to-worker relay. Regression tests now reflect the restricted API surface; live Preview retry remains pending.

The owner chose C#/WPF without WebView and requires isolation of one Chrome tab's picture and sound. `npm run dev` now prepares the MV3 extension plus C# Native Messaging helper, registers it for the current user, and opens WPF. The extension starts with consent/preview/audio gating; recording persists bounded packets and exports WebM after stopping. WPF offers verification, stop requests, recovery and deletion. No release installer was rebuilt for this slice.

Passed: native UI launch/XAML reload, two actual-helper durability/protocol tests, three mocked extension/capture-orchestration tests, extension typecheck, and the existing 11 offline tests. No live capture was run. Chrome/BBB isolation, actual encoded playback, navigation-stop timing and minimized cadence remain owner acceptance checks in [browser-tab-test.md](browser-tab-test.md). The earlier Electron/window and embedded-view plan below is historical, not the current default. S2–S6 are still pending.

## Earlier spike plan

1. Compare Electron, Tauri and .NET MAUI for the owner's Windows machine.
2. S0: prove a desktop window, UI live reload and an unsigned installer.
3. S0: provide explicit window selection, preview and a real-signal audio meter.
4. S1: persist incremental media; replay and force-kill synthetic writes to test recovery.
5. S1: owner runs the browser, minimization, audio and one-hour resource checklist.
6. S1: test embedded BigBlueButton compatibility manually, with an isolated session.
7. S2–S4: evaluate visual/audio/provider candidates on permitted fixtures.
8. S5–S6: verify PDF layout and ask the owner to rate sample notes.
9. Record pass/fail/pending separately; each completed code slice ends in tests and a commit.

## Owner context (2026-10-21)

- Windows 10 Pro 22H2, build 19045, x64; Lenovo model 20TA0023MB, 16 GB RAM, Intel Iris Xe integrated graphics (local CIM inspection).
- Owner confirmed Google Chrome + BigBlueButton; Edge is the proposed second browser.
- Owner confirmed teacher permission for sample recordings. No live capture is run by the agent.
- Owner selected on-device processing only. No external model service or key is configured.
- Keep LectureLens as the provisional name; on-device execution requires no keys.

## Status

S0 and S1 are in progress. Documentation support is not a measured pass. S2–S6 are pending; synthetic persistence bytes cannot establish speech or slide quality.

S0 first slice: `npm run typecheck`, `npm test` (2 tests), and `npm run smoke:dev` passed on this machine. The smoke opened the actual desktop shell, checked the sandbox bridge, changed CSS and observed its live update. It did not enumerate windows or capture media.

## S0/S1 measured results — 2026-10-21

| Check | Result | Boundary |
|---|---|---|
| Strict type checking | Pass | Compile-time contracts, not real browser behavior |
| Offline suite | 11 tests passed | Replay, queue backpressure, cancellation, corruption/gaps, path validation, process termination |
| Desktop UI + sandbox | Pass | Actual Electron window, renderer has no Node `require`, narrow preload bridge |
| UI live reload | Pass | CSS edit observed in running desktop window |
| No-consent / no-preview gates | Pass | IPC rejects capture selection without consent and recording creation without preview |
| Synthetic encoding | Pass | 320×180 canvas + oscillator, 4 fragments, pause/resume, decoded picture, exact concatenation after disk reconstruction |
| Small process-kill replay | Pass | 3,600 × 256-byte packets, two-hour synthetic timestamps |
| Full-size process-kill spike | Pass | 3,600 × 399,000-byte packets = 1,436,400,000 bytes; every acknowledged byte recovered; about 34 seconds elapsed |
| Unsigned NSIS build | Pass for final capture spike; 112,160,935-byte installer, 374,152,142-byte unpacked payload | Authenticode reports `NotSigned`; installer not installed by agent |
| Real browser preview/sound | Pending owner | No window listing or live media capture was run by the agent |
| Hidden/minimized capture cadence | Pending owner | Disabling background throttling is configuration, not measured proof |
| One-hour CPU/memory under capture | Pending owner | Resource logger ready; synthetic I/O does not substitute |
| Embedded BBB sign-in + audio isolation | Pending owner | Isolated listen-only experiment ready, no platform session opened |

The UI smoke initially failed to obtain its own compositor screenshot. Explicitly showing/restoring the test window made that smoke pass. This is not evidence about capturing a minimized browser window. The screenshot was visually inspected; no class content was involved.

**S0 decision remains provisional, and S1 remains incomplete.** End-to-end class capture, real-crash media decodability, frame/audio cadence, actual installation, and 3–5 permitted class fixtures are the remaining owner-run gates in `manual-checklist.md`. S2–S6 have not been claimed or fabricated.
