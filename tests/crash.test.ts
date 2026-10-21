import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fork } from 'node:child_process';
import { mkdtemp, rm, open } from 'node:fs/promises';
import { once } from 'node:events';
import os from 'node:os';
import path from 'node:path';
import { LocalStore } from '../src/adapters/storage/local-store';

test('two-hour timeline survives process kill; every acknowledged byte is recovered', { timeout: 180000 }, async t => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'lecturelens-crash-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  const count = 3600;
  const size = process.env.LECTURELENS_FULL_DISK_TEST === '1' ? 399000 : 256;
  const child = fork(path.join(__dirname, '../tools/crash-writer.js'), [root, String(count), String(size)], { silent: true });
  t.after(() => { if (!child.killed) child.kill('SIGKILL'); });
  const message = await new Promise<{ id: string }>((resolve, reject) => {
    child.once('message', value => resolve(value as { id: string }));
    child.once('error', reject);
    child.once('exit', code => reject(new Error(`Writer exited before acknowledgment: ${code}`)));
  });
  const exit = once(child, 'exit'); child.kill('SIGKILL'); await exit;
  const store = new LocalStore(root);
  const summary = (await store.list())[0];
  assert.equal(summary.state, 'interrupted');
  assert.equal(summary.chunks, count); assert.equal(summary.bytes, count * size);
  const file = await open(await store.recover(message.id), 'r');
  try {
    const buffer = Buffer.alloc(size);
    for (let sequence = 0; sequence < count; sequence++) {
      const result = await file.read(buffer, 0, size, sequence * size);
      assert.equal(result.bytesRead, size);
      assert.ok(buffer.every(value => value === sequence % 251));
    }
  } finally { await file.close(); }
  console.log(JSON.stringify({ simulatedHours: 2, packets: count, bytes: count * size, recovered: 'all acknowledged bytes', wallClockCapture: false }));
});
