# Native desktop preview

**Historical interface-only build:** the current `npm run dev` also prepares the experimental browser capture extension/helper. Use [current browser instructions](browser-tab-test.md). The portable EXE described below has not been rebuilt; daily tests use `dev`.

This is a runnable WPF interface preview, not the browser-capture MVP. No WebView, recording, transcription or PDF generation runs in this build. The recording control is disabled deliberately. The old `LectureLens Setup 0.0.1.exe` remains the Electron window-capture prototype and does not isolate tabs.

## Run

From the repository, use `npm run dev`. It builds C# and opens the native window. Editing `src/native/Desktop/MainWindow.xaml` reloads the view in place; C# edits require restarting the command. Node only launches the development command; the published desktop app does not require Node.

The launcher uses `.tools/dotnet/dotnet.exe` when present, otherwise a system .NET 10 SDK. A fresh development machine must install that SDK. `npm ci` alone does not install .NET. Legacy Electron dependencies remain for the existing capture/replay tests; they are not loaded by the native launcher.

## Portable executable

Run `npm run package:native`, then open `release/native-win-x64/LectureLens.exe`. Keep the whole folder together: it includes the .NET runtime and local UI files. The executable uses app-relative runtime lookup, so no separate .NET installation is required for this Windows x64 portable build. Packaging copies the .NET 10 runtime supplied by the installed Windows SDK. This is not an installer and does not replace an installed legacy shortcut.

## Verification

`npm run smoke:native` opens the actual WPF application with a temporary XAML copy, changes that copy, verifies live reload, and renders `artifacts/native-shell.png`. It does not capture the screen or browser. Results are in `artifacts/native-smoke.json`.

Verified on the owner's machine: native build with zero warnings/errors, actual-window XAML reload, published EXE opening without `DOTNET_ROOT` or development variables, 11 existing offline replay/storage tests, and TypeScript checks. Packaged startup used `LectureLens.exe --smoke` and exited successfully. This does not establish compatibility with other PCs or validate browser recording.

Owner check: open the executable, resize/minimize/restore the window, and verify the local-recordings-folder button. Recording is unavailable; do not use this preview to record a class. The later Chrome extension needs separate two-tab picture/audio isolation and crash-recovery checks.
