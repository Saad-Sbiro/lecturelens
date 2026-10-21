# Decisions

## 2026-10-21 — S0 provisional shell: Electron + Vite + TypeScript

**Decision:** build the first Windows spike with Electron. Keep the final capture choice gated on the owner-run S1 checklist. The shipped product is an installed desktop app; Vite's localhost server exists only during development.

| Criterion | Electron | Tauri | .NET MAUI |
|---|---|---|---|
| UI development | Vite live reload; local smoke will measure it | Vite dev integration; not run here | XAML Hot Reload; not run here |
| Windows capture + sound | Documented window selection and system loopback | Native capture/audio integration needed; not implemented | Native Windows capture/audio integration needed; not implemented |
| UI responsiveness | Separate renderer; asynchronous disk I/O; capture load pending | Native work can be isolated; not measured | Native work can be isolated; not measured |
| Install size | Bundles Chromium; measure our installer | Uses OS webview; our build size not measured | Deployment-dependent; our build size not measured |
| Free tooling/build | MIT shell, Vite and unsigned NSIS; no account | MIT/Apache-2.0, unsigned installers | Free SDK; unsigned packaging path requires its own spike |
| Future mobile domain | Pure typed ports/domain reusable, replace shell | JS domain reusable; native mobile targets | C# domain reusable; mobile targets |
| Local setup | Node 24 already available | Rust toolchain absent | dotnet command exists; MAUI workload not verified |

**Consequences:** larger desktop bundle in exchange for a short capture/dev spike. Loopback records all system output, not only the class. Windows/browser background behavior and embedded sign-in remain unverified. No framework imports in domain or ports.

**Measured build:** final Windows x64 NSIS installer is 112,160,935 bytes (~107 MiB); unpacked app payload is 374,152,142 bytes (~357 MiB). Authenticode status is `NotSigned`. Build succeeded locally without signing/store credentials. Installed-disk overhead and clean-account installation remain unmeasured.

**Dependencies and reasons:** Electron 45.0.0 supplies desktop windows/capture; Vite 8.3.5 supplies UI live reload and local assets; TypeScript 5.9.3 provides strict compile-time contracts; @types/node 24 types the local filesystem/process adapters; electron-builder 26.15.3 creates a per-user unsigned NSIS installer. All are development dependencies; there is no hosted runtime service, credit card, account or metered quota. Versions are pinned by package-lock.json. Tests use the built-in Node test runner.

Official sources checked 2026-10-21:
- [Electron capture](https://www.electronjs.org/docs/latest/api/desktop-capturer), [display-media request contract](https://www.electronjs.org/docs/latest/api/session#sessetdisplaymediarequesthandlerhandler-opts), [background throttling](https://www.electronjs.org/docs/latest/api/browser-window).
- [Electron MIT license](https://github.com/electron/electron/blob/main/LICENSE), [Vite dev loop](https://vite.dev/guide/), [electron-builder license](https://github.com/electron-userland/electron-builder/blob/master/LICENSE), [NSIS configuration](https://www.electron.build/nsis/).
- [Tauri overview](https://tauri.app/start/), [Windows prerequisites](https://tauri.app/start/prerequisites/).
- [.NET free tooling](https://dotnet.microsoft.com/en-us/platform/free), [MAUI hot reload](https://learn.microsoft.com/en-us/dotnet/maui/xaml/hot-reload).

## 2026-10-21 — Local-only spike and durability boundary

Only synthetic fixtures are committed. Real permitted classes belong in ignored `fixtures/private/` or app user data. UI strings and language defaults live in configuration. Preview requires consent, and saving starts only after a real sound signal. No automatic third-party upload is available.

MediaRecorder timeslices are fragments of one container, not independently decodable audio chunks. S1 will preserve their order and reconstruct the container; S3 must prove demux/chunk boundaries before transcription. Durability means a completed, flushed append survives process termination. An encoder buffer or unacknowledged write may still be lost in a crash; do not claim zero loss of unwritten media. Sudden power loss needs a separate test.

## 2026-10-21 — S1 transport/persistence prototype

**Decision:** use a bounded async media queue and immutable, numbered packet files. Each packet contains its sequence, monotonic session time, reason, length and SHA-256 digest. Write to a temporary file, flush, close, then rename before acknowledging. Recover only a continuous verified sequence; preserve original files and explicitly flag gaps, corruption and unfinished writes. Serialize session writes; reject concurrent duplicate sequences and writes to ended sessions.

**Evidence:** offline tests recovered every acknowledged byte after force-killing a separate writer, including 3,600 packets totaling 1.4364 GB. A real MediaRecorder was exercised with generated canvas frames and an oscillator (no microphone/system capture); reconstructed bytes matched and decoded. Actual live browser capture and speech continuity remain unverified.

**Consequences:** S0/S1 `CaptureSource` temporarily exposes synchronized container fragments as `media`; it does not yet expose the brief's independent `frames`/`audio` streams. S3 must add a demux adapter behind the port before downstream analysis is built. `Storage` implements the recording subset of §6.2; general note/document `put`/`get` operations are intentionally not invented yet. No domain code depends on container codecs, Electron, Vite or filesystem APIs.

**Capture configuration:** 1280×720 at a requested 10 fps, 1.5 Mbps video + 96 kbps audio, 2-second encoder timeslices; estimated two-hour payload is 1.4364 GB (actual variable bitrate differs). A 32 MiB queue plus a separately bounded 32 MiB Blob conversion backlog protects memory; a slow disk ends capture with an explicit error. Reserve 256 MiB of free disk beyond the two-hour estimate. Both clocks derive from one session epoch using monotonic `performance` values across renderer/main.

**Limits:** background throttling is disabled, recording uses encoder events rather than UI animation callbacks, and a power-save blocker is active while recording. These are mitigations only. The real 10-minute minimized cadence check remains mandatory. The sound meter is a start gate, not a speech-quality test; any system sound can unlock it, so the owner must verify that the signal belongs to the class. Embedded BBB opens only after an explicit URL action in a separate sandboxed, ephemeral session; no credentials, URLs or class content are committed.
