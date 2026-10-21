import path from 'node:path';
import { ReplaySource } from '../src/adapters/replay/replay-source';
import { LocalStore } from '../src/adapters/storage/local-store';

async function main(): Promise<void> {
  const fixture = process.argv[2] ?? 'fixtures/synthetic-session.json';
  const source = await ReplaySource.fromFile(fixture);
  const session = await source.start({ signal: new AbortController().signal, timeoutMs: 30_000 });
  const store = new LocalStore(path.resolve('artifacts/replay'));
  const id = await store.create('video/webm');
  for await (const chunk of session.media) await store.append(id, chunk);
  for (const event of session.events) await store.event(id, event.type, event.atMs, event.reason);
  await store.finish(id, 'synthetic-replay-complete');
  const result = (await store.list()).find(item => item.id === id);
  console.log(JSON.stringify({ fixture, result, mediaValidity: 'not-media; storage fixture only' }, null, 2));
}
void main().catch(error => { console.error(error); process.exitCode = 1; });
