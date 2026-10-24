# DESIGN BRIEF — LectureLens

Owner direction, 2026-10-24: preserve the current app design. Improve animation and smoothness only. The owner subsequently asked for clearly visible, Discord-inspired responsiveness.

- Preserve the current layout, colors, typography, dimensions, icons and light/dark themes.
- Use short, restrained transitions for control feedback, dialog/notification entry and lesson changes.
- Keep controls immediately usable. Never delay a capture action or dialog dismissal to finish an animation.
- Use render-only translation, scale and opacity: button lift/press, row feedback, dialog scale-in, menu/toast slide-in and smooth pixel scrolling. Preserve resting layout. Processing motion must reflect a real running job; no invented percentages or audio activity.
- Respect Windows animation preferences. Keep keyboard focus visible and restore it when a dialog closes.
- Replace interrupted transitions rather than queueing them. Release animation clocks and subscriptions when views unload.
- Recording, persistence and analysis run independently of presentation changes.
- Check native shell loading, modal acceptance and rapid interaction with synthetic UI tests; do not run a live capture.
