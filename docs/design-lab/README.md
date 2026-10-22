# Design lab — Phase 1, slice 4

Status: three coded directions ready for the owner's choice. No direction is approved yet. This work does not change Phase 0 plans or the native recording pipeline.

## Slice plan and acceptance

1. Reuse the existing three mockups; provide recording and result screens for each.
2. Make the comparison runnable offline, with Arabic/French UI and system/light/dark themes.
3. Preserve keyboard focus when a demo action rerenders; clearly label synthetic data and inert exports.
4. Render screenshots and check overflow, text contrast, pause/resume, reduced motion and absence of network calls.
5. Show all three directions to the owner in simple Arabic; lock production tokens only after the choice.

## Directions

| | Orbit / مدار | Paper / ورق | Prism / طيف |
|---|---|---|---|
| Core idea | A quiet instrument keeps the class safely in view. | A class becomes a thoughtfully arranged notebook. | A clear workspace gathers moments into study notes. |
| Layout | Utility rail, large slide, circular time/signal, filmstrip. | Editorial heading, offset sheet, generous margins, page-first result. | Signal panel beside slide, timeline, outline beside result. |
| Color roles | Evergreen canvas, mint signal/action, pale neutral slide. | Warm paper canvas, charcoal text, terracotta action, cream sheet. | Ink-blue canvas, lavender action, lilac secondary surfaces. |
| Type pairing | IBM Plex Sans Arabic UI; Noto Naskh Arabic note headings. | Noto Naskh Arabic display; IBM Plex Sans Arabic controls and French text. | Alexandria display; IBM Plex Sans Arabic controls; Noto Naskh Arabic notes. |
| Shape language | Circular signal, restrained rounded frames. | Nearly square sheets, fine rules, small corner radii. | Broad rounded panels, compact geometric markers. |
| Depth and lighting | Small signal halo, low surface contrast. | Offset paper shadow, flat ink, no glow. | Layered opaque panels, restrained tonal depth. |
| Illustration | Original lens geometry and teaching diagrams. | Original lesson diagrams and printed-note composition. | Original timeline marks and geometric lesson diagrams. |
| Motion proposal | Signal responds only to real audio; accepted slide settles into strip. | Short page settling and quiet crossfades, no page-turn delay. | Brief marker arrival and ordered stage transitions. |

Both themes exist for every direction. The French lesson and Arabic PDF intentionally stay independent of UI locale. All content is synthetic; no numbers imply measured capture or actual processing. Motion above describes the proposed production behavior; these direction previews do not loop or invent live progress. Only small control feedback transitions are implemented.

## Run

`npm run design:preview` writes `artifacts/design-lab/preview.html`. Open that local file to switch direction, screen, theme and language. No server, account, network service or new package installation is needed to build or open it.

`npm run design:check` uses an existing Playwright installation and installed Edge. Set `LECTURELENS_PREVIEW_NODE_MODULES` to its containing `node_modules` directory, and optionally `LECTURELENS_PREVIEW_BROWSER` to a Chromium executable. In Codex, locate the bundled runtime with `load_workspace_dependencies`; do not hard-code another user's machine path. The check writes twelve screenshots here and a compact summary to stdout; `verification.json` contains the detailed evidence.

The fixture is also embeddable in the conversation. The host supplies its carousel and design controls. The standalone wrapper supplies equivalent selectors; neither is part of the production app.

## Boundaries and next slice

These are HTML comparison mockups of a desktop UI, not the WPF implementation. Browser scale screenshots approximate 125% and 150% scaling; native DPI, window dragging/snapping, GPU/CPU cost, screen-reader behavior and real capture cadence remain unverified. Native checks come after selection, without running live captures automatically.

After the owner chooses: translate the selected direction into provisional config tokens (colors, type, spacing, radii, elevation, motion, z-order), native resources and a state gallery. Then implement the recording and result screens driven by replay/mock adapters. Add empty/loading/error/offline/permission-denied states, keyboard paths and native scaling checks in that slice. Phase 3 remains the full polish pass. No production visual token set or brand replacement is approved by this lab.

Font licenses are included beside the existing font assets; sources and tooling rationale are recorded in `docs/decisions.md`. Only the regular face of each family is embedded in the comparison to keep the inline preview under 1 MB.
