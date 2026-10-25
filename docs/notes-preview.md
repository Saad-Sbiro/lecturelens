# Study PDF preview
Start with npm run dev (close the previous app first after C# changes). No installer build needed.

1. Select a finished, healthy recording.
2. Add transcript: choose a reviewed .txt or transcript.json from the local speech probe. Full-session speech is not wired to this screen yet.
3. Select detail level, output language and whether to include saved slide images.
4. Allow sending this transcript for this export, then Generate study PDF.
5. The PDF opens when ready. Exports are in %LOCALAPPDATA%/LectureLens/exports, separate from raw sessions.
6. Keep originals when needed; otherwise confirm removal only after checking the PDF.
7. AI settings permits replacing the encrypted key and model ID. Never put a key in Git or the extension.

Manual acceptance: compare French terminology and source references, check a long transcript, both image choices, cancel and retry, verify reopening preserves preferences, and verify declining deletion retains the recording. Image inclusion means a local appendix, not AI interpretation of slides.

Add Chrome extension opens the extensions page and copies dist/extension to the clipboard. Enable Developer mode, Load unpacked, paste that folder path. This remains a one-time developer install until a Chrome Web Store listing exists.

Offline notes checks:
.tools/dotnet/dotnet.exe run --project tests/NotesChecks/NotesChecks.csproj
Add -- --render for an actual synthetic local PDF rendering probe (no network or live capture).

Lesson-only PDF acceptance (2026-10-25):
- Restart the desktop app using `npm run dev`, select an existing lesson and generate a new PDF. Existing exports are unchanged.
- Check connected explanatory paragraphs and compare the first and final substantive concepts against the source. Audience questions, microphone checks, logistics and unrelated remarks should be absent. A useful teacher answer should appear as a lesson explanation.
- Inspect `notes.json` beside the PDF for one keep/mixed/exclude decision per source and source references for each section/diagram. Check mixed segments manually: structural validation cannot prove their selected clauses are correct.
- Expect diagrams only for relationships actually stated in the lesson. Confirm their labels, ordering and comparisons; check captured images next to their matching explanations. Generated diagrams remain available with captured screenshots disabled.
- Synthetic offline checks: `.tools/dotnet/dotnet.exe run --project tests/NotesChecks/NotesChecks.csproj`. Add `-- --render` for French and temporary Arabic-registry layout fixtures in `artifacts/pdf-preview`. This does not enable Arabic in the product or test a live model.

Arabic PDF output (experimental): restart with `npm run dev`, keep Teacher language set to the actual spoken language, then select العربية (تجريبي) under PDF language and generate. Review technical terminology, omissions and diagram labels against the original lesson. The current NVIDIA model does not document Arabic among its post-training focus languages; quality is not yet measured on real lessons. The Arabic rendering fixture now uses the real registry entry, not a temporary test locale. Arabic speech recognition is not enabled by this change.
