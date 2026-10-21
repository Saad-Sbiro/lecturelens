# S2 plan — sparse slide capture and automatic crop

Current addition: automatic/manual modes, selected-tab DOM geometry fallback, two-sample region relocation, blank-screen rejection and structured/ambiguous content triage. Manual crops stay locked. Geometry scores are heuristic, not calibrated probabilities. OCR, topic relevance and learned semantic classification are not implemented. Full-frame candidates can be retained when automatic crop is uncertain; review labels make this explicit.

1. Owner evidence: Chrome playback recording isolated tab picture/audio, continued with browser minimized, and paused/resumed successfully; duration and resource use were not measured.
2. Target storage: continuous audio plus timestamped cropped slide images/visits, rather than a high-frame-rate archival video. Keep the current recoverable WebM path until the replacement passes equivalent durability checks.
3. Default candidate sampling: one full-resolution image every 5 seconds; cadence is configurable and independent of audio chunks. Measure actual timestamps/gaps while minimized; timer scheduling is not assumed reliable.
4. Locate the presentation viewport from browser layout where available, behind a browser-specific adapter; do not hard-code coordinates or assume the whole tab is a slide.
5. Refine that viewport with image boundaries, rectangular contours, plausible proportions and temporal consistency. In screen-shared PowerPoint, find the slide inside the video, excluding editor chrome and thumbnails.
6. Use confidence thresholds and show the proposed rectangle. Ambiguous crops require a user-adjustable rectangle; retain an uncropped candidate for review rather than irreversibly cutting off lesson content.
7. Re-evaluate the crop on resize, fullscreen, layout or presentation-source changes. Compare only crops aligned to the same coordinate system; layout changes are not automatically new slides.
8. Downscale the crop for block-wise visual comparison. Ignore small cursor/compression noise; chat outside the crop must have no effect. Keep the full-resolution crop for readable output.
9. Track pending changes/stability, retaining uncertain brief content as review candidates. With 5-second samples, slides shown entirely between samples can be missed; do not promise the earlier 2–3-second dwell precision.
10. Deduplicate against all prior slide crops; revisiting a slide adds a timed visit. Test small meaningful text changes separately from noise before enabling incremental-build replacement.
11. Persist retained cropped images, normalized crop coordinates, visits, confidence and every keep/reject reason incrementally. Raw candidate retention remains temporary and bounded.
12. Offline fixtures: changing chat with static slide, cursor, compression, white/dark slides, borderless slides, editor thumbnails, fullscreen/resize, transitions, builds, repeated slides, code/whiteboard and no-slide video.
13. Acceptance: labelled change recall/precision per brief S2, no duplicates caused solely by chat, crop excludes chrome without cutting slide text, bounded memory, recoverable image/audio manifests; manual minimized cadence/audio check by owner.
14. Initial slice now runs: source video requested at 1 fps for preview/analysis, candidates every 5 seconds, audio-only MediaRecorder, cropped JPEGs and atomic slide envelopes. The higher-frame-rate source is no longer archived as video.

Implemented scope: geometric edge-based crop proposal with explicit user confirmation/drag correction, block comparison, all-kept deduplication, stability/brief-content reasons, timestamps, 720-image cap, recoverable JPEG envelopes and a local gallery. Capture-dimension changes stop the session so the crop can be confirmed again. Same-size layout changes require manual re-preview.

Deferred: DOM layout hints, automatic layout-change tracking, calibrated confidence, sharpest-frame ranking, incremental-build merging, semantic filtering and labelled precision/recall evaluation. An ambiguous proposal defaults to the full frame for user correction. One screenshot from the owner yielded the inner PowerPoint slide correctly (normalized crop approximately x=.447, y=.344, w=.397, h=.383); this is not general accuracy evidence. No real capture was run by the agent.
