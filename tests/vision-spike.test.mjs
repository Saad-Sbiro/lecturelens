import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, rm, readFile, readdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { analyze, validate, pixels, model, config } from '../tools/vision/gemini.mjs';
const detection = { box: [200, 250, 800, 850], type: 'slide', isEducational: true };
test('normalized YX box converts against original dimensions with bounded margin', () => {
  assert.deepEqual(pixels(detection.box, 2000, 1000, 0), { x: 500, y: 200, width: 1200, height: 600 });
  assert.deepEqual(pixels(detection.box, 2000, 1000), { x: 482, y: 191, width: 1236, height: 618 });
  assert.deepEqual(pixels([0, 0, 1000, 1000], 2000, 1000), { x: 0, y: 0, width: 2000, height: 1000 });
  for (const box of [[0,0,0,1], [-1,0,100,100], [0,0,1001,100], [5,4,3,2], [0,'1',2,3]]) assert.throws(() => validate({ ...detection, box }));
  assert.equal(pixels(null, 2000, 1000), null);
  assert.deepEqual(validate({ box: null, type: 'none', isEducational: false }), { box: null, type: 'none', isEducational: false });
  assert.throws(() => validate({ ...detection, isEducational: false }));
});
test('request config, actual usage, local cache and errors never enable billing', async () => {
  const cacheDir = await mkdtemp(join(tmpdir(), 'lens-vision-'));
  try {
    let calls = 0;
    const options = { bytes: Buffer.from('fixture'), mime: 'image/png', cacheDir, key: 'fixture-key', allowUpload: true, freeTier: true,
      fetcher: async (url, init) => {
        calls++; assert.ok(url.endsWith(`${model}:generateContent`)); assert.ok(!url.includes('fixture-key'));
        const body = JSON.parse(init.body); assert.deepEqual(body.generationConfig, config);
        assert.equal(body.generationConfig.thinkingConfig.thinkingLevel, 'LOW');
        return new Response(JSON.stringify({ usageMetadata: { promptTokenCount: 601, candidatesTokenCount: 41, thoughtsTokenCount: 50, totalTokenCount: 692 }, candidates: [{ finishReason: 'STOP', content: { parts: [{ text: JSON.stringify(detection) }] } }] }));
      } };
    await assert.rejects(analyze({ ...options, allowUpload: false }), /allow-upload/); assert.equal(calls, 0);
    const first = await analyze(options); assert.equal(first.requestUsage.totalTokenCount, 692);
    const cached = await analyze({ ...options, key: undefined, allowUpload: false });
    assert.equal(calls, 1); assert.equal(cached.cacheHit, true); assert.equal(cached.requestUsage, null); assert.equal(cached.usageMetadata.totalTokenCount, 692);
    await analyze({ ...options, bytes: Buffer.from('changed') }); assert.equal(calls, 2);
    let failures = 0;
    await assert.rejects(analyze({ ...options, bytes: Buffer.from('quota'), fetcher: async () => { failures++; return new Response('', { status: 429, headers: { 'retry-after': '60' } }); } }), /rate limit/);
    assert.equal(failures, 1);
    await assert.rejects(analyze({ ...options, bytes: Buffer.from('bad'), fetcher: async () => new Response(JSON.stringify({ usageMetadata: { totalTokenCount: 80 }, candidates: [{ finishReason: 'STOP', content: { parts: [{ text: '{}' }] } }] })) }), /Invalid detection/);
    const attempts = await Promise.all((await readdir(cacheDir)).filter(n => n.endsWith('.attempt.json')).map(n => readFile(join(cacheDir,n), 'utf8')));
    assert.ok(attempts.some(a => JSON.parse(a).usageMetadata.totalTokenCount === 80));
    await assert.rejects(analyze({ ...options, bytes: Buffer.from('empty'), fetcher: async () => new Response('{}') }), /No complete detection/);
    assert.ok(!(await readFile(join(cacheDir, first.id + '.json'), 'utf8')).includes('fixture-key'));
  } finally { await rm(cacheDir, { recursive: true, force: true }); }
});
