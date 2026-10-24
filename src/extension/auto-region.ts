import { fullCrop, validCrop, difference, type Crop } from '../domain/slides';
type Region = { crop: Crop; reason: string; review: boolean };
type Reply = { detection?: { crop?: Crop; reason?: string }; error?: string; disabled?: boolean; retryable?: boolean };
// Network latency never enters the recorder or sampler promise chain.
export class AutoRegion {
  region: Region = { crop: fullCrop, reason: 'auto-waiting', review: true };
  private reference?: Uint8Array;
  private current?: Uint8Array;
  private stableFrame?: Uint8Array;
  private stableSince = 0;
  private inFlight = false;
  private last = -Infinity;
  private calls = 0;
  private closed = false;
  private disabled = false;
  private transientFailures = 0;
  private retryAt = -Infinity;
  private observedAt = 0;
  private retained(reason: string): Region { return { crop: this.region.crop, reason, review: true }; }
  constructor(private query: (data: string) => Promise<Reply>) {}
  observe(signature: Uint8Array, image: () => string, now: number) {
    if (this.closed) return;
    this.observedAt = now;
    this.current = signature;
    // Require a settled scene, not an animation/scroll frame. Tiny pointer changes
    // are below the block-difference threshold and do not restart this timer.
    if (!this.stableFrame || difference(this.stableFrame, signature, this.region.crop) > .025) {
      this.stableFrame = signature; this.stableSince = now;
    }
    if (this.reference && difference(this.reference, signature, this.region.crop) > .12) {
      this.region = this.retained('auto-scene-changed'); this.reference = undefined;
    }
    if (this.calls >= 10 && !this.inFlight && !this.reference && !this.disabled) this.region = this.retained('auto-limit');
    if (this.inFlight || this.disabled || this.calls >= 10 || now < this.retryAt || now - this.last < 60000 || this.reference || now - this.stableSince < 2000) return;
    this.last = now; this.calls++; this.inFlight = true;
    // A new slide changes content, not necessarily its position. Keep the last
    // detected box while rechecking; never feed surrounding UI to the sampler.
    this.region = this.retained('auto-detecting');
    const submitted = signature;
    void Promise.resolve().then(() => this.query(image())).then(reply => {
      if (this.closed) return;
      this.disabled = reply.disabled === true;
      if (reply.error) {
        let reason = reply.error;
        if (reply.retryable && !reply.disabled) {
          this.transientFailures++;
          if (this.transientFailures <= 3 && this.calls < 10) {
            const seconds = 60 * 2 ** (this.transientFailures - 1) + Math.floor(Math.random() * 6);
            this.retryAt = this.observedAt + seconds * 1000;
            reason += ` Automatic retry in ${seconds}s.`;
          } else { this.disabled = true; reason += ' Automatic retries exhausted; retry manually or choose a crop.'; }
        }
        this.region = this.retained(reason); return;
      }
      this.transientFailures = 0; this.retryAt = -Infinity;
      if (!this.current || difference(submitted, this.current, this.region.crop) > .12) {
        this.region = this.retained('auto-scene-changed'); return;
      }
      // A valid "no educational region" response is also a completed analysis.
      // Do not send the same webcam/empty scene once per minute.
      if (reply.detection) this.reference = submitted;
      const crop = reply.detection?.crop;
      if (crop && validCrop(crop)) {
        this.reference = submitted; this.region = { crop, reason: reply.detection?.reason ?? 'auto-detected', review: false };
      } else this.region = this.retained(reply.detection?.reason ?? 'auto-no-region');
    }).catch(() => { if (!this.closed) { this.disabled = true; this.region = this.retained('auto-unavailable'); } })
      .finally(() => { this.inFlight = false; });
  }
  close() { this.closed = true; }
}
