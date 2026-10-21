import { test } from 'node:test';
import assert from 'node:assert/strict';
import { ChunkQueue } from '../src/domain/chunk-queue';

test('slow consumer is bounded and failure is reported after committed queue items drain', async () => {
  const queue = new ChunkQueue(4);
  queue.push({ sequence: 0, atMs: 0, bytes: new Uint8Array(4), reason: 'replay-chunk' });
  assert.throws(() => queue.push({ sequence: 1, atMs: 1, bytes: new Uint8Array(1), reason: 'replay-chunk' }), /Disk cannot keep up/);
  queue.end(new Error('stop-with-reason'));
  const iterator = queue[Symbol.asyncIterator]();
  assert.equal((await iterator.next()).value.sequence, 0);
  await assert.rejects(iterator.next(), /stop-with-reason/);
});

test('waiting consumer wakes on data and on normal stop', async () => {
  const queue = new ChunkQueue(4); const iterator = queue[Symbol.asyncIterator]();
  const pending = iterator.next();
  queue.push({ sequence: 0, atMs: 0, bytes: new Uint8Array(1), reason: 'replay-chunk' });
  assert.equal((await pending).value.sequence, 0);
  const ended = iterator.next(); queue.end(); assert.equal((await ended).done, true);
});
