# LectureLens — desktop feasibility lab

An installable Windows desktop app for browser-based classes. Phase 0 currently implements a consent-gated preview, sound check, local recording, pause/resume and recovery tooling. **It does not yet produce study notes or PDFs.** Chrome + BigBlueButton is the owner's first target; real capture validation is pending.

## Run

Requires Node.js 24 and npm on Windows. No service account, key or credit card.

```powershell
npm ci
npm run dev
```

`dev` opens the desktop app, uses Vite for UI live reload, and restarts the shell after shell/storage edits. Keep development edits separate from real recordings. The shipped app uses bundled local assets and needs no localhost server.

```powershell
npm run typecheck
npm test
npm run smoke
npm run smoke:dev
npm run replay
npm run test:durability
npm run package
```

- `smoke` checks the actual desktop shell without capturing a window or system audio.
- `smoke:dev` additionally checks live CSS reload, consent gates, synthetic canvas + oscillator encoding, pause/resume, decoding and exact disk reconstruction. It does not contact a class platform.
- `test:durability` temporarily uses about 3 GB for a two-hour **synthetic** write/kill/recovery workload; it removes its own temporary data.
- `replay` uses a tiny deterministic storage fixture; `eval` reports which golden-set metrics are not ready. Neither is a speech/visual benchmark yet.
- `package` produces a per-user, unsigned NSIS installer in `release/`. No store/signing account is needed. Manual installation remains on the checklist.

Recordings live in the app's local user-data `recordings/` folder. Use **Open recordings folder** to find the exact location. After End or a crash, **Recover saved chunks** concatenates the verified container fragments into `recording.webm`. A timeslice is not an independently playable audio file. Existing chunks are preserved if export finds a gap/corruption. No media is uploaded. There is no PDF-triggered deletion yet because there is no PDF stage; delete raw sessions explicitly in this spike.

See [Phase 0 status](docs/phase-0.md), [decisions](docs/decisions.md), [provider inventory](docs/providers.md) and the [owner's manual checklist](docs/manual-checklist.md).
