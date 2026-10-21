# LectureLens — desktop feasibility lab

An installable Windows desktop app for browser-based classes. The current default is **native C#/WPF without WebView**, with an experimental Chrome extension for selected-tab picture/audio and durable local storage. Offline checks pass; real Chrome/BigBlueButton isolation still needs the owner's manual test. Zoom/Teams come later. This build does not produce notes or PDFs. The earlier Electron window-capture spike remains available for historical checks but fails the tab-isolation requirement.

## Run

Development requires Node.js 24 and the .NET 10 SDK on Windows. The launcher also finds the local SDK in `.tools/dotnet`. No service account, key or credit card.

```powershell
npm ci
npm run dev
```

`dev` prepares the Chrome extension and native helper, registers the helper for the current user, opens WPF, and reloads edits to `src/native/Desktop/MainWindow.xaml`. Load `dist/extension` once using Chrome's Load unpacked button. Restart for C# edits and reload the extension after extension edits. See [browser setup and manual tests](docs/browser-tab-test.md). No release build is needed for daily testing. The existing portable EXE is the older interface-only preview and is not refreshed by `dev`.

The following commands and recording behavior refer to the **legacy Electron spike**, not the native preview:

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
