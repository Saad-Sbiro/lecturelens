import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { once } from 'node:events';

function client(root) {
  const env = { ...globalThis.process.env }; delete env.DOTNET_ROOT;
  const process = spawn(resolve('dist/native-host/LectureLens.NativeHost.exe'), ['--test-root', root], { env });
  let buffer = Buffer.alloc(0), serial = 0;
  const waiting = new Map();
  process.stdout.on('data', chunk => {
    buffer = Buffer.concat([buffer, chunk]);
    while (buffer.length >= 4 && buffer.length >= 4 + buffer.readUInt32LE()) {
      const size = buffer.readUInt32LE(); const reply = JSON.parse(buffer.subarray(4, size + 4)); buffer = buffer.subarray(size + 4);
      const task = waiting.get(reply.requestId); waiting.delete(reply.requestId);
      if (task) { clearTimeout(task.timer); reply.ok ? task.resolve(reply.value) : task.reject(new Error(reply.error)); }
    }
  });
  process.stderr.on('data', data => { /* stdout must contain framed JSON only */ });
  const exited = once(process, 'exit');
  return {
    process,
    call(op, data = {}) { return new Promise((resolve, reject) => {
      const requestId = String(++serial); const payload = Buffer.from(JSON.stringify({ requestId, op, ...data }));
      const header = Buffer.alloc(4); header.writeUInt32LE(payload.length);
      const timer = setTimeout(() => { waiting.delete(requestId); reject(new Error('Host response timeout')); }, 10000);
      waiting.set(requestId, { resolve, reject, timer }); process.stdin.write(Buffer.concat([header, payload]));
    }); },
    async close(kill = false) { if (kill) process.kill(); else process.stdin.end(); await exited; },
  };
}
test('actual C# host: consent, ordered durable writes, process kill, recovery and corruption', { timeout: 30000 }, async () => {
  const root = await mkdtemp(join(tmpdir(), 'lecturelens-native-'));
  let host = client(root);
  try {
    assert.equal((await host.call('hello')).version, 1);
    await assert.rejects(host.call('create', { consent: false, mime: 'video/webm' }));
    const { id } = await host.call('create', { consent: true, mime: 'audio/webm;codecs=opus' });
    const data = Buffer.alloc(262144, 42);
    await host.call('append', { id, sequence: 0, atMs: 0, data: data.toString('base64') });
    await host.call('slide', { id, slide: 0, atMs: 0, crop: { x: 0, y: 0, width: 1, height: 1 }, reason: 'synthetic-crash-test', data: '/9j/2Q==' });
    await assert.rejects(host.call('append', { id, sequence: 0, atMs: 1, data: 'AA==' }));
    await assert.rejects(host.call('recover', { id }));
    const second = client(root);
    try { await assert.rejects(second.call('create', { consent: true, mime: 'video/webm' })); } finally { await second.close(); }
    await host.close(true); host = client(root);
    assert.equal((await host.call('list'))[0].state, 'interrupted');
    const file = await host.call('recover', { id });
    assert.ok(file.endsWith('audio.webm'));
    assert.deepEqual(await readFile(join(root, id, 'slides', '00000.jpg')), Buffer.from('/9j/2Q==', 'base64'));
    assert.deepEqual(await readFile(file), data);
    await assert.rejects(host.call('recover', { id: '../outside' }));
    const packet = join(root, id, '00000000.chunk'); const bytes = await readFile(packet); bytes[bytes.length - 1] ^= 1; await writeFile(packet, bytes);
    await assert.rejects(host.call('recover', { id }));
    assert.equal((await host.call('list'))[0].state, 'needs-review');
    assert.deepEqual(await readFile(file), data, 'previous good export is preserved after corruption');
  } finally { await host.close(); await rm(root, { recursive: true, force: true }); }
});
test('C# host finalizes and exports, rejects sequence gaps and oversized packets', { timeout: 30000 }, async () => {
  const root = await mkdtemp(join(tmpdir(), 'lecturelens-native-')); const host = client(root);
  try {
    const { id } = await host.call('create', { consent: true, mime: 'video/webm' });
    await assert.rejects(host.call('append', { id, sequence: 2, atMs: 0, data: 'AA==' }));
    await assert.rejects(host.call('append', { id, sequence: 0, atMs: 0, data: Buffer.alloc(524289).toString('base64') }));
    await host.call('append', { id, sequence: 0, atMs: 1, data: 'AQID' });
    await host.call('slide', { id, slide: 0, atMs: 1, crop: { x: 0, y: 0, width: 1, height: 1 }, reason: 'new-slide:stable', data: '/9j/2Q==' });
    await host.call('visualEvent', { id, slide: 0, atMs: 1, crop: { x: 0, y: 0, width: 1, height: 1 }, reason: 'new-slide:stable' });
    await host.call('event', { id, type: 'ended', atMs: 2, reason: 'synthetic-test' });
    const result = await host.call('finish', { id, reason: 'user-stop' });
    assert.deepEqual(await readFile(result.path), Buffer.from([1, 2, 3]));
    assert.deepEqual(await readFile(join(root, id, 'slides', '00000.jpg')), Buffer.from('/9j/2Q==', 'base64'));
    assert.match(await readFile(join(root, id, 'slides', 'index.html'), 'utf8'), /00000.jpg/);
    assert.equal((await host.call('list'))[0].state, 'finished');
  } finally { await host.close(); await rm(root, { recursive: true, force: true }); }
});


test('shared terms acceptance persists across host launches and rejects stale or implicit acceptance', async () => {
  const root = await mkdtemp(join(tmpdir(), 'lecturelens-terms-')); let host = client(root);
  try {
    const status = await host.call('termsStatus'); assert.equal(status.accepted, false);
    await assert.rejects(host.call('acceptTerms', {version:status.version,accepted:false}));
    await assert.rejects(host.call('acceptTerms', {version:'old',accepted:true}));
    assert.equal((await host.call('termsStatus')).accepted, false);
    assert.equal((await host.call('acceptTerms', {version:status.version,accepted:true})).accepted, true);
    await host.close(); host = client(root);
    assert.equal((await host.call('termsStatus')).accepted, true);
    const saved = JSON.parse(await readFile(join(root,'terms-acceptance.json'),'utf8'));
    assert.equal(saved.Version,status.version); assert.ok(Date.parse(saved.AcceptedAt));
    await writeFile(join(root,'terms-acceptance.json'), '{broken');
    assert.equal((await host.call('termsStatus')).accepted, false);
  } finally {await host.close();await rm(root,{recursive:true,force:true});}
});
