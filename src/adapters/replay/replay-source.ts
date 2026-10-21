import { readFile } from 'node:fs/promises';
import { CapabilityError, type AdapterMetadata, type CaptureOptions, type CaptureSession, type CaptureSource, type MediaChunk } from '../../ports/capture';

export interface ReplayFixture { schemaVersion: number; kind: string; chunks: { atMs: number; hex: string }[] }
export class ReplaySource implements CaptureSource {
  readonly metadata: AdapterMetadata = {
    capability: 'CaptureSource', runsOn: 'device', costTier: 'free', dataUse: 'private', requiresKey: false,
    status: 'tested', limits: { fixtureBytes: 1_000_000 }, supportedLanguages: [], mode: 'streaming',
  };
  constructor(private readonly fixture: ReplayFixture) {}
  static async fromFile(filename: string): Promise<ReplaySource> {
    const data = await readFile(filename);
    if (data.length > 1_000_000) throw new CapabilityError('Unsupported', 'Fixture too large');
    return new ReplaySource(JSON.parse(data.toString()));
  }
  async start(options: CaptureOptions): Promise<CaptureSession> {
    if (this.fixture.schemaVersion !== 1 || this.fixture.kind !== 'synthetic-persistence' || !Array.isArray(this.fixture.chunks)) {
      throw new CapabilityError('BadInput', 'Unsupported fixture schema');
    }
    let last = 0;
    for (const chunk of this.fixture.chunks) {
      if (!Number.isFinite(chunk.atMs) || chunk.atMs < last || !/^(?:[0-9a-f]{2})+$/.test(chunk.hex)) throw new CapabilityError('BadInput', 'Invalid fixture chunk');
      last = chunk.atMs;
    }
    let stopped = false;
    let paused = false;
    let wake: (() => void) | undefined;
    const deadline = performance.now() + options.timeoutMs;
    const events: CaptureSession['events'] = [{ type: 'started', atMs: 0, reason: 'replay-start' }];
    const chunks = this.fixture.chunks;
    const check = () => {
      if (options.signal.aborted || performance.now() > deadline) throw new CapabilityError('Unavailable', 'Replay cancelled or timed out');
    };
    check();
    return {
      events,
      media: (async function* (): AsyncGenerator<MediaChunk> {
        for (const [sequence, chunk] of chunks.entries()) {
          check();
          if (paused) await new Promise<void>((resolve, reject) => {
            const abort = () => { clearTimeout(timer); reject(new CapabilityError('Unavailable', 'Replay cancelled')); };
            const timer = setTimeout(abort, Math.max(0, deadline - performance.now()));
            options.signal.addEventListener('abort', abort, { once: true });
            wake = () => { clearTimeout(timer); options.signal.removeEventListener('abort', abort); resolve(); };
          });
          check();
          if (stopped) break;
          yield { sequence, atMs: chunk.atMs, bytes: Buffer.from(chunk.hex, 'hex'), reason: 'replay-chunk' };
        }
        events.push({ type: 'ended', atMs: last, reason: 'replay-ended' });
      })(),
      pause: () => { paused = true; },
      resume: () => { paused = false; wake?.(); },
      stop: async () => { stopped = true; wake?.(); },
    };
  }
}
