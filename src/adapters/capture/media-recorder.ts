import { captureConfig } from '../../../config/capture';
import { ChunkQueue } from '../../domain/chunk-queue';
import { CapabilityError, type AdapterMetadata, type CaptureOptions, type CaptureSession, type CaptureSource, type Clock } from '../../ports/capture';

export function chooseMimeType(audioOnly = false): string {
  const choices = audioOnly ? ['audio/webm;codecs=opus'] : ['video/webm;codecs=vp8,opus', 'video/webm;codecs=vp9,opus'];
  const mime = choices.find(type => MediaRecorder.isTypeSupported(type));
  if (!mime) throw new CapabilityError('Unsupported', 'No supported video/audio encoder');
  return mime;
}

export async function requestPreview(signal: AbortSignal, timeoutMs: number): Promise<MediaStream> {
  if (signal.aborted) throw new CapabilityError('Unavailable', 'Capture cancelled');
  let abandoned = false;
  let timer: ReturnType<typeof setTimeout>;
  let abort: () => void;
  const pending = navigator.mediaDevices.getDisplayMedia({
    video: { width: { ideal: captureConfig.width }, height: { ideal: captureConfig.height }, frameRate: { ideal: captureConfig.frameRate, max: captureConfig.frameRate } },
    audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false },
  }).then(stream => {
    if (abandoned) { stream.getTracks().forEach(track => track.stop()); throw new CapabilityError('Unavailable', 'Capture request expired'); }
    return stream;
  });
  const cancel = new Promise<never>((_, reject) => {
    abort = () => { abandoned = true; reject(new CapabilityError('Unavailable', 'Capture cancelled or timed out')); };
    timer = setTimeout(abort, timeoutMs);
    signal.addEventListener('abort', abort, { once: true });
  });
  try { return await Promise.race([pending, cancel]); }
  catch (error) {
    if (error instanceof CapabilityError) throw error;
    throw new CapabilityError('Unavailable', error instanceof Error ? error.message : 'Capture permission or source unavailable');
  }
  finally { clearTimeout(timer!); signal.removeEventListener('abort', abort!); }
}

export class BrowserMediaSource implements CaptureSource {
  readonly metadata: AdapterMetadata = {
    capability: 'CaptureSource', runsOn: 'device', costTier: 'free', dataUse: 'private', requiresKey: false,
    status: 'experimental', limits: { chunkMs: captureConfig.chunkMs }, supportedLanguages: [], mode: 'streaming',
  };
  constructor(private readonly stream: MediaStream, private readonly mimeType: string, private readonly clock: Clock) {}
  async start(options: CaptureOptions): Promise<CaptureSession> {
    if (options.signal.aborted) throw new CapabilityError('Unavailable', 'Capture cancelled');
    if ((!this.mimeType.startsWith('audio/') && !this.stream.getVideoTracks().some(t => t.readyState === 'live')) || !this.stream.getAudioTracks().some(t => t.readyState === 'live')) {
      throw new CapabilityError('Unavailable', 'Both live video and audio are required');
    }
    const recorder = new MediaRecorder(this.stream, {
      mimeType: this.mimeType, videoBitsPerSecond: captureConfig.videoBitsPerSecond, audioBitsPerSecond: captureConfig.audioBitsPerSecond,
    });
    const queue = new ChunkQueue(captureConfig.maxQueuedBytes);
    const events: CaptureSession['events'] = [{ type: 'started', atMs: this.clock.now(), reason: 'user-start' }];
    let sequence = 0;
    let pendingBytes = 0;
    let write = Promise.resolve();
    let failure: Error | undefined;
    let endReason = 'capture-ended';
    let resolveStop: () => void;
    const stopped = new Promise<void>(resolve => { resolveStop = resolve; });
    const stop = (reason = 'user-stop') => { if (recorder.state !== 'inactive') { endReason = reason; recorder.stop(); } };
    const abort = () => stop('cancelled');
    const sourceEnded = () => stop('source-ended');
    recorder.ondataavailable = event => {
      if (!event.data.size) return;
      pendingBytes += event.data.size;
      if (pendingBytes > captureConfig.maxQueuedBytes) {
        failure = new CapabilityError('Unavailable', 'Capture buffer exceeded its limit'); stop(); return;
      }
      const atMs = this.clock.now();
      write = write.then(async () => {
        try {
          const bytes = new Uint8Array(await event.data.arrayBuffer());
          queue.push({ sequence: sequence++, atMs, bytes, reason: 'capture-chunk' });
        } finally { pendingBytes -= event.data.size; }
      }).catch(error => { failure = error; stop(); });
    };
    recorder.onerror = () => { failure = new CapabilityError('Unavailable', 'Media encoder failed'); stop(); };
    recorder.onstop = () => {
      options.signal.removeEventListener('abort', abort);
      this.stream.getTracks().forEach(track => track.removeEventListener('ended', sourceEnded));
      events.push({ type: 'ended', atMs: this.clock.now(), reason: failure ? 'encoder-or-backpressure' : endReason });
      void write.finally(() => { queue.end(failure); resolveStop(); });
    };
    options.signal.addEventListener('abort', abort, { once: true });
    this.stream.getTracks().forEach(track => track.addEventListener('ended', sourceEnded, { once: true }));
    try { recorder.start(captureConfig.chunkMs); }
    catch (error) {
      options.signal.removeEventListener('abort', abort);
      this.stream.getTracks().forEach(track => track.removeEventListener('ended', sourceEnded));
      throw new CapabilityError('Unavailable', error instanceof Error ? error.message : 'Encoder could not start');
    }
    return {
      media: queue, events,
      pause: () => {
        if (recorder.state !== 'recording') return;
        recorder.requestData(); recorder.pause();
        events.push({ type: 'paused', atMs: this.clock.now(), reason: 'user-pause' });
      },
      resume: () => {
        if (recorder.state !== 'paused') return;
        recorder.resume(); events.push({ type: 'resumed', atMs: this.clock.now(), reason: 'user-resume' });
      },
      stop: async reason => { stop(reason); await stopped; },
    };
  }
}
