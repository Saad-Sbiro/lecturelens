# DESIGN BRIEF — LectureLens

Owner direction, 2026-10-24: preserve the current app design. Improve animation and smoothness only. The owner subsequently asked for clearly visible, Discord-inspired responsiveness.

Owner direction, 2026-10-25: replace the upper-right paper preview with an original, very cute living companion. Lumi is a cream woodland creature with long soft ears, a sage scarf and a small app-logo forehead badge. Its gaze and body follow the in-app cursor; clicks, keyboard activity and lesson selection get brief expressive reactions. This explicitly permits character artwork and idle motion in that panel. Preserve the surrounding workspace. The companion is local presentation, never a recording or job-status indicator; respect reduced motion and pause when inactive, hidden or minimized.

- Preserve the current layout, colors, typography, dimensions, icons and light/dark themes.
- Use short, restrained transitions for press feedback, dialog/notification entry and lesson changes. Button hover styling is immediate and never animated.
- Keep controls immediately usable. Never delay a capture action or dialog dismissal to finish an animation.
- Use render-only translation, scale and opacity: button press, row feedback, dialog scale-in, menu/toast slide-in and smooth pixel scrolling. Preserve resting layout. Processing motion must reflect a real running job; no invented percentages or audio activity.
- Desktop capture opens as a full workspace subpage, matching Review slides: a Back to lesson button, 26px heading, 24px page spacing and the active app theme/font. It replaces the workspace rather than showing a centered modal. Its capture helper has no independent frame or taskbar presence. Back and Escape use the existing stop-and-save close guard before returning.
- Respect Windows animation preferences. Keep keyboard focus visible and restore it when a dialog closes.
- Replace interrupted transitions rather than queueing them. Release animation clocks and subscriptions when views unload.
- Recording, persistence and analysis run independently of presentation changes.
- Check native shell loading, modal acceptance and rapid interaction with synthetic UI tests; do not run a live capture.

Lumi refinement, 2026-10-25: the owner wants a companion for everyone, with more varied movement and better cursor tracking. Keep a friendly adventure-buddy appearance: warm neutral ear/paw accents, muted teal scarf, subtle cheeks, mostly stars and an occasional small heart. Rotate eight playful and three gentle gestures without immediate repetition; alternate gesture directions across cycles. Eyes lead, head and body follow with separate damping across the entire workspace. Finish each gesture before accepting another; ignore extra reactions without queueing them. Leave room for ears at the extremes of hops and gaze. Never consume the user's clicks or let cosmetic reactions delay controls.

Lumi interaction polish, 2026-10-25: show only the name below the artwork, with no hover tooltip. Use the existing app logo on the forehead and one continuous body/leg silhouette. The card stays still on mouse/keyboard press while preserving keyboard focus. Only enabled button activations may trigger gestures, with a 70% probability while idle; background clicks, typing and list/language selection do not trigger gestures. Existing moves finish before another can start.
