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

test('503 retries back off, recover automatically, and stop after three retries', async () => {
  let calls = 0;
  const auto = new context.Auto.AutoRegion(async () => {
    calls++; return { error: 'temporary 503', retryable: true, disabled: false };
  });
  auto.observe(frame(0), () => 'fixture', 0);
  for (const time of [2000, 68000, 200000, 450000]) {
    auto.observe(frame(0), () => 'fixture', time); await settle();
    const prior = calls;
    auto.observe(frame(0), () => 'fixture', time + 1000); await settle();
    assert.equal(calls, prior, 'no rapid retry');
  }
  auto.observe(frame(0), () => 'fixture', 900000); await settle();
  assert.equal(calls, 4);
  assert.match(auto.region.reason, /exhausted/);
  let attempts = 0;
  const recovered = new context.Auto.AutoRegion(async () => ++attempts === 1
    ? { error: 'temporary 503', retryable: true, disabled: false } : { detection: { crop } });
  for (const time of [0, 2000, 68000]) { recovered.observe(frame(0), () => 'fixture', time); await settle(); }
  assert.equal(attempts, 2); assert.equal(recovered.region.crop.width, .6);
});

test('ten-minute recording keeps three cropped slides despite sidebar updates, slow vision and quota failure', async () => {
  const samplerBuild = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/slide-sampler.ts', name: 'Sampler', formats: ['iife'] } } });
  let time = 0, scene = frame(0), queries = 0, reply;
  const calls = [];
  function canvas() {
    let area = [0, 0, 160, 90];
    const c = { width: 0, height: 0, toDataURL: () => 'data:image/jpeg;base64,/9j/2Q==', getContext: () => ({
      drawImage(_video, x, y, w, h) { area = [x, y, w, h]; },
      getImageData() {
        const data = new Uint8ClampedArray(c.width * c.height * 4);
        for (let y = 0; y < c.height; y++) for (let x = 0; x < c.width; x++) {
          const value = scene[Math.min(89, Math.floor(area[1] + y * area[3] / c.height)) * 160 + Math.min(159, Math.floor(area[0] + x * area[2] / c.width))];
          const i = (y * c.width + x) * 4; data[i] = data[i + 1] = data[i + 2] = value; data[i + 3] = 255;
        }
        return { data };
      }
    }) }; return c;
  }
  const sandbox = vm.createContext({ console, document: { createElement: canvas } });
  vm.runInContext((Array.isArray(built) ? built[0] : built).output.find(x => x.type === 'chunk').code, sandbox);
  vm.runInContext((Array.isArray(samplerBuild) ? samplerBuild[0] : samplerBuild).output.find(x => x.type === 'chunk').code, sandbox);
  const auto = new sandbox.Auto.AutoRegion(() => {
    queries++;
    return queries === 1 ? Promise.resolve({ detection: { crop } }) : new Promise(resolve => reply = resolve);
  });
  function update() {
    scene = frame(Math.floor(time / 10000) % 2 ? 240 : 20);
    const slide = time < 200000 ? 0 : time < 400000 ? 1 : 2;
    for (let y = 18; y < 72; y++) for (let x = 32; x < 128; x++) scene[y * 160 + x] = x % 20 < 10 ? 40 + slide * 60 : 240;
    auto.observe(scene, () => 'fixture', time);
  }
  update(); time = 2000; update(); await settle();
  const sampler = new sandbox.Sampler.SlideSampler({ videoWidth: 160, videoHeight: 90, readyState: 2 }, crop,
    { call: async (op, data) => calls.push({ op, ...data }) }, 'fixture', () => time, e => { throw e; }, 'manual', () => auto.region);
  for (time = 3000; time <= 600000; time += 1000) {
    update(); await settle();
    if (time === 190000) assert.equal(queries, 1, 'sidebar changes never upload');
    if (time === 350000) { reply({ disabled: true, error: 'quota' }); await settle(); }
    sampler.tick(); await settle();
    assert.equal(auto.region.crop.width, .6, 'last good crop survives reanalysis');
  }
  await sampler.finish();
  const slides = calls.filter(c => c.op === 'slide');
  assert.equal(slides.length, 3, 'only the three educational pages are saved');
  assert.ok(slides.every(s => Object.keys(crop).every(k => Math.abs(s.crop[k] - crop[k]) < .015)), JSON.stringify(slides.map(s => s.crop)));
  assert.equal(queries, 2, 'quota stops requests without removing crop');
});

test('confirmed relocation replaces the retained crop, but empty and invalid replies do not', async () => {
  const relocated = { x: .1, y: .1, width: .5, height: .5 };
  const replies = [{ detection: { crop } }, { detection: { crop: relocated } }, { detection: { reason: 'no-region' } }, { detection: { crop: { ...crop, x: 2 } } }];
  const auto = new context.Auto.AutoRegion(async () => replies.shift());
  for (let page = 0; page < 4; page++) {
    auto.observe(frame(page * 60), () => 'fixture', page * 100000);
    auto.observe(frame(page * 60), () => 'fixture', page * 100000 + 2000); await settle();
    assert.equal(auto.region.crop.width, page ? .5 : .6);
  }
});
test('cloud detection is asynchronous, stale results ignored, unchanged scene cached and manual close wins', async () => {
  let resolve, calls = 0;
  const auto = new context.Auto.AutoRegion(() => { calls++; return new Promise(r => resolve = r); });
  auto.observe(frame(0), () => 'image', 0); auto.observe(frame(0), () => 'image', 2000); await settle();
  assert.equal(auto.region.crop.width, 1);
  auto.observe(frame(255), () => 'image', 3000);
  resolve({ detection: { crop, reason: 'gemini-slide' } }); await settle();
  assert.equal(auto.region.crop.width, 1, 'stale region never applied');
  assert.equal(auto.region.reason, 'auto-scene-changed', 'completed stale request must not remain detecting');
  auto.observe(frame(255), () => 'image', 62000); await settle();
  resolve({ detection: { crop } }); await settle(); assert.equal(auto.region.crop.width, .6);
  auto.observe(frame(255), () => 'image', 120000); await settle(); assert.equal(calls, 2);
  auto.observe(frame(0), () => 'image', 180000); auto.observe(frame(0), () => 'image', 182000); await settle(); assert.equal(auto.region.crop.width, .6);
  auto.close(); resolve({ detection: { crop: { ...crop, width: .5 } } }); await settle(); assert.equal(auto.region.crop.width, .6);
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
