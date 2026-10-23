# Standalone educational-surface detection

This is an isolated experiment, not an automatic crop feature in the recorder. No production code imports it. It uses the existing Electron image decoder/cropper and Node fetch; no dependency or model SDK was added.

## Run

Create a Gemini API key in https://aistudio.google.com/apikey using a project that has **Free tier and no billing enabled**. Do not paste the key into source or chat. In PowerShell:

```powershell
$secureKey = Read-Host 'Gemini API key' -AsSecureString
$env:GEMINI_API_KEY = [System.Net.NetworkCredential]::new('', $secureKey).Password
npm run vision:spike -- "C:\path\lecture.png" --allow-upload --free-tier
Remove-Item Env:GEMINI_API_KEY
```

The flags acknowledge uploading this screenshot and confirm that you selected a non-billed project. The tool cannot independently determine billing status from a key; it never enables billing, changes plans, falls back to another model, or automatically retries. The free tier may use submitted content to improve Google's products. Use permitted, non-sensitive screenshots. Exact account quotas are shown in AI Studio; 429 stops with a retry hint.

PNG/JPEG only, up to 14 MB and 40 MP. The original full-resolution bytes are sent; medium resolution controls model processing, not the locally cropped source. Gemini 3.8 Flash does not support minimal thinking; LOW is the lowest supported setting. No sampling overrides or image generation are used.

For an entirely offline UI/geometry preview (fixed fake box, not accuracy evidence):

```powershell
npm run vision:spike -- "C:\path\lecture.png" --mock
npm run test:vision
```

Add `--no-open` to save without opening the comparison window. `--smoke` opens, captures the comparison and exits. Detection JSON contains only `box`, `type`, `isEducational`; tool stdout has that object (npm may print its own command banner). Use `node tools/run-electron.mjs tools/vision-spike.cjs ...` to avoid npm banners.

## Results

Each run writes under ignored `artifacts/vision-spike/`: `report.html` (offline original and crop side by side), `detection.json`, `metrics.json`, and `crop.png` when a valid educational box exists. Coordinates are YX normalized 0–1000. Pixel edges round outward, add 1.5% of the detected width/height on each side, then clamp to the original dimensions. The overlay shows the final padded crop. Source images are never overwritten. Non-educational results produce no crop. Invalid/reversed/out-of-range boxes fail clearly instead of guessing a correction.

Local response cache keys include exact input bytes, MIME, model, prompt and full generation configuration. Cached runs send no request. Usage is copied verbatim from API `usageMetadata`, including thinking tokens if returned; missing usage stays null. `requestUsage` is null on cache hits, while `usageMetadata` preserves the original request's usage. Attempts also record usage when response validation fails. Delete the corresponding cache file only when intentionally requesting another paid-in-quota measurement. Mock runs are labelled and never populate the live cache.

## Accuracy evaluation (not yet measured)

Collect at least 20 permitted screenshots: large slides, small nested slides, moved/resized content, PDF pages, code editors, whiteboards, webcams/chat only, and ambiguous multiple surfaces. Run each once and inspect the report. Record expected type and a human-labelled tight box, whether text was cut off, unrelated UI included, missed/false detections, latency and actual tokens. Compare pixel boxes with the labelled box using IoU; keep margin-inclusive and tight-box scores separate. Medium resolution may miss small text; do not declare the feature ready based on synthetic fixtures or one good screenshot. Cache hits are not new accuracy samples.

Official documentation checked 2026-10-23:
- https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash — LOW/MEDIUM/HIGH; minimal unsupported.
- https://ai.google.dev/gemini-api/docs/pricing — standard Flash input/output free tier; account quotas apply.
- https://ai.google.dev/gemini-api/docs/media-resolution
- https://ai.google.dev/gemini-api/docs/structured-output

Real API availability, token usage and detection accuracy remain unverified until a permitted screenshot is run with a Free-tier Gemini key.
