# Automatic crop trial in the browser extension

The owner authorized integration after the standalone feasibility tool. The browser preview now starts Gemini detection after the recording/upload consent notice. No credentials are stored in extension files: the native host reads a CurrentUser-DPAPI-encrypted Gemini key from local app settings. Model remains pinned to `gemini-3.8-flash`, rather than the moving `gemini-flash-latest` alias. Medium image resolution and LOW thinking are used. No billing is enabled or changed; ensure the key's Google project remains on Free tier. The API key alone does not let the app verify billing status.

## Try it

1. Finish any current recording. Open `chrome://extensions` and reload LectureLens (the updated build is in `dist/extension`).
2. Open a lesson tab, accept the updated recording/image-upload notice and choose Preview.
3. A box appears when detection succeeds. Recording can start immediately with the full-frame fallback while detection is pending. Manual “Choose slide area” locks your chosen box and stops cloud detection. “Retry automatic crop” enables it again during preview.
4. Once per second, a local 160×90 grayscale comparison checks for meaningful visual change. More than 12% of comparison blocks must change to invalidate a previously analyzed scene; small cursor movements are ignored. A new scene must settle for two seconds (no more than 2.5% block changes) before upload. Full-frame candidates are retained while re-detection is pending. Requests run at most once per minute and at most 10 per automatic-detection activation; identical stable scenes, including scenes classified as non-educational, need no further request. Quota/authentication failures disable further requests for that activation.
5. Review saved slides: verify no text is cut off, whether moved/resized content is found, and whether unnecessary UI is included. Check that audio continues during detection and when the network is unavailable.

## Reliability and limitations

Vision uses a separate native host connection; requests never enter the audio persistence queue. Responses are applied only while the detector is active and the scene remains similar. No model-generated image is used: a 1.5% margin is applied to normalized coordinates and the existing canvas crops locally from the full-resolution captured frame. The full-frame JPEG sent to Gemini is quality-bounded to fit native messaging; extremely large frames fall back safely. Invalid, tiny or missing regions keep full-frame images. At the request cap, later changed scenes remain full-frame.

Scene-change detection is approximate: a sufficiently small movement or subtle change can evade it. This is an experimental crop aid, not verified exact localization. Up to one minute of full-frame slides may appear while waiting for a new detection. Paused sessions make no new detection requests, but a request already sent may finish. The manual crop remains available in preview. Closing/reloading the extension during capture still ends its stream; reload only after recording finishes.

Actual response usage and normalized detections are stored under `%LOCALAPPDATA%/LectureLens/vision-cache`; no key is included. Cache keys cover image bytes, model, prompt and settings. `.usage.json` records actual usage, including failed model-output validation; cache reuse makes no request. Recording visual-event logs distinguish Gemini detection, scene-change fallback, failure and cap. Private screenshot cache files and keys are outside the repository.

Verified offline: native request/config, margin, cache, usage and quota handling; extension stale-response rejection, no-repeat behavior, manual override, independent persistence while vision hangs, and normal durability tests. No real lesson or model call was run by the agent. Detection accuracy and account/model access need the owner's live check.
