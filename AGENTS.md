# AGENTS.md — LectureLens

Always-loaded rules. Keep this file under 60 lines. Full spec: `PROJECT_BRIEF.md` (read §0–§2 and §11 once, then only the section a task needs).

## What this is
An installable **desktop app** (a mobile app comes later; not a hosted website). It records a class that runs in a browser window (screen + sound), finds important slides, transcribes the speech, and outputs a PDF of simple study notes with slide screenshots. Later sources: desktop meeting apps (Zoom, Teams), then mobile. French is the first language, but nothing may assume French.

## Non-negotiable rules
1. **Free-first:** v1 runs at $0 with no credit card. Never add a paid-only dependency. Check official docs for free limits before integrating any service.
2. **Technology-neutral domain:** vendors, models and frameworks appear only inside adapters. Domain code depends on the capability interfaces in `PROJECT_BRIEF.md` §6.2.
3. **Language-agnostic:** no hard-coded language outside the language registry and test fixtures. Teacher, PDF and UI languages are independent.
4. **Privacy:** consent notice before recording. Delete raw media after the PDF unless the user opts in. No secrets in client code or commits (`.env.example` only).
5. **Never lose a recording:** persist incrementally and support recovery.
6. **Explainable:** every kept/rejected frame and segment gets a logged reason.
7. **Fast dev loop:** `dev` must open the desktop app with live reload for UI changes. Slow-to-reload parts are covered by the replay harness.
8. UI work must follow DESIGN_BRIEF.md (premium, original, alive; never at the cost of capture reliability).

## Workflow
- Start each phase with a plan of 15 lines or fewer (tasks plus acceptance checks), then proceed. Ask the owner only about items in `PROJECT_BRIEF.md` §12.
- Small vertical slices. A slice ends with tests passing, then a commit.
- Edit, do not rewrite. Keep modules small. No dead code. Comments explain why.
- A new dependency needs a reason in `docs/decisions.md`.
- Tests use the replay harness and mock adapters. Do not call real model or network services in tests. Do not run live captures; ask the owner to run the manual checklist when real-browser behavior matters.
- Items marked ⚠ in the brief are unverified. Test them in Phase 0 before building on them. If a spike contradicts the brief, update the brief and tell the owner.

## Token discipline
Read only the brief section you need. Do not print whole logs or large files. Do not re-read files you just edited. Do not paste the brief into prompts.

## Honesty
Never claim something works unless you ran it. State what was verified, what was not, and the next step. Keep reports short.

## Commands (fill in during Phase 0, then keep current)
- install: `npm ci`
- dev (native WPF, XAML live reload): `npm run dev` (.NET 10 SDK; local `.tools/dotnet` supported)
- test: `npm test` (offline); shell checks: `npm run smoke` / `npm run smoke:dev`
- lint/typecheck: `npm run typecheck`
- speech spike: `npm run speech:setup`; `npm run speech:spike -- <audio.webm> --seconds 60`; mocks: `npm run test:speech`
- replay a fixture: `npm run replay -- fixtures/synthetic-session.json`
- golden-set eval: `npm run eval` (readiness only until labelled fixtures exist)
- native preview: `npm run package:native` → `release/native-win-x64/LectureLens.exe`; check: `npm run smoke:native`
- browser spike: `npm run dev` prepares extension/host; load `dist/extension` once in Chrome. Checks: `npm run test:native`, `npm run test:extension`, `npm run typecheck:extension`.
- legacy Electron installer: `npm run package`; disk crash spike: `npm run test:durability`

## Layout (fill in after Phase 0 scaffold)
`docs/` (decisions, providers, manual checklist) · `config/` · `src/domain` · `src/ports` · `src/adapters` · `src/app` · `src/shell` · `fixtures/` · `tools/` · `tests/`
