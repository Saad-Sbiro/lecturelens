# S3 local speech spike — 2026-10-22

Status: experimental batch probe, not a desktop feature yet. No API keys, uploads or paid services.

## Run

- npm run speech:setup (CPU engine + Small Q5 + Silero); add -- --compare for Base Q5.
- npm run speech:spike -- "C:/path/audio.webm" --model small --seconds 60
- --model base compares the lighter model. --language takes an enabled language-registry code; the default comes from teacher preferences in config/languages.ts.
- npm run test:speech runs mocks only, without model/network access.

Outputs are under a new ignored artifacts/speech/<id>/ directory: sample.wav, engine.log, engine.json, transcript.json, transcript.txt. Reports never overwrite earlier runs. Source recordings stay untouched. These private artifacts are not automatically deleted: remove them after review when no longer needed.

## Assets and compatibility

Pinned whisper.cpp v1.8.3 Windows x64 CPU build, multilingual small-q5_1 (190,085,487 bytes), base-q5_1 (59,707,625 bytes), and Silero v6.2.0 (885,098 bytes). SHA-256 is verified during setup. Fixed model revisions and hashes live in config/speech-assets.json. Downloads are public, no login. The engine and Silero use MIT licensing; preserve their license notices before distributing them.

This is a replay harness. The existing Electron development dependency decodes the saved WebM locally to 16kHz mono PCM; this does NOT add a WebView to the WPF app. HTTP/HTTPS is blocked in the decoder. A production native decoder must replace this harness before exposing a desktop button. The probe accepts up to 40 MiB encoded input and processes at most 180 seconds; decoding currently loads the whole input in memory, so this is not an hours-long-session implementation.

CPU inference uses four threads with GPU disabled. Windows 10 on the owner's machine was verified; older CPU instruction sets, ARM and other PCs remain unverified. No universal minimum hardware claim. The latest .NET wrapper is not assumed compatible.

## Observed measurements (not unit tests)

A 60-second sample from an existing finished local audio recording, sequential runs:

| Model | Inference + report time | Segments |
|---|---:|---:|
| Small Q5 | 29.80 seconds | 5 |
| Base Q5 | 7.86 seconds | 5 |

Decode/setup time excluded. A mid-run OS observation of Small showed 526 MiB working set and 529 MiB peak-to-that-point for the engine only; this is NOT a measured whole-app peak. VAD reported 42% sample reduction on this recording. Small produced zero transcript segments on a separate five-second synthetic-silence probe (0.30 seconds). This does not establish robustness against music/noise.

Both transcripts contain questionable phrases. No human reference transcript exists yet: accuracy, word-error rate, names, mixed French/Darija behavior and dropped words are unscored. No claims of usable lecture quality yet. Owner should review the same minute before choosing a default. Small remains the provisional quality candidate; Base is a speed option, not an automatic silent downgrade.

## Remaining S3 work

- Label real audio and compare transcript quality; test mixed languages, noise, long silences, and sentence boundaries.
- Validate VAD timestamp remapping with known inserted-silence fixtures. Engine segment offsets currently use decoded audio time; capture pause gaps have NOT been mapped to the slide/session clock.
- Four-layer language filtering, overlap/chunk joins, streaming, quality filtering and slide alignment remain unimplemented.
- Confidence is null when unavailable, never fabricated. Text is explicitly unreviewed. VAD decisions remain in engine.log; formal kept/rejected range reasons remain to be integrated.
- Add resumable, bounded-memory native decoding/processing and cancellation before long-session UI integration.

## Optional NVIDIA experiment

Owner clarified that NVIDIA API would be for personal evaluation, potentially summarization for PDF notes, not the deployed product. No cloud integration or upload was performed. NVIDIA's FAQ says trial rate limits vary by model/load and should be checked in the account. Trial access fits internal evaluation; no fixed free-token allowance is assumed.

Sources:
- https://github.com/ggml-org/whisper.cpp/releases/tag/v1.8.3
- https://github.com/snakers4/silero-vad
- https://huggingface.co/ggerganov/whisper.cpp
- https://forums.developer.nvidia.com/t/nvidia-nim-faq/300317
- https://assets.ngc.nvidia.com/products/api-catalog/legal/NVIDIA%20API%20Trial%20Terms%20of%20Service.pdf
