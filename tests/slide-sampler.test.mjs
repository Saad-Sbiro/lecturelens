import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { build } from 'vite';
const built = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/slide-sampler.ts', name: 'Sampler', formats: ['iife'] } } });
const code = (Array.isArray(built) ? built[0] : built).output.find(item => item.type === 'chunk').code;

test('cloud crop follows resized and moved slide edges locally and deduplicates normalized content', async () => {
  let time = 0, page = 0, box = { x: 128, y: 72, w: 384, h: 216 };
  const calls = [];
  function canvas() {
    let area = [0, 0, 640, 360];
    const c = { width: 0, height: 0, toDataURL: () => 'data:image/jpeg;base64,/9j/2Q==', getContext: () => ({
      drawImage(_video, x, y, w, h) { area = [x, y, w, h]; },
      getImageData() {
        const data = new Uint8ClampedArray(c.width * c.height * 4);
        for (let y = 0; y < c.height; y++) for (let x = 0; x < c.width; x++) {
          const sx = area[0] + x * area[2] / c.width, sy = area[1] + y * area[3] / c.height;
          const nx = (sx - box.x) / box.w, ny = (sy - box.y) / box.h;
          let v = 70;
          if (nx >= 0 && nx < 1 && ny >= 0 && ny < 1) {
            v = 245;
            if (nx > .1 && nx < .7 && ny > .15 && ny < .8 && Math.floor(ny * 12) % 2 === 0) v = page ? 130 : 20;
          }
          const i = (y * c.width + x) * 4; data[i] = data[i + 1] = data[i + 2] = v; data[i + 3] = 255;
        }
        return { data };
      }
    }) }; return c;
  }
  const context = vm.createContext({ console, document: { createElement: canvas } }); vm.runInContext(code, context);
  const crop = { x: .2, y: .2, width: .6, height: .6 };
  const sampler = new context.Sampler.SlideSampler({ videoWidth: 640, videoHeight: 360, readyState: 2 }, crop,
    { call: async (op, data) => calls.push({ op, ...data }) }, 'test', () => time, e => { throw e; }, 'manual',
    () => ({ crop, reason: 'auto-detecting', review: true }));
  const settle = () => new Promise(resolve => setImmediate(resolve));
  for (time = 0; time <= 30000; time += 5000) {
    if (time === 10000) box = { x: 160, y: 90, w: 320, h: 180 };
    if (time === 20000) box = { x: 180, y: 100, w: 320, h: 180 };
    sampler.tick(); await settle();
    assert.ok(Math.abs(sampler.currentCrop.width - box.w / 640) < .004, 'tracks exact resized width before saving');
    assert.ok(Math.abs(sampler.currentCrop.x - box.x / 640) < .004, 'tracks position');
  }
  page = 1;
  for (time = 35000; time <= 40000; time += 5000) { sampler.tick(); await settle(); }
  await sampler.finish();
  assert.equal(calls.filter(c => c.op === 'slide').length, 2, 'moving/resizing is not a new slide; real content change is');
});
test('sampling uses a five-second clock, deduplicates stable frames and drains on finish', async () => {
  let time = 0; const calls = [];
  function canvas() {
    const c = { width: 0, height: 0, toDataURL: () => 'data:image/jpeg;base64,/9j/2Q==', getContext: () => ({ drawImage() {}, getImageData: () => ({ data: Uint8ClampedArray.from({ length: c.width * c.height * 4 }, (_, i) => Math.floor(i / 4) % 20 < 10 ? 200 : 40) }) }) };
    return c;
  }
  const context = vm.createContext({ document: { createElement: canvas }, console }); vm.runInContext(code, context);
  const sampler = new context.Sampler.SlideSampler({ videoWidth: 320, videoHeight: 180, readyState: 2 }, { x: 0, y: 0, width: 1, height: 1 }, { call: async (op, data) => calls.push({ op, ...data }) }, 'test', () => time, error => { throw error; });
  const settle = () => new Promise(resolve => setImmediate(resolve));
  sampler.tick(); await settle();
  time = 4999; sampler.tick(); await settle();
  assert.equal(calls.filter(c => c.reason === 'sampled-candidate').length, 1);
  time = 5000; sampler.tick(); await settle();
  time = 10000; sampler.tick(); await settle();
  assert.equal(calls.filter(c => c.reason === 'sampled-candidate').length, 3);
  assert.equal(calls.filter(c => c.op === 'slide').length, 1);
  await sampler.finish(); time = 15000; sampler.tick(); await settle();
  assert.equal(calls.filter(c => c.reason === 'sampled-candidate').length, 3);
});
