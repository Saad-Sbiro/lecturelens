import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { build } from 'vite';
const built = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/auto-region.ts', name: 'Auto', formats: ['iife'] } } });
const context = vm.createContext({ console });
vm.runInContext((Array.isArray(built) ? built[0] : built).output.find(x => x.type === 'chunk').code, context);
const settle = () => new Promise(resolve => setImmediate(resolve));
const frame = value => new Uint8Array(160 * 90).fill(value);
const crop = { x: .2, y: .2, width: .6, height: .6 };
test('cloud detection is asynchronous, stale results ignored, unchanged scene cached and manual close wins', async () => {
  let resolve, calls = 0;
  const auto = new context.Auto.AutoRegion(() => { calls++; return new Promise(r => resolve = r); });
  auto.observe(frame(0), () => 'image', 0); auto.observe(frame(0), () => 'image', 2000); await settle();
  assert.equal(auto.region.crop.width, 1);
  auto.observe(frame(255), () => 'image', 3000);
  resolve({ detection: { crop, reason: 'gemini-slide' } }); await settle();
  assert.equal(auto.region.crop.width, 1, 'stale region never applied');
  auto.observe(frame(255), () => 'image', 62000); await settle();
  resolve({ detection: { crop } }); await settle(); assert.equal(auto.region.crop.width, .6);
  auto.observe(frame(255), () => 'image', 120000); await settle(); assert.equal(calls, 2);
  auto.observe(frame(0), () => 'image', 180000); auto.observe(frame(0), () => 'image', 182000); await settle(); assert.equal(auto.region.crop.width, 1);
  auto.close(); resolve({ detection: { crop } }); await settle(); assert.equal(auto.region.crop.width, 1);
});
test('quota failures stop further requests and keep full frame; invalid regions never crop', async () => {
  let calls = 0;
  const auto = new context.Auto.AutoRegion(async () => { calls++; return { disabled: true, error: 'quota' }; });
  auto.observe(frame(0), () => 'image', 0); auto.observe(frame(0), () => 'image', 2000); await settle();
  auto.observe(frame(255), () => 'image', 60000); await settle();
  assert.equal(calls, 1); assert.equal(auto.region.crop.width, 1);
  const invalid = new context.Auto.AutoRegion(async () => ({ detection: { crop: { ...crop, x: 2 } } }));
  invalid.observe(frame(0), () => 'image', 0); invalid.observe(frame(0), () => 'image', 2000); await settle(); assert.equal(invalid.region.crop.width, 1);
});
test('cursor motion and unchanged non-educational scenes do not upload; stable page changes do', async () => {
  let calls = 0;
  const auto = new context.Auto.AutoRegion(async () => { calls++; return { detection: { crop: null, reason: 'no-educational-region' } }; });
  auto.observe(frame(0), () => 'image', 0); await settle(); assert.equal(calls, 0);
  auto.observe(frame(0), () => 'image', 2000); await settle(); assert.equal(calls, 1);
  for (let i = 1; i <= 5; i++) {
    const cursor = frame(0);
    for (let y = 20; y < 23; y++) for (let x = i * 10; x < i * 10 + 3; x++) cursor[y * 160 + x] = 255;
    auto.observe(cursor, () => 'image', 60000 + i * 1000); await settle();
  }
  assert.equal(calls, 1, 'cursor motion never uploads');
  auto.observe(frame(100), () => 'image', 120000);
  auto.observe(frame(180), () => 'image', 121000);
  auto.observe(frame(255), () => 'image', 122000); await settle(); assert.equal(calls, 1, 'transition frames never upload');
  auto.observe(frame(255), () => 'image', 124000); await settle(); assert.equal(calls, 2, 'settled changed page uploaded once');
  auto.observe(frame(255), () => 'image', 240000); await settle(); assert.equal(calls, 2);
});
