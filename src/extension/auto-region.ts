import { fullCrop, validCrop, difference, type Crop } from '../domain/slides';
type Region = { crop: Crop; reason: string; review: boolean };
type Reply = { detection?: { crop?: Crop; reason?: string }; error?: string; disabled?: boolean };
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
  constructor(private query: (data: string) => Promise<Reply>) {}
  observe(signature: Uint8Array, image: () => string, now: number) {
    if (this.closed) return;
    this.current = signature;
    // Require a settled scene, not an animation/scroll frame. Tiny pointer changes
    // are below the block-difference threshold and do not restart this timer.
    if (!this.stableFrame || difference(this.stableFrame, signature) > .025) {
      this.stableFrame = signature; this.stableSince = now;
    }
    if (this.reference && difference(this.reference, signature) > .12) {
      this.region = { crop: fullCrop, reason: 'auto-scene-changed', review: true }; this.reference = undefined;
    }
    if (this.calls >= 10 && !this.inFlight && !this.reference && !this.disabled) this.region = { crop: fullCrop, reason: 'auto-limit', review: true };
    if (this.inFlight || this.disabled || this.calls >= 10 || now - this.last < 60000 || this.reference || now - this.stableSince < 2000) return;
    this.last = now; this.calls++; this.inFlight = true;
    this.region = { crop: fullCrop, reason: 'auto-detecting', review: true };
    const submitted = signature;
    void Promise.resolve().then(() => this.query(image())).then(reply => {
      if (this.closed) return;
      this.disabled = reply.disabled === true;
      if (!this.current || difference(submitted, this.current) > .12) return;
      // A valid "no educational region" response is also a completed analysis.
      // Do not send the same webcam/empty scene once per minute.
      if (reply.detection) this.reference = submitted;
      const crop = reply.detection?.crop;
      if (crop && validCrop(crop)) {
        this.reference = submitted; this.region = { crop, reason: reply.detection?.reason ?? 'auto-detected', review: false };
      } else this.region = { crop: fullCrop, reason: reply.error ?? reply.detection?.reason ?? 'auto-no-region', review: true };
    }).catch(() => { if (!this.closed) { this.disabled = true; this.region = { crop: fullCrop, reason: 'auto-unavailable', review: true }; } })
      .finally(() => { this.inFlight = false; });
  }
  close() { this.closed = true; }
}
