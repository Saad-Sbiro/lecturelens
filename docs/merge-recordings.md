# Merge saved recordings

Open **Merge recordings** in the desktop lesson library. Add two or three stopped recordings, select a row and use **Move up / Move down** to set playback order. Enter a name and choose **Create merged lesson**. The result appears as a new selected lesson. Choose **Transcribe** to create its combined text, then generate a PDF normally. Original transcripts and PDFs remain attached to the originals.

Merging runs entirely locally with the already configured audio decoder. It produces 16 kHz, mono, 16-bit PCM WAV plus ordered slide captures. It joins parts directly, without inserting time between recording sessions. It does not join independent WebM files byte-for-byte and does not combine full-motion video. Originals are retained. If the local decoder is missing, run the existing `npm run speech:setup` before merging.

Active, archived, duplicate, corrupt or empty inputs are rejected. Interrupted recordings with intact acknowledged packets are eligible. Sources are locked against recording/deletion/cleanup during the operation. The output uses a private staging folder and appears in the library only after all media and metadata are complete. Cancellation or failure removes that operation's staging folder, never the sources. A process crash can leave a hidden `.merge-*` folder; original recordings remain available and merging can be retried.

`merge.json` retains original IDs, titles, order, decoded durations and offsets. Source decision/event logs are retained under `source-events/`. Slide timestamps subtract recorded pauses and add the decoded duration of earlier parts; encoder timing differences mean these remain approximate. Raw-audio cleanup includes the merged WAV and packet copies. The merged lesson is independent of later deletion of the originals.

## Verification

`npm run test:merge` uses synthetic PCM through a mock decoder, without media capture, model inference or network access. It checks two/three-part ordering, WAV size, slide offsets across pauses, source preservation, recovery, cancellation, failed decoding, invalid selections, active/corrupt/archived input rejection and raw cleanup. A native UI check exercises add/remove/reorder and button limits, writing `artifacts/merge-window.png`.

Manual check: after restarting the app, merge two short saved lessons in reverse order; play the recovered `audio.wav` to confirm the boundary and order, then transcribe the new lesson and view its slides. Actual-class decoding and listening quality require this owner check.
