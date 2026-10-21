import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CropTracker, assessContent } from '../src/domain/visual-policy';
const first = { x: 0.2, y: 0.2, width: 0.5, height: 0.5 };
const moved = { x: 0.4, y: 0.1, width: 0.5, height: 0.5 };
test('auto crop changes only after consistent observations and preserves uncertain layouts for review', () => {
  const tracker = new CropTracker(first, 'auto');
  assert.equal(tracker.observe({ crop: moved, confidence: 0.95 }).review, true);
  assert.equal(tracker.observe({ crop: moved, confidence: 0.95 }).reason, 'region-relocated');
  assert.deepEqual(tracker.crop, moved);
  assert.equal(tracker.observe({ crop: first, confidence: 0.2 }).reason, 'region-uncertain');
  assert.deepEqual(tracker.crop, moved, 'weak evidence must not overwrite the tracked region');
});
test('manual crop is never replaced by automatic geometry', () => {
  const tracker = new CropTracker(first, 'manual');
  for (let i = 0; i < 5; i++) assert.deepEqual(tracker.observe({ crop: moved, confidence: 1 }).crop, first);
});
test('blank screens are rejected, structured content is kept, ambiguous content remains reviewable', () => {
  assert.equal(assessContent(new Uint8Array(160 * 90).fill(240), 160, 90).action, 'reject');
  const textLike = Uint8Array.from({ length: 160 * 90 }, (_, i) => i % 20 < 10 ? 220 : 40);
  assert.equal(assessContent(textLike, 160, 90).action, 'keep');
  const diagram = new Uint8Array(160 * 90).fill(200); diagram.fill(0, 100, 300);
  assert.equal(assessContent(diagram, 160, 90).action, 'review');
});
