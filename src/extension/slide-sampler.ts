import { visualConfig as config } from '../../config/visual';
import { difference, SlideIndex, suggestCrop, validCrop, type Crop, fullCrop } from '../domain/slides';
import type { NativeClient } from './native-client';
import { CropTracker, overlap, assessContent, type CropMode } from '../domain/visual-policy';

export function canvasFrame(video: HTMLVideoElement, crop: Crop, width: number, height: number) {
  const canvas = document.createElement('canvas'); canvas.width = width; canvas.height = height;
  canvas.getContext('2d')!.drawImage(video, crop.x * video.videoWidth, crop.y * video.videoHeight, crop.width * video.videoWidth, crop.height * video.videoHeight, 0, 0, width, height);
  return canvas;
}
export function gray(canvas: HTMLCanvasElement) {
  const rgba = canvas.getContext('2d')!.getImageData(0, 0, canvas.width, canvas.height).data;
  return Uint8Array.from({ length: canvas.width * canvas.height }, (_, i) => Math.round(0.299 * rgba[i * 4] + 0.587 * rgba[i * 4 + 1] + 0.114 * rgba[i * 4 + 2]));
}
export function detectCrop(video: HTMLVideoElement, anchor?: Crop) {
  const width = anchor ? 640 : 320, height = Math.max(1, Math.round(width * video.videoHeight / video.videoWidth));
  return suggestCrop(gray(canvasFrame(video, fullCrop, width, height)), width, height, anchor);
}
type Candidate = { signature: Uint8Array; jpeg: string; atMs: number; crop: Crop; reason: string; review: boolean };
export class SlideSampler {
  private index = new SlideIndex();
  private pending?: Candidate;
  private busy: Promise<void> = Promise.resolve();
  private running = false;
  private closed = false;
  private last = -Infinity;
  private dimensions: string;
  private lastSlide = -1;
  private cloudCrop?: Crop;
  saved = 0;
  readonly tracker: CropTracker;
  get currentCrop() { return this.crop; }
  status = 'waiting-for-sample';
  constructor(private video: HTMLVideoElement, private crop: Crop, private host: NativeClient, private id: string, private clock: () => number, private failed: (e: unknown) => void, mode: CropMode = 'manual', private regionProvider?: () => { crop: Crop; reason: string; review: boolean }) {
    if (!validCrop(crop)) throw new Error('Invalid slide crop');
    this.dimensions = `${video.videoWidth}:${video.videoHeight}`;
    this.tracker = new CropTracker(crop, mode);
  }
  tick() {
    if (this.closed || this.running || this.clock() - this.last < config.sampleMs) return;
    this.running = true;
    this.busy = this.sample().catch(this.failed).finally(() => { this.running = false; });
  }
  private async log(atMs: number, reason: string, slide?: number, crop = this.crop) {
    await this.host.call('visualEvent', { id: this.id, atMs, reason, slide, crop });
  }
  private async commit(candidate: Candidate, stability: string) {
    const result = this.index.classify(candidate.signature);
    if (result.keep) {
      await this.host.call('slide', { id: this.id, slide: result.slide, atMs: candidate.atMs, crop: candidate.crop, data: candidate.jpeg, reason: `${candidate.review ? 'review' : 'keep'}:${candidate.reason}:${stability}` }); this.saved++;
    }
    const reason = !result.keep && result.slide === this.lastSlide && result.slide >= 0 ? 'unchanged-slide' : result.reason;
    await this.log(candidate.atMs, `${reason}:${stability}`, result.slide, candidate.crop);
    this.lastSlide = result.slide;
  }
  private async sample() {
    const atMs = this.clock(), previous = this.last; this.last = atMs;
    if (Number.isFinite(previous) && atMs - previous > config.sampleMs * 1.5) await this.log(atMs, 'sampling-gap');
    if (this.video.readyState < 2) { await this.log(atMs, 'no-video-frame'); return; }
    const dimensions = `${this.video.videoWidth}:${this.video.videoHeight}`;
    if (dimensions !== this.dimensions) {
      await this.log(atMs, 'capture-resized'); this.dimensions = dimensions;
    }
    let proposal = this.tracker.mode === 'auto' ? detectCrop(this.video) : { crop: this.crop, confidence: 1 };
    if (this.tracker.mode === 'auto' && proposal.confidence < 0.75) {
      try {
        let timeout: ReturnType<typeof setTimeout> | undefined;
        const hints = await Promise.race([
          chrome.runtime.sendMessage({ target: 'background', op: 'layoutHints' }),
          new Promise<never>((_, reject) => { timeout = setTimeout(() => reject(new Error('Layout lookup timeout')), 1500); }),
        ]).finally(() => clearTimeout(timeout));
        if (Array.isArray(hints) && hints[0] && validCrop(hints[0])) proposal = { crop: hints[0], confidence: 0.8 };
      } catch { /* No DOM access: retain a review candidate, never stop audio. */ }
    }
    let region = this.regionProvider?.() ?? this.tracker.observe(proposal);
    if (this.regionProvider && region.crop.width < .98) {
      // Cloud checks are throttled; track the known slide's edges locally on
      // every sample so a resize cannot become a padded duplicate in that gap.
      const fresh = !this.cloudCrop || ['x', 'y', 'width', 'height'].some(key => region.crop[key as keyof Crop] !== this.cloudCrop![key as keyof Crop]);
      const anchor = fresh ? region.crop : this.crop;
      this.cloudCrop = { ...region.crop };
      const local = detectCrop(this.video, anchor);
      if (local.confidence >= .90) region = { crop: local.crop, reason: 'local-slide-boundary', review: false };
      else region = { ...region, crop: anchor };
    }
    this.crop = region.crop; this.status = region.reason;
    await this.log(atMs, region.reason);
    const signature = gray(canvasFrame(this.video, this.crop, config.compareWidth, config.compareHeight));
    const content = assessContent(signature, config.compareWidth, config.compareHeight);
    await this.log(atMs, content.reason);
    if (content.action === 'reject') {
      if (this.pending) await this.commit(this.pending, 'before-blank');
      this.pending = undefined; this.status = content.reason; return;
    }
    const canvas = canvasFrame(this.video, this.crop, Math.max(1, Math.round(this.video.videoWidth * this.crop.width)), Math.max(1, Math.round(this.video.videoHeight * this.crop.height)));
    let quality: number = config.jpegQuality;
    let jpeg = canvas.toDataURL('image/jpeg', quality).split(',')[1];
    while (jpeg.length * 0.75 > config.maxImageBytes && quality > 0.4) { quality -= 0.1; jpeg = canvas.toDataURL('image/jpeg', quality).split(',')[1]; }
    if (jpeg.length * 0.75 > config.maxImageBytes) throw new Error('Slide image exceeds storage limit');
    const next = { signature, jpeg, atMs, crop: { ...this.crop }, reason: content.reason, review: region.review || content.action === 'review' };
    if (this.pending) {
      if (overlap(this.pending.crop, this.crop) > 0.92 && difference(this.pending.signature, signature) < config.changedFraction) {
        await this.commit({ ...next, atMs: this.pending.atMs }, 'stable');
      } else await this.commit({ ...this.pending, review: true }, 'brief-content-review');
    }
    this.pending = next;
    await this.log(atMs, 'sampled-candidate');
  }
  async pause() {
    await this.busy;
    if (this.pending) await this.commit(this.pending, 'pause-boundary');
    this.pending = undefined;
    this.last = -Infinity; this.lastSlide = -1;
  }
  async finish() { this.closed = true; await this.pause(); }
}
