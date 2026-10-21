# PROJECT BRIEF — LectureLens (working title)

**Audience:** the AI coding agent, and the owner.
**How to use:** read §0–§2 and §11 fully once. After that, open only the section a task needs. Do not paste this whole file into prompts. `AGENTS.md` holds the short always-loaded rules.
**Version:** v1 spec, written 2026-10-19, revised 2026-10-20 after a second verification pass (§13). **Revised 2026-10-21:** the product is an **installable desktop app** (a mobile app comes later), not a hosted web app. Changed: §0–§3, §4.2, §4.9, §6, §7.6, §8, §9, §10, §12.
**Legend:** ✔ = verified against documentation or papers on that date (sources in §13). ⚠ = uncertain or conflicting evidence, so it **must be tested in Phase 0** before anything depends on it.

---

## 0. Summary

LectureLens is an **installable desktop app** (a mobile app comes later). While an online class runs on the user's computer, the app records the class's screen and sound, automatically finds the important slides, transcribes the teacher's speech, and when the user clicks **End** produces a **PDF of study notes**. The notes are written in simple language and include the important slide screenshots, placed where they belong.

- **v1 scope:** a desktop app for the owner's computer, for classes that run **in a web browser** (any meeting platform's web client). Later capture sources: desktop meeting apps (Zoom, Teams and similar), then a mobile app.
- **Not a website:** the product is not a hosted web app. A small public page for downloads and a demo is fine in Phase 3.
- **Languages:** French is the first enabled teacher language. The design supports any language through configuration, and nothing in the code may assume French.
- **Cost model:** everything is **free to build and run at the start**. Every paid-grade capability sits behind a swappable interface. If v1 proves valuable, we **invest by switching a configuration profile** (stronger speech, vision and language models, hosting, code signing), not by rewriting.
- **Technology:** this brief deliberately names no frameworks, vendors or models. The agent chooses in Phase 0, within the rules in §2 and §7. That includes **how the desktop app itself is built** (§8, S0).

---

## 1. Product

### 1.1 Problem
Students in online classes cannot listen, understand and take notes at the same time. Full recordings are long and rarely re-watched. Slides are shown on screen but often not shared afterwards.

### 1.2 What it does
One start, one end, one PDF. While the class runs, the app watches the shared content and the audio. At the end it outputs notes organized by slide: a screenshot, then short simple explanations of what the teacher said about it, followed by announcements, a glossary and optional review questions.

### 1.3 Users
- **Primary:** a student who attends classes in a browser and runs the app on their own computer.
- **Secondary:** classmates who receive the PDF.

### 1.4 Non-goals for v1
Live captions, real-time notes during class, speaker identification, a mobile app (planned after v1), capture of desktop meeting apps (planned, §4.2 and Phase 4), a hosted web version, teacher-facing features, social features, payments, and a full document editor.

---

## 2. Ground rules (already decided)

| # | Rule | Meaning |
|---|---|---|
| R1 | **Desktop-first, class in a browser** | The product is an installable desktop app. v1 records a class that runs in a web browser on the same computer, by capturing the browser window (§4.2). Later: desktop meeting apps, then a mobile app. Core logic must not depend on the desktop shell, so a mobile app can reuse it. |
| R2 | **Free-first** | Build, test and run v1 at zero cost with no credit card. "Free" means on-device or open-source, or a permanent free tier. Expiring trial credits do not count. Every external capability needs a free path. Paid-only dependencies are not allowed in v1. Verify every free limit in the official docs before integrating. Paid-grade options are added later as adapters behind the same interfaces (§7), never built now. Desktop distribution in v1 means unsigned builds for the owner and friends; paid signing and store fees wait for §7.5 (see §7.6). |
| R3 | **Technology-neutral** | The domain logic never names a vendor or model. Everything external sits behind a capability interface (§6.2). The agent picks concrete technologies in Phase 0 and records why in `docs/decisions.md`. |
| R4 | **Scalable by design** | Scale comes from structure (modules, config, stateless stages, queue-ready jobs), not from infrastructure we do not need yet. |
| R5 | **Language-agnostic** | Teacher language, PDF language and UI language are independent settings drawn from a language registry (§5). No hard-coded language outside the registry and test fixtures. |
| R6 | **Privacy and consent** | A consent reminder before recording. Capture is always started by the user and visible (never covert, no hidden bots). Raw media is deleted after the PDF is produced unless the user opts to keep it. Sending class audio or images to a third party is opt-in, and any provider that may use submitted data is flagged in the UI. |
| R7 | **Reliability over features** | A smaller feature set that works every time beats a wide one. Never lose a recording. |
| R8 | **Explainable decisions** | Every screenshot kept or rejected, and every transcript segment kept or dropped, has a recorded reason that can be inspected. |
| R9 | **Fast development loop** | The `dev` command must start the desktop app with live reload for UI changes, like a website dev server. Parts that cannot reload live (capture engine, native code) restart quickly and are covered by the replay harness (§6.8). |

**Owner context:** the owner is a student building this as a portfolio-grade project with an AI coding agent. Talk to the owner in simple Arabic (short sentences). Keep code, comments, docs and commits in English. Keep explanations short, prefer recommended defaults to open questions, and provide a short manual test checklist whenever a feature cannot be tested automatically.

---

## 3. User journey

1. **Open the app.** On first run, show a short intro, the consent notice and a permissions guide (the operating system may ask for screen-recording and sound-capture permission).
2. **Setup (preferences).** See §4.1. A saved "course profile" can prefill everything.
3. **Start.** A picker opens (the app's own or the operating system's). The user chooses the window that shows the class (the browser). The app checks that frames and sound really arrive and shows a live audio level. If sound is missing, it explains how to fix it for this operating system (§4.2).
4. **Calibration.** A live preview appears. The user draws a rectangle around the slide area once (this excludes the browser's own toolbar and tabs, meeting UI, participant tiles and webcams). The rectangle is remembered per course profile. Skipping uses the full frame.
5. **Recording.** A minimal screen shows elapsed time, the number of slides captured, the audio level and any warnings, plus Pause/Resume and **End**. A global shortcut pauses recording instantly, for example when the user opens something private.
6. **End.** A processing screen shows progress per stage. Processing is resumable. If the app closes or crashes mid-way, the session can be recovered and resumed.
7. **Result.** PDF preview and download. Optional "Review and fix" lets the user exclude screenshots, edit titles and regenerate.
8. **History (local).** Past sessions with regenerate and delete.

The default path is zero-interaction after End. Review is optional and happens after the PDF exists.

---

## 4. Functional specification

### 4.1 Preferences
| Setting | Values | Default |
|---|---|---|
| PDF language | any enabled registry language | UI language |
| Teacher language | any enabled registry language, or "auto-detect" (reserved for later) | French |
| Other-language speech | ignore / include, plus an optional ignore list | ignore |
| Detail level | brief / standard / detailed | standard |
| Style | very simple / standard / academic | very simple |
| Screenshots | include yes/no, max per hour | yes, cap in config |
| Extra sections | key takeaways, announcements and homework, glossary, review questions | takeaways, announcements and glossary on |
| Privacy mode | on-device only / allow online services | depends on profile |
| Quality profile | free / standard / premium (§7.1) | free |
| Course profile | save preferences and crop region under a name | none |

### 4.2 Capture
**Source abstraction.** Capture is a `CaptureSource` capability (§6.2). v1 implements one live source chosen in Phase 0 (S1) between **window capture** (default candidate) and an **embedded class view**, plus a "replay from files" source used by tests (and later by "import a recording"). Later sources (desktop meeting apps, which are also just windows, then mobile) must plug in without touching analysis code.

**How the class is captured (options to test in S1)**
- **A. Window capture (default candidate).** The user picks the browser window that shows the class. It also works later for desktop meeting apps, which are windows too. Consequences: the window shows whichever tab is active, so **switching tabs inside that window changes what is recorded**, and a desktop app cannot capture one single tab from outside the browser. Mitigations: guidance to keep the class in its own browser window, instant Pause (global shortcut), rejection of non-lesson frames by the importance cascade (§4.3), and a warning when the window title or layout changes.
- **B. Embedded class view.** The app opens the class page inside its own panel and captures only that panel (tab-like isolation, sound limited to that panel). Risks ⚠: meeting platforms may refuse embedded browsers or block sign-in, terms-of-service questions, and extra work per platform. Fallback or later option.
- **C. Companion browser extension (later).** Captures one chosen tab and hands it to the app. An extra moving part, so Phase 4.

**Sound.** Capturing sound is operating-system specific (§9, desktop facts). Whole-system sound capture also records other sounds (music, notifications) ✔, so: show a clear notice that other apps' sounds may be recorded, offer per-application sound capture where the operating system supports it ⚠, and let the language and speech filters (§4.4) reduce stray audio. Avoid asking the user to install extra drivers where possible.

**Requirements**
- After the user picks the window, verify that frames and sound arrive. If sound is missing: block Start, or allow video-only with a clear warning that no transcript can be produced, and explain how to enable the needed permission for this operating system. Some permission failures produce a **silent sound stream with no error** ✔, so check for a real signal and never assume sound works.
- First-run permission guide for the operating system (screen recording, sound capture). Detect a denied permission, explain how to grant it, and say when an app restart is needed.
- If sound exists but stays silent for a configurable time while the screen changes, warn once.
- If the captured window closes or capture stops, finalize gracefully and continue to processing with what was captured.
- Pause and resume record a gap in the timeline. Nothing is analyzed or stored during a pause. A visible indicator (R6) always shows whether recording is active.
- Keep the computer awake while recording.
- **Crash-safe persistence:** write audio chunks, slide candidates and the timeline to local disk **incrementally**. After a crash, offer "Recover session". Estimate the disk space a class needs (bitrate × duration), show free space, and warn before Start if it is low.
- One monotonic session clock timestamps everything (audio chunks, frames, events).
- **The app's own window may be minimized or hidden** while the user watches the class. Capture and sampling must keep full cadence (§9 ⚠ item 1).
- Heavy work (frame comparison, image encoding) runs off the UI thread. Memory stays bounded: store frames as compressed images, release raw frames immediately.
- The class window may be on another monitor. Capture the chosen window, not a screen position.

### 4.3 Visual pipeline (slide detection and selection)
Process in this order. Numeric values are **starting points** that live in config and are tuned on fixtures.

1. **Sample.** Take reduced-size frames at a modest cadence (start: every 1–2 s). Keep the latest full-resolution frame available for on-demand grabs. Do not assume a constant frame rate: static content may deliver few frames ⚠.
2. **Crop.** Apply the user's normalized crop rectangle before analysis and before saving images.
3. **Detect change.** Compare each frame to the current reference slide using **block-wise** differences, not exact pixel equality. A published example used about 160×90 comparison frames, 8-px blocks, "block changed" above a mean difference near 14/255, and "frame changed" above about 2% changed blocks ✔. Ignore tiny changes (cursor, caret, small toasts, clocks). Learn the noise level during the first minutes. Mask regions that change constantly (webcam bubbles) ✔.
4. **Stability gate.** A change becomes a candidate only after the new content stays stable for a minimum dwell (start about 2–3 s). This avoids fades, transitions and half-rendered frames ✔. Keep the **last stable frame** before the next change, which is the most complete version of a slide that builds up. Delivered video quality may still be settling right after a change (adaptive bitrate needs a few seconds to stabilize ✔), so inside the stable window prefer the **sharpest** frame over the first one ⚠ (S2 compares both).
5. **Deduplicate against all kept slides**, not just the last one ✔. A match means a new **visit** of an existing slide, not a new image. The list of visits (slide id, start, end) is the main data structure, because transcript text is attached per visit.
6. **Incremental builds.** If a new frame equals the previous one plus added content, replace the earlier image with the newer one within the same visit.
7. **Moving content** (video, animation): if no stable window occurs for a long period, keep at most one representative frame per interval and mark it `motion`.
8. **Scrolling and accumulating content** (documents, code, handwriting, whiteboards): sample when scrolling or writing pauses, and keep a snapshot just before a large erase or clear. Never one image per stroke or keystroke. Cap the rate and merge near-duplicates.
9. **Importance cascade** (cheap to expensive; stop as soon as the decision is clear):
   - Rules: dwell time, change size, blank or near-uniform frames, very low information. Starting heuristics: under about 4 s is likely a transition, 8 s or more is likely content.
   - Content signals: amount of text and structure, if text extraction is available at low cost.
   - Model judgement for **ambiguous candidates only**, returning `{isLessonContent, type, title, confidence, reason}`.
   - Policy: **prefer recall**. Borderline candidates are kept as "maybe" up to the cap, and the user can drop them in Review.
   - Typical rejects: desktop, file manager, meeting participant grid, chat panel, home or start pages, system dialogs, login screens, blank or black screens.
10. **Caps.** Maximum screenshots per class hour and a minimum spacing, both in config.
11. **Output** per accepted slide: id, compressed image of the cropped area, visits, title, confidence and the decision log.

If no slides are ever detected (a pure talk), the app still produces notes, organized by topic or time (§4.5).

### 4.4 Audio pipeline
1. **Chunking.** Record continuously as chunks that are **independently decodable** and cut at silences, with a short overlap (about 1 s) that is removed after transcription. Chunk length is chosen from the active provider's limits (§7.2). Recorder slices produced by a web engine are **not** guaranteed to be playable on their own ✔; whatever recorder is used, design chunking for this explicitly.
2. **Voice activity detection** before transcription. Speech models produce invented text on non-speech audio, often a few stock phrases ✔. Filtering non-speech helps most, and post-filtering known phantom phrases and repeated loops helps further, but nothing removes the problem completely ✔.
3. **Transcription** with at least segment-level timestamps. A recognizer works either on finished chunks (**batch**) or on a live audio track that emits text as it is spoken (**streaming**). The port supports both (§6.2). Streaming recognizers run during the class and their text is persisted like everything else. Raw audio is always persisted too, so a different recognizer can re-run later.
4. **Language handling.** The goal is **no stray foreign text in the final notes**, not a perfect transcript. Set the primary language explicitly from preferences. Speech models typically fix the language per window or file, and foreign words inside a sentence may be translated, transliterated or garbled; forcing the language does not fix mixed speech ✔. Foreign speech decoded under a forced language is expected to come out as plausible-looking text in the primary language's own script, so script filtering alone cannot catch it ⚠ (S3 measures this). Therefore implement four layers for the "ignore other languages" policy:
   - **Window language gate (before transcription):** classify short audio windows. The signal may come from the recognizer's own detection, a dedicated language-ID model, or a comparison of recognition confidence between the allowed and the ignored languages. Windows whose best language is outside the allowed set with high confidence are skipped, which also saves quota and avoids invented text. Adapters declare which signals they offer. Windows with uncertain language continue to the next layers.
   - **Segment filter (after transcription):** drop segments that are low-confidence, repetitive loops, known phantom phrases for the language (data lives in the language registry), or whose text-level language ID falls outside the allowed set.
   - **Script filter:** when scripts differ (for example Latin vs Arabic), strip tokens in scripts that do not belong to the allowed languages. It catches foreign-script output only.
   - **Notes instruction:** the summarizer ignores side remarks and stray foreign words and marks unclear audio instead of guessing.
   Every dropped window or segment gets a reason code (R8). Report two error rates: foreign text that leaked into the notes, and primary-language speech that was wrongly dropped (§10).
5. **Technical terms.** Optionally pass terms found on slides as vocabulary hints.
6. **Other speakers** (students asking questions) are part of the audio. v1 does not separate speakers.
7. **Output:** transcript segments `{start, end, text, lang, confidence, kept, reason}`.

### 4.5 Alignment
- Each kept segment attaches to the slide visit active at its midpoint, with a small tolerance.
- Text before the first slide goes to "Introduction".
- Speech while a rejected screen is shown (for example the teacher on the desktop) attaches to the previous accepted slide or to a "Discussion" block by time.
- With no slides at all, group by time windows or detected topic shifts.

### 4.6 Summarization ("simple language")
**Rules for generated notes**
- Write in the PDF language, with short sentences. Define jargon at first use.
- Keep formulas, code, numbers, names, dates and definitions exactly as given.
- Do not invent. Ground everything in the slide or the transcript. If they conflict, prefer the slide for facts and note the teacher's remark. Mark unclear audio instead of guessing.
- Skip small talk, jokes and technical issues ("can you hear me?").
- Highlight what the teacher stresses (for example "this will be in the exam").
- Collect announcements, homework and deadlines into their own section.

**Stages:** per-visit notes (slide content plus aligned transcript), then merge multiple visits of one slide, then the overview and key takeaways, then the glossary, then optional review questions with answers.
**Detail levels:** brief is about 3 bullets per slide, standard is 3–6, detailed is 6–10 with examples.
**Structured output:** notes are produced as validated structured data, not free text, and the renderer builds the PDF from it.
**Long sessions:** process in independent units (map then reduce). Never exceed a model's limits. Persist intermediate results.
**Cost discipline:** send images to a model only when text extraction is not enough. Cache every provider result by content hash. Batch small units.

### 4.7 PDF output
- **Sections:** cover (title, date, duration, language), overview, key takeaways, per-slide pages (screenshot plus notes), announcements and homework, glossary, review questions (optional), page numbers.
- **Technical:** A4 or Letter. Correct direction per language, including mixed left-to-right and right-to-left text in one paragraph. Embedded fonts covering every enabled language. Images never split across pages. Selectable text and an outline or bookmarks. Compressed images, with a target size of about 20 MB or less for a 2-hour class. Right-to-left shaping is a known weak spot of many PDF tools ⚠, so verify it with real mixed text in Phase 0.
- **File name:** date plus course name. Also export Markdown (with an images folder).

### 4.8 Review and fix (Phase 2)
After generation: thumbnails with include/exclude toggles, editable titles, editable note text, and "Regenerate PDF". A debug view (developer mode) shows the decision log on a timeline.

### 4.9 Edge cases
| Case | Expected behavior |
|---|---|
| No sound | Explain how to enable the operating system's sound-capture permission or route. Never record silently. |
| Capture stopped early / class window closed | Finalize and process what exists. |
| Browser crash or refresh | Offer to recover the session. |
| Network loss while processing | Retry with backoff, resume from the last finished unit. |
| Provider quota exhausted | Queue and wait, switch to a fallback provider, or reduce optional work. Tell the user. Never silently drop content. |
| Layout changes (crop now wrong) | Detect a large persistent change and prompt to re-crop. |
| Class with no slides | Time- or topic-based notes. |
| 3-hour session | Bounded memory, chunked processing, same quality. |
| Unsupported OS version or denied permission | Explain the limitation and how to fix it. Do not fail silently. |
| User switches tabs inside the captured browser window | The importance cascade rejects non-lesson frames. Suggest keeping the class in its own window. |
| Class window covered or minimized | Behavior is documented per OS in S1. Warn the user if frames freeze. |
| Very fast slide changes | Rate caps, merge near-duplicates, keep the last stable frame. |

---

## 5. Language design

**Registry.** One entry per language with: code, native and English names, script, text direction, speech-recognition code(s), text-extraction code, PDF font stack, prompt locale notes, `enabled`, `status` (tested or experimental), and which providers support it.

**Three independent roles:** teacher (audio) language, PDF (output) language, UI language. Any combination must work.

**Session language policy:** `primaryLanguage`, `allowedLanguages[]` (default equals primary), `onOtherLanguage: ignore | include`, `ignoreList[]`.

**Adding a language is configuration only:** registry entry, UI strings, font check, prompt locale check, golden-set test, then enable. If adding a language needs pipeline code changes, the design is wrong. In Phase 2, enable a second language purely through config to prove this.

**Auto-detect (later):** analyze the first 30–60 seconds, show the detected language with confidence, and ask the user to confirm. The policy object already supports a `mode: fixed | auto` field so no refactor is needed.

---

## 6. Architecture (principles only)

### 6.1 Layers
1. **UI:** screens, internationalization.
2. **Application:** session controller, pipeline orchestrator, preferences and profiles.
3. **Domain modules:** capture, visual analysis, audio analysis, alignment, summarization, document rendering. They depend only on the interfaces in layer 4.
4. **Capability interfaces ("ports")**, see §6.2.
5. **Adapters:** concrete implementations chosen by configuration.
6. **Desktop shell:** the app window, menus, tray, global shortcuts, permission prompts, packaging and updates. It hosts the UI and the adapters. Domain code never depends on it, so a future mobile app can reuse the domain and the ports.

A modular monolith is the target. No microservices, no orchestration platforms, no accounts, no payments in v1.

### 6.2 Capability interfaces (ports)
Domain code talks only to these. The notation is language-neutral. Every call can be cancelled, has a timeout and returns a typed error: `Unavailable`, `QuotaExceeded` (with retry time if known), `Unsupported` (language or size), `BadInput` or `Transient`. The orchestrator retries `Transient` with backoff and switches to a fallback adapter on the others. It never drops data silently.

| Port | Responsibility | Contract (inputs → outputs) |
|---|---|---|
| `CaptureSource` | Supply video frames and audio on one session clock | `start(options)` → a session with `frames`, `audio` and `events` (`started`, `paused`, `resumed`, `sourceChanged`, `ended` with a reason). `stop()`. Implementations: window capture (v1 candidate), embedded class view (v1 fallback, §4.2), replay from files (tests, later "import a recording"); later: desktop meeting apps (same window mechanism), a browser extension, mobile sources. |
| `SpeechActivityDetector` | Separate speech from non-speech | `detect(audio)` → ranges `{start, end, confidence}`. May be a simple heuristic. |
| `LanguageIdentifier` | Identify the language of an audio window or a text | `identify(audioWindow or text, candidates?)` → `[{language, confidence}]`. Declares which input kinds it accepts. |
| `SpeechToText` | Turn audio into timed text | Batch: `transcribe(chunk, {language, hints})` → segments `{start, end, text, confidence, language?}`. Streaming: `stream(audioTrack, {language})` → events with the same segment shape. Declares its mode, limits, timestamp granularity and languages. |
| `TextExtraction` | Read text from an image (optional) | `extract(image, {languages})` → blocks `{text, box, confidence}` |
| `VisionJudge` | Classify a candidate screenshot | `judge(image, {context})` → `{isLessonContent, type, title?, confidence, reason}` |
| `TextGenerator` | Produce structured text | `generate(task, input, outputSchema, {language, budget})` → an object already validated against the schema. Tasks: slide notes, merge visits, overview, glossary, announcements, review questions, grounding check. |
| `Storage` | Keep records and blobs durably | `append(chunk)`, `put`, `get`, `list`, `delete`, `usage()`, `requestPersistence()`. Data is durable when the call returns. |
| `DocumentRenderer` | Notes model to PDF and Markdown | `render(documentModel, template, languageProfiles)` → file |
| `UsageMeter` | Count consumption and estimated cost | `record(adapterId, units, estimatedCostIfPaid)`, `summary(session)` |
| `Clock` | One monotonic session clock | `now()` |

Not ports: pure computation on pixel buffers or text (differences, hashes, sharpness, deduplication, alignment, scoring). It lives in the domain and is unit-tested directly.

Every adapter exposes **metadata**: capability, `runsOn` (`device` or `remote`), `costTier` (`free` or `paid`), limits (maximum input size or duration, rate limits, daily quota), supported languages, mode (`batch` or `streaming`), data-use policy (`private`, `may-train` or `unknown`), whether it needs a key or relay, download size if on-device, and `status` (`tested` or `experimental`). The orchestrator uses this metadata to choose chunk sizes and concurrency, to show privacy notices, and to pick fallbacks.

### 6.3 Pipeline and jobs
- Stages are functions from typed input to typed output. Each persists its output, is idempotent, and is cached by content hash.
- Flow: `capture → [visual: detect → select → export] ∥ [audio: chunk → vad → transcribe → filter] → align → summarize → render`. A streaming recognizer replaces the "transcribe" step and runs beside capture.
- Stages operate on **units** (a chunk, a slide visit), so incremental processing during the class can be added later without redesign.
- A job record `{id, stage, unit, status, attempts, error}` is kept for each unit. v1 runs the orchestrator in-process behind an interface that allows remote workers later.

### 6.4 Logical data model
Session, CourseProfile, TimelineEvent (visit start/end, pause, resume, share-ended), SlideImage, SlideVisit, AudioChunk, TranscriptSegment, NotesUnit, SummaryDocument, RenderedDocument, UsageRecord, ProviderConfig, DecisionLog. Every record carries a `schemaVersion` and an `ownerId` (even if single-user now), so multi-user support later needs no migration of meaning.

### 6.5 Storage and retention
Local-first persistent storage in v1, behind the `Storage` interface so remote storage can be added later. Delete raw audio and frames after the PDF is generated unless the user opts to keep them. Keep the PDF and structured notes.

### 6.6 Configuration
One typed configuration: language registry, thresholds, caps, quality profiles, feature flags. Prompts live in versioned files with variables, and the prompt version is stored with each generated note. No language-specific text inside prompts or code.

### 6.7 Observability
Structured logs without transcript text by default. A downloadable per-session debug report (decisions, timings, provider usage). A developer-mode timeline view showing why each frame or segment was kept or rejected. Decisions use a small closed set of reason codes (examples: `stable-content`, `user-mark`, `transition`, `duplicate`, `not-lesson-content`, `cap-reached`, `non-speech`, `other-language`, `low-confidence`, `phantom-phrase`, `loop`). Extend it as needed, so tests and reports can count by reason.

### 6.8 Testing strategy (this is what saves tokens and money)
1. Deterministic unit tests for analysis code (comparison, stability, dedup, alignment).
2. A **replay harness** that feeds recorded sample sessions (video file plus audio file) through the same pipeline as live capture, so most work never needs a live class.
3. **Mock adapters** returning canned outputs, so UI and pipeline tests cost nothing and make no network calls.
4. A **golden-set evaluation script** that prints the metrics in §10.
5. A **provider benchmark script** that runs the same fixtures through different profiles and reports quality, latency and estimated cost.
6. A short **manual checklist** for real capture on the owner's operating system, which cannot be reliably automated. The agent prepares it and asks the owner to run it.

### 6.9 Security and secrets
Never ship developer secrets to the client and never commit secrets (`.env.example` only). In a desktop app everything inside the installer can be extracted, so developer secrets still must not be bundled; store user-supplied keys in the operating system's secure credential storage. Decide in Phase 0 between: (a) on-device processing, (b) user-supplied keys stored locally, (c) a thin relay with per-user limits. A public demo that uses the owner's free quota will be exhausted or abused, so it must use (a) or (b), or be limited. Treat slide text and transcripts as untrusted data: pass them to text generation as clearly delimited data, validate every output against its schema, escape everything before rendering, and never execute anything that comes from a model.

### 6.10 Performance, accessibility, i18n
Keep the main thread responsive, bound memory and cap concurrency. Report progress. Keyboard-navigable UI with labels for assistive tools. All UI strings externalized.

### 6.11 Suggested layout (by responsibility)
`/docs` (decisions, providers, manual test checklist), `/config`, `/src/domain`, `/src/ports`, `/src/adapters`, `/src/app`, `/src/shell` (desktop window, OS integration, packaging), `/fixtures` (small samples only), `/tools` (replay, eval, benchmark), `/tests`.

---

## 7. Free-first strategy and growth path

### 7.1 Quality profiles
A profile maps each stage to an adapter plus parameters. Switching profiles is a config change.

| Profile | Purpose | Typical choices (by capability) | Cost |
|---|---|---|---|
| **free** (default) | build, demo, personal use | on-device where quality and speed are acceptable, hosted free tiers otherwise, lighter models, conservative caps | $0 |
| **standard** | first real users | low-cost paid speech and language models, hosted relay | low |
| **premium** | only when a quality gap is proven | strongest speech, vision and language models, hosted workers | higher |

### 7.2 Sourcing free capabilities
Three families: **(A) on-device** (runs on the user's computer, natively or inside the app's web engine: zero marginal cost and private, but needs a model download and local compute, so it must be benchmarked on a typical student laptop ⚠), **(B) hosted free tiers** (easy and strong, but quota-limited, may use submitted data ✔, and carry terms-of-service risk), **(C) self-hosted open source** (needs hosting, so not v1).

Start the search with family (A) for every capability, because it is free forever, private and has no quota. Known on-device candidate classes (details and caveats in §9): open speech and text models run natively by the app (a desktop app is not limited to what runs in a web page), models built into a browser engine, and a browser engine's own on-device speech recognition. The last two exist only in specific browser versions and may be unavailable inside an app shell ⚠. They depend on the student's hardware, so ask for it (§12) and benchmark.

Evaluate every candidate with this checklist and record it in `docs/providers.md`:
free path without a credit card; maximum request size or duration (hosted speech services commonly cap uploads around 25 MB, and free tiers are stricter ✔); per-minute and per-day limits and the error behavior when exceeded; data-use and retention policy; terms of service for this use; quality on our fixtures for the target languages; latency; whether it can be called from a browser or needs a relay; download size if on-device; license; and a fallback.

### 7.3 Usage meter from day one
Per session record: audio minutes, calls, units used, and the **estimated cost if it were paid**, even in the free profile. This is the evidence for upgrade decisions.

### 7.4 Handling limits gracefully
Queue and back off. Split work. Degrade optional work first (extra sections, image-to-model calls) before core notes. Always tell the user what was reduced.

### 7.5 When and how to invest
Move beyond the free profile only when **any** of these holds:
1. Real usage regularly hits free limits.
2. The golden-set benchmark shows a quality gap caused by model quality (not by bugs) that a stronger profile closes.
3. Real users show repeat use and good ratings.
4. The owner decides to invest.

Procedure: run the provider benchmark on the golden set, compute the cost per class hour for each profile, choose the **smallest** upgrade that closes the gap, flip the profile, and add hosting, a relay, queue workers or accounts **only when needed**. "Investing" can mean stronger speech models, stronger vision and language models, hosted processing, accounts and history sync, and more capture sources.

### 7.6 Desktop distribution (free first)
- **v1:** build installers for the owner's operating system and share them with friends **unsigned**. The operating system shows a warning on first launch, so the README explains how to open the app anyway. This costs nothing.
- **Later (only after the §7.5 triggers):** paid code signing and notarization. Reported typical costs (single source, ⚠ re-check at the time): Apple's developer program about $99 a year, needed for a smooth macOS install; Windows signing certificates from a few hundred dollars a year, and even signed apps may show reputation warnings at first; a free route through the Windows store may exist ⚠. A mobile app adds store developer fees and review rules, to be researched when we get there ⚠.
- Auto-update is not needed in v1.

---

## 8. Build plan

### Phase 0: Feasibility spikes (disposable code, about a week)
Each spike answers a question with pass/fail. Output is `docs/decisions.md`, `docs/providers.md`, the fixtures, and the updated `AGENTS.md` (commands and layout).

| Spike | Question | Pass criteria |
|---|---|---|
| **S0 Shell** | Which way of building the desktop app fits our criteria? | Compare 2–3 candidates on: live-reload dev loop works (R9); window capture and system sound work on the owner's OS; UI stays responsive during capture; install size; free tooling; an installable build can be produced without paid accounts; the domain code can be reused by a future mobile app. Pick one and record it in `docs/decisions.md`. Prove it with a "hello capture" demo: pick a window, see a live preview and a live sound meter. |
| **S1 Capture** | Does window capture plus sound capture work for 15+ minutes on real class content, on the owner's operating system? | Sound complete and intelligible, and only the class sound (or the mixing is documented). Tested cases, each documented: class window visible; covered by another window; minimized; switching browser tabs inside the window; closing the window. With **our** app minimized for 10+ minutes, frames and sound continue at ≥90% of the expected cadence (⚠ items 1–2). Works with the owner's browser and one more. CPU and memory measured over 1 hour. Persistence: write 2 hours of chunks to disk, kill the app, verify nothing is lost. Option B (embedded class view) tried briefly with the owner's meeting platform: does it load, sign in, and isolate sound? Save 3–5 sample recordings as fixtures (with the teacher's permission). |
| **S2 Visual** | Can detection run offline on fixtures? | Hand-label slide changes. Recall ≥90%, precision ≥85%, ≤1 duplicate per class. Covers webcam bubble, incremental builds, returning to old slides, moving content, handwriting. Compare "first stable frame" with "sharpest stable frame" on legibility. |
| **S3 Audio** | Are independent chunks possible without losing words? Is transcription usable on real class audio with Arabic words mixed into French? | Chunk joins drop no words. No invented text on silent stretches after voice-activity filtering. Foreign-word behavior measured (including whether forced-language output of foreign speech stays in the primary script) and the four-layer language filter (§4.4) tried. Report leaked-foreign and wrongly-dropped rates. Batch and streaming recognizers both tried. |
| **S4 Providers** | What are the best free options per capability? | For each capability, 2–3 candidates, **starting with on-device ones**, checked against the §7.2 checklist and run on the same fixtures. Also compute one class hour's demand (audio minutes, calls, tokens) against each free limit. Default free profile and fallbacks chosen with numbers and links. |
| **S5 PDF** | Can we render mixed right-to-left and left-to-right text, images and long content correctly? | Correct shaping, fonts, page breaks and size in common PDF viewers. |
| **S6 Notes quality** | Do candidate prompts produce faithful, simple notes? | Owner rates ≥4/5 on 3 sample slides. No invented facts. |

### Phase 1: Walking skeleton
Build in this order, one vertical slice at a time. Each slice has its own tests and commit.
1. **Scaffold and contracts:** the desktop app opens with live reload via the `dev` command (R9), toolchain, commands filled into `AGENTS.md`, config and language registry, ports with **mock adapters**, fixture format, synthetic fixture generator, replay harness.
2. **Offline pipeline:** replay a synthetic fixture through the pipeline with mocks and get a PDF and a decision log from one command.
3. **Persistence and recovery:** storage adapter, resumable stages, "Recover session".
4. **UI flow:** setup, recording screen, processing, result, history, still driven by replay or mocks.
5. **Real capture:** the capture source chosen in S1 (window capture or embedded view). The owner runs the manual checklist.
6. **Real free adapters** for speech and text generation, chosen in S4.
7. **Installable build** for the owner's operating system (unsigned, §7.6) with short install notes.
**Acceptance:** a real 20-minute class becomes a PDF with no manual steps (quality may be rough). Crash recovery works. All tests pass.

### Phase 2: Quality
Importance cascade, dedup with revisits and builds, noise masks, calibration, alignment, summarization tuning, announcements and glossary, Review and fix, a second language enabled by config only.
**Acceptance:** §10 metrics met on the golden set.

### Phase 3: Robustness and portfolio polish
Long-session test (≥2 hours), quota handling and fallbacks, usage meter, error UX, privacy notices, accessibility, UI in three languages, README with a short demo animation, architecture diagram, decision log, installers for the operating systems decided in §12 with install notes (explain the unsigned-app warning), and a landing and download page on free hosting.
**Acceptance:** Definition of Done (§10).

### Phase 4: Growth (only after §7.5 triggers)
Premium profile, accounts, sharing, auto language detection, more capture sources (desktop meeting apps such as Zoom and Teams through the same window mechanism, with sound routing checked per app ⚠; a browser-extension variant for single-tab capture that avoids the picker and background limits ✔; and a **mobile app**: first for in-person classes with the microphone and photos of the board, then research whether recording an online class on a phone is possible on each mobile operating system ⚠), live notes.

---

## 9. Verified facts and risks

### ✔ Verified
**Browser display-capture and web-engine facts** (they apply if the desktop shell captures or records through a web engine, and to options B and C in §4.2):

- Tab audio capture through the browser's display-capture facility works on **desktop Chrome and Edge** (Chrome 74 and later). One source says system audio is offered only when sharing an entire screen on Windows and ChromeOS, and tab audio on every desktop OS. Support table (checked 2026-10-20): the call exists on desktop Chrome 72+, Edge 79+, Firefox 33+ (⚠ that table's Firefox figure may count an older, non-standard variant; irrelevant to v1) and Safari 13+. Mobile: not supported in Chrome, Firefox or Samsung Internet on Android; Safari on iOS lists support from version 27.0 (behavior and audio untested ⚠, relevant only for later mobile work).
- One practitioner reports that Firefox and Safari accept the audio request and silently return no audio track (unmeasured, no versions given). The app must therefore always check for an audio track, and v1 targets Chromium desktop browsers.
- The picker is mandatory, needs a user gesture and cannot be persisted. Chrome desktop offers controls to preselect the tab option, hide the entire-screen option, hide the current tab, and let the user switch the shared tab.
- Closing the shared tab ends capture.
- Hidden pages have timers limited to about once per second. The strictest tier (about once per minute) applies only when a page has been hidden more than 5 minutes, has been silent for at least 30 seconds, has a timer chain count of 5 or more, and uses no WebRTC. Pages that made sound in the last 30 seconds, or that hold a "live" media track or an open real-time data channel, are exempt from that tier, but not from the once-per-second tier. Whether a locally captured track counts as "live" for this rule is not stated ⚠ (item 1 below).
- Browser recorder slices need not be playable individually. Only the full concatenated recording must be.
- Storage: a site may use up to about 60% of the disk in Chromium and recent Safari. In Firefox the best-effort limit is the smaller of 10% of the disk or 10 GiB, and persistent storage can reach 50%. Asking for persistent storage protects data from automatic eviction (Chromium and Safari decide silently, Firefox asks the user). One quota covers structured storage, caches and the private file system, and exceeding it raises a quota error ✔.
- Hosted speech services often cap request size around 25 MB. Some free tiers allow the provider to use submitted data or give no privacy guarantee.
- Speech models invent text on non-speech audio. In one large study (one model, large-v3), hallucinations appeared in 121,378 of 301,317 inferences on non-speech audio (40.3%). Voice-activity pre-filtering gave the largest improvement, removing repetition loops and known phantom phrases afterwards came nearly as close, and the two together were best. Nothing eliminated it.
- Speech models fix the language per window or file and do not switch per word, which is why mixed-language speech is imperfect.
- Open speech models can run inside the page. The tiny, base and small classes are roughly 120 MB, 210 MB and 590 MB downloads. Public speed and accuracy figures are unsourced, so benchmark them ⚠. One article also reports hallucination and timestamp drift near chunk boundaries on long audio ⚠.
- The browser's own speech-recognition interface gained an on-device mode in Chrome 139 ✔, where audio and text are not sent to a third-party service. The documented example list of on-device languages (17, user-agent dependent) includes French and does not include Arabic ✔. The documentation says `start()` accepts an audio track, and marks the on-device parts experimental ✔. Edge's on-device model is a preview behind a flag with a different language list (not French) ✔. Whether Chrome honors a tab-audio track, runs for hours, and gives usable timestamps is unknown ⚠.
- Built-in browser AI (Chromium, desktop only): language-detection, translation and summarization interfaces are stable since Chrome 138 ✔. A general prompt interface with image and audio input is listed as stable for web pages in Chrome 148, while another sentence on the same page says extensions only ⚠. Audio input needs a GPU. Documented requirements: at least 22 GB free disk, more than 4 GB of GPU memory or 16 GB RAM with 4 or more cores, and an unmetered connection for the first download. Documented languages: English, Spanish, Japanese, German, French ✔. Quality for lecture speech is unknown ⚠.
- Slide detection pitfalls from a published browser-based tool: fades give half-transition frames, webcam overlays add noise, returning to old slides needs comparison with all kept slides, small text edits are hard to catch.
- The agent's instruction file (`AGENTS.md`) is read once at the start of each run, and the default combined size limit is 32 KiB, so keep it short.

**Desktop capture facts** (checked 2026-10-21; they depend on the operating system, so S0 and S1 must confirm them on the owner's machine):

- Desktop apps can list the computer's screens and windows and capture one of them ✔. A browser's individual tabs are not separate windows, so a desktop app captures the browser **window**, which shows whichever tab is active. Whether a given browser window appears in a framework's window list is not documented, so test it ⚠.
- Windows: system-wide sound capture ("loopback") exists ✔. The minimum Windows version and per-application capture were not verified ⚠.
- macOS: system sound capture needs a recent system. The newer route needs macOS 14.2 or later and a usage-description entry in the app's metadata; without it the app gets a silent sound stream and **no error** ✔. The older screen-capture route works from about macOS 13 (one source says 12.3), needs the Screen Recording permission and an app restart after granting it ✔. On macOS 12.7.6 and earlier, system sound capture needs a virtual audio device ✔. One vendor guide says that on macOS 26 and later the Screen Recording permission also covers system sound ⚠ (single source). Screen capture needs user consent on macOS 10.15 and later ✔.
- Linux: window and screen capture go through the desktop's own picker (portal and PipeWire), and in one framework only the single source the user picked is returned ✔. System sound capture on Linux was not verified ⚠.
- System sound capture is not isolated: other apps' sounds end up in the recording ✔ (stated for one macOS route; assume the same elsewhere ⚠).
- If the shell uses the operating system's own web view, media and capture features vary by OS and version (example: an embedded-WebKit screen-capture bug on macOS, fixed in 2024 ✔). A shell that bundles its own web engine avoids this but is larger ⚠.
- Development loop: one web-technology desktop stack reloads UI code live and restarts the app for main-process changes, and calls this similar to, but not true, hot reloading ✔. One cross-platform UI framework offers stateful hot reload in debug mode, while native-code changes need a full restart ✔. So UI work can be live, and capture or native code needs a quick restart plus the replay harness ⚠.
- Distribution costs: see §7.6.

### ⚠ Must be tested in Phase 0
1. **Our app window minimized or hidden:** do sampling and sound keep full cadence? If the shell runs capture in a web engine, hidden-page timer throttling may apply, and capture alone is **not guaranteed** to exempt a page. Design frame handling so it does not depend on timers or animation callbacks of a hidden page (for example stream-based frame readers off the UI thread, or capture in a background or native process).
2. **Sampling cadence over an hour:** drift, dropped frames, memory growth.
3. **The class window covered by another window or minimized:** captured pixels may freeze or go black depending on the OS and capture method. Test per OS and per browser. If frames freeze, the UX instruction is to keep the class window visible (side by side or on a second monitor). The older evidence about tab capture when the user switches tabs (an older browser design note says captured content counts as visible, a practitioner report says video and tab audio can freeze) matters only for options B and C.
4. **Browser tab-audio checkbox** (only if a browser-based capture is used, options B and C): sources disagree on whether it is pre-ticked.
5. **Right-to-left text in the PDF tool** chosen.
6. **Speed and accuracy of on-device speech recognition** on a typical student laptop.
7. **Sharpest-frame selection:** does picking the sharpest frame inside the stable window improve legibility on the owner's platform?
8. **Browser speech recognition, on-device:** is a tab-audio track accepted, does it run for hours, how good is French, are timestamps usable?
9. **Built-in browser AI:** available and good enough in French on the owner's laptop for language ID, summarization and screenshot judging? Is the prompt interface really open to web pages?
10. **Window listing:** does the chosen shell list the owner's browser window on the owner's OS, with a usable live preview?
11. **Per-application sound capture:** is it available on the owner's OS, and does it exclude other apps' sounds?
12. **Embedded class view (option B):** does the owner's meeting platform load, allow sign-in and play sound inside it?

---

## 10. Quality bar

### Golden set
3–5 sessions of 10–20 minutes, plus one session of 1.5 hours or longer for stress. Labels: slide-change timestamps and a 5-minute reference transcript.

| Metric | Initial target |
|---|---|
| Important slides captured (recall) | ≥ 90% |
| Captured slides that are truly important (precision) | ≥ 85% |
| Duplicate screenshots per class | ≤ 1 |
| Stray foreign-language text in notes | 0 |
| Primary-language speech wrongly dropped by the language filter | ≤ 3% of spoken time (starting point) |
| Invented facts in a 20-note spot check | 0 critical |
| Readability and simplicity (owner, 1–5) | ≥ 4 |
| Processing time after End (free profile) | report it; aim for ≤ 0.5× class duration |
| Cost within free limits | $0 |
| Recovery after a forced crash | no data lost |

### Definition of Done for v1
- A real 2-hour class runs end to end with no crash, and the PDF is useful.
- Metrics above are met on the golden set.
- A second language works through configuration only.
- Consent notice and provider data-use notices are in place.
- A manual checklist exists and was executed by the owner.
- README with demo, architecture diagram and decision log. An installable build for the owner's operating system with install notes, and a free landing and download page.

### Manual checklist (owner runs; agent maintains it in `docs/`)
First-run permission flow on a clean user account; start and stop; sound level visible; switch browser tabs inside the captured window and return; cover or minimize the class window and return; minimize our app for 10 minutes; change slides quickly; return to an old slide; share a window with a webcam bubble; pause (button and shortcut) and resume; kill the app mid-session and recover; produce a PDF and open it in two viewers.

---

## 11. Working agreement for the agent

**Communication:** be brief. Report what changed, what was verified, what was not, and the next step.
**Planning:** at the start of each phase, propose a plan of 15 lines or fewer with tasks and acceptance checks. Proceed without waiting inside a phase. Ask the owner only about the items in §12.
**Coding:**
- Small vertical slices. Each slice ends with tests passing and a commit.
- Edit instead of rewriting. Keep modules small and cohesive. No dead code.
- Comments explain why, not what.
- Add a dependency only with a reason, and record it in `docs/decisions.md`.

**Token discipline:** read only the sections you need. Do not print whole logs. Use the replay harness and mocks, not live runs and model calls, in tests. Put repeated instructions into `AGENTS.md` once. Do not re-research items marked ✔ here; test the ⚠ ones. Provider limits change, so re-check the official docs once at integration time and record the date in `docs/providers.md`.
**Honesty:** never say something works unless you ran it. Mark assumptions. If a spike contradicts this brief, update the brief and tell the owner.
**Free-first discipline:** check official docs for limits before integrating. Never add a paid-only dependency. No flows that require a credit card.
**Decision records:** `docs/decisions.md` entries contain the date, the decision, the options considered, the evidence (links and numbers) and the consequences.

---

## 12. Decisions that belong to the owner

Ask only these, each with a recommended default:
1. **Which operating system and version (Windows, macOS or Linux), meeting platform(s), browser and computer the owner uses** (platform name, browser, RAM, and whether the computer has a dedicated graphics card). Needed for S0, S1 and S4. The operating system decides the sound-capture route (§9). Recommended default: build for the owner's operating system first. Ask once.
2. **Permission to record sample classes** for fixtures, ideally including a stretch where the teacher mixes in the other language, which is the hardest case. If the teacher declines, use public or self-made sample content.
3. **Privacy stance for real classes.** Recommended default: use on-device or `private` providers only. A provider that may use submitted data (`may-train` or `unknown`) is allowed only after an explicit opt-in per session, because the teacher's voice belongs to a third party.
4. **Product name.** Default: keep "LectureLens" as a placeholder.
5. **Key-handling approach** (§6.9). Recommended: on-device where acceptable, otherwise user-supplied keys. No shared developer key in a public demo.

Everything else: decide, record in `docs/decisions.md`, and move on.

---

## 13. Sources (checked 2026-10-19, 2026-10-20 and 2026-10-21)

Sources are evidence, not endorsements. Nothing in this brief requires any product they mention.

- Browser display-capture controls and versions: https://developer.chrome.com/docs/web-platform/screen-sharing-controls
- Tab audio support by browser and OS: https://addpipe.com/getdisplaymedia-demo/ and https://dev.to/flo152121063061/i-tried-to-capture-system-audio-in-the-browser-heres-what-i-learned-1f99
- Tab capture vs display capture, background behavior, tab closing: https://www.recall.ai/blog/how-to-build-a-chrome-recording-extension
- Timer throttling rules and exemptions: https://developer.chrome.com/blog/timer-throttling-in-chrome-88
- Recorder slices need not be playable alone: https://lists.w3.org/Archives/Public/public-dap-commits/2013Aug/0000.html and https://developer.mozilla.org/en-US/docs/Web/API/MediaRecorder/ondataavailable
- Speech-model hallucinations on non-speech audio: https://arxiv.org/pdf/2501.11378
- Mixed-language speech limits: https://github.com/openai/whisper/discussions/49
- On-device speech recognition sizes and speed claims: https://offlinetts.com/blog/browser-speech-recognition-whisper-comparison/
- Slide detection approach and pitfalls: https://dev.to/larryxue/how-i-built-a-browser-based-video-to-powerpoint-converter-ag0
- Agent instruction file loading and size limit: https://learn.chatgpt.com/docs/agent-configuration/agents-md
- Free-tier limits and data-use notes (speech and language services), gathered earlier in this project: https://eesel.ai/blog/groq-pricing , https://toolfreebie.com/?p=237 , https://geotoolbox.ai/blog/gemini-api-pricing
- Browser support table for display capture (checked 2026-10-20): https://caniuse.com/mdn-api_mediadevices_getdisplaymedia
- Display-capture options and the permission and gesture rules: https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getDisplayMedia
- Conflicting practitioner reports on audio support and the "share tab audio" default: https://dev.to/flo152121063061/i-tried-to-capture-system-audio-in-the-browser-heres-what-i-learned-1f99 and https://addpipe.com/getdisplaymedia-demo/
- Built-in browser AI status and requirements: https://developer.chrome.com/docs/ai/built-in-apis and https://developer.chrome.com/docs/ai/prompt-api
- Browser speech recognition, on-device mode: https://developer.chrome.com/blog/new-in-chrome-139 , https://github.com/WebAudio/web-speech-api/blob/main/explainers/on-device-speech-recognition.md , https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition/start , https://developer.mozilla.org/docs/Web/API/SpeechRecognition/available_static , https://learn.microsoft.com/fi-fi/microsoft-edge/web-platform/speech-recognition-api
- Storage quotas and eviction: https://developer.mozilla.org/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria
- Hallucination figures (numbers checked in the paper body): https://arxiv.org/pdf/2501.11378
- Desktop window and screen capture, system sound, macOS and Linux caveats: https://www.electronjs.org/docs/latest/api/desktop-capturer
- System sound capture routes and versions on macOS and Windows (vendor guides, treat with care): https://www.recall.ai/blog/how-to-get-access-to-system-audio and https://nonstrict.eu/recordkit/guides/system-audio-recording.html
- Embedded web view screen-capture bug on macOS (fixed 2024): https://bugs.webkit.org/show_bug.cgi?id=274896
- Desktop distribution costs and friction (single blog, verify before relying): https://www.osola.org.uk/blog/publish_apps.html
- Development loop examples: https://electron-vite.org/guide/hmr-and-hot-reloading and https://docs.flutter.dev/tools/hot-reload
