export const captureConfig = {
  width: 1280, height: 720, frameRate: 10,
  videoBitsPerSecond: 1_500_000, audioBitsPerSecond: 96_000,
  chunkMs: 2000, maxQueuedBytes: 32 * 1024 * 1024,
  signalThreshold: 0.003, signalFreshMs: 5000,
  silenceWarningMs: 15_000, reserveBytes: 256 * 1024 * 1024,
  estimatedMinutes: 120,
} as const;
