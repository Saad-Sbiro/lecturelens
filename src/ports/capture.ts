export type ErrorCode = 'Unavailable' | 'QuotaExceeded' | 'Unsupported' | 'BadInput' | 'Transient';
export class CapabilityError extends Error {
  constructor(public readonly code: ErrorCode, message: string) { super(message); }
}
export interface AdapterMetadata {
  capability: string; runsOn: 'device' | 'remote'; costTier: 'free' | 'paid';
  dataUse: 'private' | 'may-train' | 'unknown'; requiresKey: boolean;
  status: 'tested' | 'experimental'; limits: Record<string, number | string>;
  supportedLanguages: readonly string[]; mode: 'batch' | 'streaming';
}
export interface Clock { now(): number }
export type CaptureEvent = { atMs: number; type: 'started' | 'paused' | 'resumed' | 'sourceChanged' | 'ended'; reason: string };
export interface MediaChunk { sequence: number; atMs: number; bytes: Uint8Array; reason: 'capture-chunk' | 'replay-chunk' }
export interface CaptureOptions { signal: AbortSignal; timeoutMs: number }
export interface CaptureSession {
  // Containers carry synchronized video and audio. Demuxing is an adapter concern.
  media: AsyncIterable<MediaChunk>;
  events: CaptureEvent[];
  pause(): void;
  resume(): void;
  stop(reason?: string): Promise<void>;
}
export interface CaptureSource {
  metadata: AdapterMetadata;
  start(options: CaptureOptions): Promise<CaptureSession>;
}
