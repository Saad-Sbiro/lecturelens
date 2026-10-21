import { test } from 'node:test';
import assert from 'node:assert/strict';
import { difference, SlideIndex, suggestCrop, validCrop } from '../src/domain/slides';

test('slide comparison ignores a cursor, keeps changed content, and recognizes a return', () => {
  const first = new Uint8Array(160 * 90).fill(220), cursor = first.slice(), next = first.slice();
  cursor[100] = 0;
  for (let y = 30; y < 60; y++) for (let x = 40; x < 120; x++) next[y * 160 + x] = 40;
  assert.equal(difference(first, cursor), 0);
  const index = new SlideIndex(); assert.equal(index.classify(first).keep, true);
  assert.equal(index.classify(cursor).keep, false);
  assert.equal(index.classify(next).keep, true);
  assert.equal(index.classify(first).slide, 0);
});
test('crop proposal finds an isolated slide rectangle and falls back on ambiguous blank content', () => {
  const w = 320, h = 180, frame = new Uint8Array(w * h).fill(240);
  for (let y = 40; y < 140; y++) for (let x = 100; x < 280; x++) frame[y * w + x] = 50;
  const found = suggestCrop(frame, w, h);
  assert.ok(found.confidence >= 0.65);
  assert.ok(Math.abs(found.crop.x - 100 / w) < 0.02);
  assert.ok(Math.abs(found.crop.width - 180 / w) < 0.02);
  assert.equal(suggestCrop(new Uint8Array(w * h).fill(240), w, h).confidence, 0);
  assert.equal(validCrop({ x: 0.9, y: 0, width: 0.3, height: 1 }), false);
});
