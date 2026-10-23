# Local capability inventory — checked 2026-10-21

| Capability | Candidate | Cost / limits | Privacy / keys | Status |
|---|---|---|---|---|
| Desktop shell / capture | Electron, Chromium media APIs | MIT; no service quota; device CPU/disk limits | Device/private; no key | S0/S1 experimental |
| Storage | Node filesystem, flushed immutable chunks | No service quota; local free space | Device/private; no key | Synthetic corruption/kill/recovery tests passed; live power-loss unverified |
| Replay | Local fixture adapter | No service quota | Device/private; no key | Synthetic persistence replay tested; media analysis replay pending |

Official license/API sources are linked in `decisions.md`. Remote speech and note generation are optional adapters with separate per-job consent; local speech and offline notes remain available. S4's real class-hour quality and throughput comparison is still pending owner-approved fixtures. Zero service cost is not evidence of adequate quality.

## Optional Groq speech preview — checked 2026-10-23

The app can send three-minute, 16 kHz mono WAV chunks to `whisper-large-v3`. It keeps each completed transcript checkpoint locally, so a quota or network interruption resumes instead of restarting. Images never go to this endpoint. The API key is protected with Windows DPAPI and audio consent resets after every job.

The published Free Plan limits for both Whisper models are 20 requests/minute, 2,000 requests/day, 7,200 audio seconds/hour and 28,800 audio seconds/day. Exact organization limits can differ. Free uploads are limited to 25 MB; LectureLens chunks are about 5.8 MB. The Free Plan returns HTTP 429 at its limit and the app never requests a paid upgrade. Groq says inference data is not retained by default, with exceptions for features that require retention and security/abuse investigation; owners should review Data Controls before using real classes.

- Speech API: https://console.groq.com/docs/speech-to-text
- Free limits: https://console.groq.com/docs/rate-limits
- Data controls: https://console.groq.com/docs/your-data
- Billing tiers: https://console.groq.com/docs/billing-faqs

Only mock audio has been sent during automated checks. A real quality/latency result requires the owner to add a Free Plan key in **AI connections**, enable cloud transcription for one lesson, approve that job's audio upload, and review names/numbers against the recording.

## Personal NVIDIA notes preview — updated 2026-10-23
NVIDIA API catalog/access and a synthetic short French response were checked previously with Nemotron 3.5 Lightning 30B A3B (1.46 s for 70 tokens, not a lesson benchmark). The quality default is now the currently listed free endpoint `nvidia/nemotron-3.5-super-vl-120b-a12b`; Lightning remains selectable for speed. The app sends transcript text only, keeps images local, disables thinking, and caches completed chronological parts. Model availability and evaluation quotas can change and no fixed free allowance is promised. A real Super result was not claimed without running an owner key.
- Current catalog: https://build.nvidia.com/models?label=Chat&q=nemotron
- Lightning model: https://build.nvidia.com/nvidia/nemotron-3.5-lightning-30b-a3b/modelcard
- Cookbook: https://github.com/NVIDIA-NeMo/Nemotron/blob/main/usage-cookbook/Nemotron-3.5-Lightning/README.md
- Trial FAQ: https://forums.developer.nvidia.com/t/nvidia-nim-faq/300317
