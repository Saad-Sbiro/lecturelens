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
