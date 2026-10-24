import { visualConfig as config } from '../../config/visual';
export type Crop = { x: number; y: number; width: number; height: number };
export const fullCrop: Crop = { x: 0, y: 0, width: 1, height: 1 };
export function validCrop(c: Crop): boolean {
  return [c.x, c.y, c.width, c.height].every(Number.isFinite) && c.x >= 0 && c.y >= 0 && c.width >= 0.05 && c.height >= 0.05 && c.x + c.width <= 1.001 && c.y + c.height <= 1.001;
}
export function difference(a: Uint8Array, b: Uint8Array, crop: Crop = fullCrop): number {
  if (a.length !== config.compareWidth * config.compareHeight || a.length !== b.length) throw new Error('Invalid comparison frame');
  let changed = 0, blocks = 0;
  for (let y = 0; y < config.compareHeight; y += config.block) for (let x = 0; x < config.compareWidth; x += config.block) {
    // Ignore surrounding chat/webcam content once the educational area is known.
    const left = Math.max(x, Math.ceil(crop.x * config.compareWidth)), top = Math.max(y, Math.ceil(crop.y * config.compareHeight));
    const right = Math.min(x + config.block, Math.floor((crop.x + crop.width) * config.compareWidth));
    const bottom = Math.min(y + config.block, Math.floor((crop.y + crop.height) * config.compareHeight));
    if (left >= right || top >= bottom) continue;
    let delta = 0;
    for (let yy = top; yy < bottom; yy++) for (let xx = left; xx < right; xx++) delta += Math.abs(a[yy * config.compareWidth + xx] - b[yy * config.compareWidth + xx]);
    if (delta / ((right - left) * (bottom - top)) > config.pixelDelta) changed++;
    blocks++;
  }
  return blocks ? changed / blocks : 0;
}
export class SlideIndex {
  private kept: Uint8Array[] = [];
  classify(frame: Uint8Array): { slide: number; reason: string; keep: boolean } {
    const match = this.kept.findIndex(prior => difference(prior, frame) < config.changedFraction);
    if (match >= 0) return { slide: match, reason: 'existing-slide-visit', keep: false };
    if (this.kept.length >= config.maxSlides) return { slide: -1, reason: 'slide-cap-review-needed', keep: false };
    this.kept.push(frame.slice());
    return { slide: this.kept.length - 1, reason: 'new-slide', keep: true };
  }
}
// A deliberately conservative geometric proposal, never a claim of semantic recognition.
// Long rectangular borders can reveal a slide nested inside an editor/video frame.
export function suggestCrop(gray: Uint8Array, width: number, height: number): { crop: Crop; confidence: number } {
  const rows = new Float64Array(height), cols = new Float64Array(width);
  const edge = (x: number, y: number) => Math.max(Math.abs(gray[y * width + x] - gray[y * width + Math.max(0, x - 1)]), Math.abs(gray[y * width + x] - gray[Math.max(0, y - 1) * width + x]));
  for (let y = 1; y < height; y++) for (let x = 1; x < width; x++) { const e = edge(x, y); if (e > 22) { rows[y]++; cols[x]++; } }
  const peaks = (values: Float64Array) => {
    const selected: number[] = [];
    for (const i of Array.from(values.keys()).sort((a, b) => values[b] - values[a])) {
      if (values[i] < 8) break;
      if (selected.every(j => Math.abs(j - i) > 3)) selected.push(i);
      if (selected.length === 10) break;
    }
    return selected.sort((a, b) => a - b);
  };
  const xs = peaks(cols), ys = peaks(rows); let best = 0, crop = fullCrop;
  for (const left of xs) for (const right of xs) for (const top of ys) for (const bottom of ys) {
    const w = right - left, h = bottom - top, area = w * h / (width * height), ratio = w / h;
    if (w <= 0 || h <= 0 || area < 0.12 || area > 0.92 || ratio < 1.15 || ratio > 2.1) continue;
    let support = 0;
    for (let x = left; x <= right; x++) { if (edge(x, top) > 22) support++; if (edge(x, bottom) > 22) support++; }
    for (let y = top; y <= bottom; y++) { if (edge(left, y) > 22) support++; if (edge(right, y) > 22) support++; }
    const score = support / (2 * (w + h + 2));
    // Prefer a well-defined inner rectangle over a large but incomplete page border.
    if (score > best) { best = score; crop = { x: left / width, y: top / height, width: w / width, height: h / height }; }
  }
  return best >= 0.65 ? { crop, confidence: best } : { crop: fullCrop, confidence: 0 };
}
