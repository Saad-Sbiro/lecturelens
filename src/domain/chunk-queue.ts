import { CapabilityError, type MediaChunk } from '../ports/capture';

export class ChunkQueue implements AsyncIterable<MediaChunk> {
  private chunks: MediaChunk[] = [];
  private bytes = 0;
  private ended = false;
  private failure?: Error;
  private wake?: () => void;
  constructor(private readonly maxBytes: number) {}
  push(chunk: MediaChunk): void {
    if (this.ended) throw new CapabilityError('BadInput', 'Queue is closed');
    if (this.bytes + chunk.bytes.byteLength > this.maxBytes) {
      throw new CapabilityError('Unavailable', 'Disk cannot keep up with capture; end recording to preserve saved chunks');
    }
    this.chunks.push(chunk);
    this.bytes += chunk.bytes.byteLength;
    this.wake?.();
  }
  end(error?: Error): void { this.ended = true; this.failure = error; this.wake?.(); }
  async *[Symbol.asyncIterator](): AsyncIterator<MediaChunk> {
    while (true) {
      const chunk = this.chunks.shift();
      if (chunk) { this.bytes -= chunk.bytes.byteLength; yield chunk; continue; }
      if (this.ended) { if (this.failure) throw this.failure; return; }
      await new Promise<void>(resolve => { this.wake = resolve; });
      this.wake = undefined;
    }
  }
}
