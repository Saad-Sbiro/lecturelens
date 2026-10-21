# Local capability inventory — checked 2026-10-21

| Capability | Candidate | Cost / limits | Privacy / keys | Status |
|---|---|---|---|---|
| Desktop shell / capture | Electron, Chromium media APIs | MIT; no service quota; device CPU/disk limits | Device/private; no key | S0/S1 experimental |
| Storage | Node filesystem, flushed immutable chunks | No service quota; local free space | Device/private; no key | Synthetic corruption/kill/recovery tests passed; live power-loss unverified |
| Replay | Local fixture adapter | No service quota | Device/private; no key | Synthetic persistence replay tested; media analysis replay pending |

Official license/API sources are linked in `decisions.md`. No remote speech, vision, language or document provider is integrated. S4's 2–3 candidate benchmarks per capability, class-hour budgets, quality and throughput are **pending real fixtures**. Zero service cost is not evidence of adequate local performance.
