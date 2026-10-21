# Phase 0 plan and evidence

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
