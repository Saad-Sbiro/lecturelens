# S2 plan — sparse slide capture and automatic crop

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
14. This is the agreed implementation direction, not a shipped detector: the existing capture still requests up to 10 fps until sparse capture and crop checks are implemented together.
