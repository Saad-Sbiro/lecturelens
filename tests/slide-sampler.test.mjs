import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { build } from 'vite';
const built = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false, lib: { entry: 'src/extension/slide-sampler.ts', name: 'Sampler', formats: ['iife'] } } });
const code = (Array.isArray(built) ? built[0] : built).output.find(item => item.type === 'chunk').code;
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
