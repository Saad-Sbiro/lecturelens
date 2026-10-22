# Decisions

## 2026-10-21 — Automatic region tracking and conservative visual triage

Add automatic/manual crop modes. Automatic mode combines geometric borders with fallback viewport rectangles from video/canvas/SVG elements in the selected tab. Two consistent observations are required before relocating a region; weak or changing geometry preserves full-frame review candidates rather than clipping content silently. Manual mode is never overridden. Source-size changes are logged instead of stopping audio.

`scripting` permission enables a small activeTab-scoped geometry query. It returns normalized rectangles only, no text/URLs/chat, and is invoked only for the selected tab through an offscreen-origin-checked worker message. No broad host permissions, persistent content scripts, new libraries or network services. Lookup is time-bounded so unavailable DOM access cannot stall finalization.

Content triage rejects near-uniform frames, keeps text/diagram-like structure and marks ambiguous content for review. This is not OCR, topic matching or semantic lesson recognition: a structured unrelated page may pass. No model-based claims are made. Uncertain layouts and brief content remain review candidates. Kept/rejected decisions and region changes are logged. Synthetic tests cover manual lock, hysteresis, blank/structured/ambiguous frames and selected-tab-only geometry lookup; real layouts still need owner validation.

## 2026-10-21 — Sparse images plus continuous audio

Replace archived tab video with audio-only Opus/WebM and five-second image candidates. The selected video source is requested at up to 1 fps/1920×1080 for preview/analysis, independent of continuous audio. No new dependencies or paid services. A pure geometric edge detector proposes an inner rectangular crop; explicit confirmation/manual dragging gates recording. Crop comparison uses 160×90 gray frames and 5×5 blocks; tiny cursor noise is ignored. All-kept matching avoids duplicate images on revisits. Stable samples are kept once; brief changes are retained for review, not silently discarded. Images are capped at 720 and 500 kB each; only one pending full image is retained in memory.

C# flushes an atomic image+metadata+SHA-256 envelope before acknowledging; recover/export regenerates JPEGs and a local HTML gallery. Audio uses the existing packet recovery path. Tests cover sparse timing, duplicates/returns, crop bounds, image export after host kill and compatibility with old video sessions. The owner's screenshot independently yielded the inner slide; artefacts remain local/ignored. Live sparse capture, audio continuity under this new mode, varied layouts and labelled detection accuracy still require validation. See `docs/visual-spike-plan.md` for deferred work; geometric confidence is not calibrated semantic confidence.

## 2026-10-21 — Keep tab acquisition constraints in one syntax

Owner Preview testing reached getUserMedia but Chrome rejected the mixed constraint syntax: the audio request combined legacy `mandatory` tab selectors with standard audio fields. Acquisition now uses only legacy tab constraints, matching the video request. The offscreen mock now rejects mixed syntax instead of accepting it. This fixes request construction; live Preview and audio-isolation checks remain pending.

## 2026-10-21 — Native Messaging must be owned by the service worker

Owner testing of Preview returned `chrome.runtime.connectNative is not a function`: the offscreen document cannot open the native port. The original mock incorrectly supplied that method. Move the native connection into the service worker; offscreen uses `runtime.connect` to an origin-checked relay. Requests and durable ACKs pass through unchanged. Either-side disconnect closes both ports; never reconnect silently to an ongoing recording. Tests now omit `connectNative` in the offscreen context and exercise relay forwarding, sender rejection and disconnect/error propagation. Live Chrome retry remains pending. This corrects the earlier interpretation of the runtime APIs exposed offscreen; the [Native Messaging guide](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging) documents service-worker support.

## 2026-10-21 — S1 selected Chrome tab and C# storage

The user requires a fixed browser tab's picture and audio, with desktop meeting apps deferred. An MV3 Chrome extension requests `tabCapture.getMediaStreamId({targetTabId})` after an explicit popup action/consent; an offscreen document owns the stream and MediaRecorder. It preserves audible tab playback through an AudioContext. No content scripts, broad host permissions, window/screen API, microphone request or system loopback fallback are used. `webNavigation` stops on selected main-frame navigation; this is asynchronous and is not a zero-frame guarantee. The owner must avoid navigating the selected tab to private content.

Native Messaging sends bounded 256 KiB payloads to a C# helper. Each packet is acknowledged after flush/rename, with sequence, monotonic time, digest and reason. The existing bounded MediaRecorder adapter supplies backpressure. Stop drains queued packets and automatically reconstructs a WebM; abrupt host loss leaves committed packets recoverable in WPF. Preview does not persist media. The desktop verifies sessions only on explicit refresh, keeping full-recording scans off the UI thread and out of polling loops.

New dependency: `@types/chrome` 0.3.6 supplies compile-time API contracts only; no runtime service, quota, key or payment. Existing Vite/TypeScript build extension assets. The checked-in public key fixes the development extension ID; no private key is retained. Registration uses one current-user Chrome native-messaging key with one allowlisted extension origin, also checked by the helper. Daily development builds only the small helper and extension, not a release app/installer.

Evidence: actual C# protocol tests cover consent, ordering, active locks, force-kill recovery, corruption, oversized packets and final export. Mocked extension tests exercise fixed target selection, consent, navigation routing, preview-without-persistence, packet splitting, pause/resume and drain-before-finish. These do not establish live Chrome capture correctness; follow `docs/browser-tab-test.md`.

Official references checked: [tabCapture](https://developer.chrome.com/docs/extensions/reference/api/tabCapture), [offscreen](https://developer.chrome.com/docs/extensions/reference/api/offscreen), [Native Messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging). Chrome 116+ is required for service-worker stream IDs consumed offscreen. App/minimized behavior and speech continuity remain manual acceptance gates.

## 2026-10-21 — Native WPF preview replaces the default development entry point

The owner chose C#/.NET with WPF/XAML and no WebView for the Windows interface. `npm run dev` now builds and opens that shell; the old Electron launcher remains only for legacy spike checks. The Vue/WebView2 draft was removed before release. The .NET 10 SDK is free, installed locally under ignored `.tools/dotnet`; WPF comes with the Windows Desktop framework, with no additional UI package or hosted service. See [official .NET free tooling](https://dotnet.microsoft.com/en-us/platform/free).

Loose, local XAML is watched and reloaded in development; invalid edits preserve the last valid view. C# changes require restarting. Release builds load local XAML without watching. UI messages live in a locale resource file. The initial locale is English, matching the existing UI default, independent of teacher/output language.

The native preview deliberately disables recording: browser tab isolation is not implemented yet. The existing Electron installer records windows, and the owner's tab-switch test demonstrated why that is unsuitable. Next S1 uses a Chrome extension for selected-tab picture/audio; Zoom/Teams are deferred. No real capture was performed for this slice.

Verification: WPF build and actual-window smoke passed, including an observed XAML reload and rendered screenshot. `package:native` produces a portable Windows x64 folder with an app-relative .NET runtime copied from the SDK, not an installer. The published EXE also passed startup with development/runtime environment variables removed. Keep its files together. See `docs/native-preview.md` for exact commands and limitations.

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

## 2026-10-22 — Manual crop and shared desktop/extension styling
Owner deferred automatic region detection. Default to unconfirmed manual crop and expose a maximized extension editor with a frozen, aspect-preserving image up to 1920 pixels wide. Only explicit Save changes the crop; the editor cannot start capture or retarget the tab. Keep capture in the existing offscreen document. No new dependencies or cloud services. WPF and extension share dark slate/mint styling and clearer recording controls.

## 2026-10-22 — Original logo and wider popup
Use the supplied source assets in src/assets unchanged. Embed logomark.png as a WPF resource and let Vite bundle the same file for the extension and crop editor. Gold/amber accents and warm charcoal surfaces replace the mint/slate palette. Set the Chrome popup to 600px with a two-column preview card so recording controls remain reachable. No new dependencies.

## 2026-10-22 — Local audio feasibility probe
Start with CPU whisper.cpp v1.8.3 + multilingual Small Q5 and Silero v6.2.0; compare Base Q5. These optional MIT-licensed downloaded tools/models are pinned and SHA-256 checked under ignored .tools/speech. No npm dependency added. The existing development Electron runtime is used only for offline replay decoding, not shipped with WPF. This isolates model feasibility from a future native decoder. See speech-spike.md for measured results and limitations. Owner permits considering NVIDIA trial APIs for personal summarization experiments later; local speech remains the chosen path and no media was uploaded.

## 2026-10-22 — Optional personal cloud notes and native study studio
Owner authorized NVIDIA API use for personal PDF experiments. The key is stored outside the repository with Windows current-user DPAPI; no credentials ship with the app. Only an explicitly attached transcript is uploaded after per-export consent. Audio and images remain local. No new paid or npm dependency.

Default adapter: nvidia/nemotron-3.5-lightning-30b-a3b, with thinking disabled. A synthetic French provider feasibility probe completed in 1.46 seconds (70 output tokens); this is not a full-lesson latency or quality benchmark. Several catalog-listed alternatives returned unavailable responses. Model ID remains editable. Official model information: https://build.nvidia.com/nvidia/nemotron-3.5-lightning-30b-a3b/modelcard . Trial capacity is variable, not a production SLA; https://forums.developer.nvidia.com/t/nvidia-nim-faq/300317 .

The native WPF studio supports brief/balanced/detailed notes, independent output language, optional local image appendix, encrypted key settings, cached completed chunks, cancellation and source-reference validation. Import a .txt or speech-probe transcript.json first: full recording transcription is not yet integrated into this UI. References establish traceability, not factual correctness. Images are not semantically understood or aligned to paragraphs.

PDF adapter prints escaped, offline HTML through installed Edge/Chrome headless, keeping the desktop UI native. This reuses an installed browser instead of adding a heavyweight PDF dependency. A4 layout, source appendix and image integrity validation are included. Exports live separately from recordings; deleting raw media requires an explicit post-export choice. Preview limits: 250,000 transcript characters and 100 images; larger image sets require exporting without images.

Chrome setup is one button opening chrome://extensions and copying the unpacked extension path. Automatic arbitrary unpacked installation is unsupported on normal Windows Chrome. Store publication is required for supported external installation; user enablement still applies. No policy bypass or fake installed state: https://developer.chrome.com/docs/extensions/how-to/distribute/install-extensions .

## 2026-10-22 — UI direction lab before native integration

**Decision:** complete the existing Orbit, Paper and Prism recording/result mockups as an offline comparison. The owner explicitly requested coding now and must choose a direction before production screens and tokens are locked. Phase 0 plans and the capture implementation are unchanged. `docs/design-lab/README.md` describes each direction's typography, color roles, geometry, depth, illustration and proposed motion.

**Options:** code all three directly in WPF, generate static illustrations, or use isolated HTML fixtures. Choose HTML fixtures for this selection slice: they are inspectable, exercise Arabic/French layout and share offline screenshot checks without linking experimental visuals to recording. This does not change the WPF desktop architecture. Native resources, state gallery, measured performance and real-data motion follow the choice.

**Tooling:** building/opening the preview uses Node and an installed browser only. Automated rendering reuses Codex's existing bundled Playwright and installed Edge; no npm package or production dependency was added. The renderer blocks HTTP requests; the standalone viewer also disallows connections and media through CSP. All times, signals, slides and PDF pages are labelled synthetic fixtures. Export controls are demonstrative and cannot create a real PDF or delete recordings.

**Font licenses verified from official sources:** IBM Plex Sans Arabic ([IBM OFL](https://github.com/IBM/plex/blob/master/LICENSE.txt)), Noto Naskh Arabic ([Noto OFL](https://github.com/notofonts/arabic/blob/main/OFL.txt)) and Alexandria ([Google Fonts OFL](https://raw.githubusercontent.com/google/fonts/main/ofl/alexandria/OFL.txt)) permit free use and embedding under SIL OFL 1.1. Local license files stay beside the existing unmodified font assets and are included in generated preview CSS comments. The lab uses original CSS geometry and synthetic teaching diagrams, with no stock artwork or sounds. It does not replace the supplied production logo.

**Evidence and limits:** `npm run design:check` checks 132 layout cases, 48 text/surface contrast pairs, keyboard pause/resume with retained focus, named controls and reduced motion, and produces 12 individual screenshots plus 3 comparison boards. Browser renderings at device scales 1.25 and 1.5 are inspection aids, not proof of native Windows DPI behavior. Native capture performance, OS frame behavior and full accessibility remain pending. The detailed run report is `docs/design-lab/verification.json`; user approval is still pending.
