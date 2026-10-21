import { fullCrop, type Crop } from './slides';
export type CropMode = 'auto' | 'manual';
export function overlap(a: Crop, b: Crop) {
  const intersection = Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x)) * Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y));
  return intersection / (a.width * a.height + b.width * b.height - intersection);
}
export class CropTracker {
  private proposed?: Crop;
  private confirmations = 0;
  constructor(public crop: Crop, public readonly mode: CropMode) {}
  observe(proposal: { crop: Crop; confidence: number }) {
    if (this.mode === 'manual') return { crop: this.crop, reason: 'manual-locked', review: false };
    if (proposal.confidence < 0.75) {
      this.proposed = undefined; this.confirmations = 0;
      return { crop: fullCrop, reason: 'region-uncertain', review: true };
    }
    if (overlap(proposal.crop, this.crop) > 0.92) {
      this.proposed = undefined; this.confirmations = 0;
      return { crop: this.crop, reason: 'region-tracked', review: false };
    }
    this.confirmations = this.proposed && overlap(proposal.crop, this.proposed) > 0.92 ? this.confirmations + 1 : 1;
    this.proposed = proposal.crop;
    if (this.confirmations < 2) return { crop: fullCrop, reason: 'layout-change-review', review: true };
    this.crop = proposal.crop; this.confirmations = 0; this.proposed = undefined;
    return { crop: this.crop, reason: 'region-relocated', review: false };
  }
}
export function assessContent(gray: Uint8Array, width: number, height: number) {
  let sum = 0, squares = 0, edges = 0, rows = 0;
  for (let y = 0; y < height; y++) {
    let rowEdges = 0;
    for (let x = 0; x < width; x++) {
      const v = gray[y * width + x]; sum += v; squares += v * v;
      if (x && Math.abs(v - gray[y * width + x - 1]) > 24) { edges++; rowEdges++; }
    }
    if (rowEdges >= 4) rows++;
  }
  const variance = squares / gray.length - (sum / gray.length) ** 2;
  if (variance < 4) return { action: 'reject' as const, reason: 'blank-frame' };
  const density = edges / gray.length;
  // Structure is evidence, not semantic proof. Diagrams/handwriting with little
  // text remain review candidates instead of being discarded.
  if (rows >= 6 && density >= 0.01 && density <= 0.3) return { action: 'keep' as const, reason: 'structured-content' };
  return { action: 'review' as const, reason: 'content-uncertain' };
}
