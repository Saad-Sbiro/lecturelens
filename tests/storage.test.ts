import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm, writeFile, readdir } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { LocalStore } from '../src/adapters/storage/local-store';
import { ReplaySource } from '../src/adapters/replay/replay-source';
import { encodePacket } from '../src/adapters/storage/packet';

async function setup(t: { after(fn: () => Promise<void>): void }) {
  const directory = await mkdtemp(path.join(os.tmpdir(), 'lecturelens-test-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  return new LocalStore(directory);
}
const chunk = (sequence: number, atMs = sequence * 2000) => ({ sequence, atMs, bytes: Buffer.from(`chunk-${sequence}`), reason: 'replay-chunk' as const });

test('replay persists ordered chunks and reconstructs the exact byte stream after reopening', async t => {
  const store = await setup(t);
  const source = await ReplaySource.fromFile('fixtures/synthetic-session.json');
  const session = await source.start({ signal: new AbortController().signal, timeoutMs: 1000 });
  const id = await store.create('video/webm');
  const expected: Uint8Array[] = [];
  for await (const part of session.media) { expected.push(part.bytes); await store.append(id, part); }
  const reopened = new LocalStore(store.root);
  assert.equal((await reopened.list())[0].state, 'interrupted');
  const output = await reopened.recover(id);
  assert.deepEqual(await readFile(output), Buffer.concat(expected));
  assert.equal((await reopened.list())[0].chunks, 3);
});

test('atomic packets include metadata, tolerate an unfinished write, and keep every committed chunk', async t => {
  const store = await setup(t); const id = await store.create('video/webm');
  await store.append(id, chunk(0));
  await writeFile(path.join(store.root, id, '00000001.chunk.partial'), encodePacket(chunk(1)).subarray(0, 10));
  const reopened = new LocalStore(store.root);
  const summary = (await reopened.list())[0];
  assert.equal(summary.chunks, 1); assert.equal(summary.state, 'needs-review');
  assert.deepEqual(await readFile(await reopened.recover(id)), chunk(0).bytes);
  assert.ok((await readdir(path.join(store.root, id))).includes('00000001.chunk.partial'));
});

test('corrupted and missing packets never produce a silently successful export', async t => {
  const store = await setup(t); const id = await store.create('video/webm');
  await store.append(id, chunk(0)); await store.append(id, chunk(1));
  const filename = path.join(store.root, id, '00000001.chunk');
  const packet = await readFile(filename); packet[packet.length - 1] ^= 1; await writeFile(filename, packet);
  const reopened = new LocalStore(store.root);
  await assert.rejects(reopened.recover(id), /Integrity/);
  assert.ok((await reopened.list())[0].problems.some(problem => problem.startsWith('corrupt:')));
  await rm(path.join(store.root, id, '00000000.chunk'));
  await writeFile(filename, encodePacket(chunk(1)));
  await assert.rejects(reopened.recover(id), /Integrity/);
});

test('concurrent duplicate sequence, backwards clocks and closed-session writes are rejected', async t => {
  const store = await setup(t); const id = await store.create('video/webm');
  const results = await Promise.allSettled([store.append(id, chunk(0, 100)), store.append(id, chunk(0, 100))]);
  assert.equal(results.filter(item => item.status === 'fulfilled').length, 1);
  await assert.rejects(store.append(id, chunk(1, 99)), /Out-of-order/);
  await assert.rejects(store.recover(id), /End recording/);
  await assert.rejects(store.delete(id), /active recording/);
  await store.finish(id, 'user-stop');
  await assert.rejects(store.append(id, chunk(1, 200)), /not active/);
  assert.equal((await store.list())[0].state, 'finished');
});

test('storage rejects path traversal and does not delete unrelated files', async t => {
  const store = await setup(t);
  await writeFile(path.join(store.root, 'keep.txt'), 'keep');
  await assert.rejects(store.delete('../'), /Invalid session/);
  await assert.rejects(store.recover('../../outside'), /Invalid session/);
  assert.equal(await readFile(path.join(store.root, 'keep.txt'), 'utf8'), 'keep');
});

test('replay honors cancellation and validates fixture clocks', async () => {
  const source = await ReplaySource.fromFile('fixtures/synthetic-session.json');
  const controller = new AbortController(); controller.abort();
  await assert.rejects(source.start({ signal: controller.signal, timeoutMs: 1000 }), /cancelled/);
  const invalid = new ReplaySource({ schemaVersion: 1, kind: 'synthetic-persistence', chunks: [{ atMs: -1, hex: 'ff' }] });
  await assert.rejects(invalid.start({ signal: new AbortController().signal, timeoutMs: 1000 }), /Invalid fixture/);
});
