# Local capability inventory — checked 2026-10-21

| Capability | Candidate | Cost / limits | Privacy / keys | Status |
|---|---|---|---|---|
| Desktop shell / capture | Electron, Chromium media APIs | MIT; no service quota; device CPU/disk limits | Device/private; no key | S0/S1 experimental |
| Storage | Node filesystem, flushed immutable chunks | No service quota; local free space | Device/private; no key | Synthetic corruption/kill/recovery tests passed; live power-loss unverified |
| Replay | Local fixture adapter | No service quota | Device/private; no key | Synthetic persistence replay tested; media analysis replay pending |

Official license/API sources are linked in `decisions.md`. No remote speech, vision, language or document provider is integrated. S4's 2–3 candidate benchmarks per capability, class-hour budgets, quality and throughput are **pending real fixtures**. Zero service cost is not evidence of adequate local performance.

## Personal NVIDIA notes preview — 2026-10-22
NVIDIA API catalog/access and a synthetic short French response were checked. Selected Nemotron 3.5 Lightning 30B A3B with thinking disabled; measured 1.46 s for 70 tokens, not a long-summary benchmark. Model access and evaluation quotas may vary; no fixed free allowance is promised. HTTP 429/5xx are bounded retries with cached completed parts. This optional personal experiment does not replace the free-first deployment requirement.
- Model: https://build.nvidia.com/nvidia/nemotron-3.5-lightning-30b-a3b/modelcard
- Cookbook: https://github.com/NVIDIA-NeMo/Nemotron/blob/main/usage-cookbook/Nemotron-3.5-Lightning/README.md
- Trial FAQ: https://forums.developer.nvidia.com/t/nvidia-nim-faq/300317
