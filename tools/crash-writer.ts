import { LocalStore } from '../src/adapters/storage/local-store';

async function main(): Promise<void> {
  const root = process.argv[2];
  const count = Number(process.argv[3] ?? 3600);
  const size = Number(process.argv[4] ?? 256);
  const store = new LocalStore(root);
  const id = await store.create('video/webm');
  for (let sequence = 0; sequence < count; sequence++) {
    const bytes = Buffer.alloc(size, sequence % 251);
    await store.append(id, { sequence, atMs: (sequence + 1) * 2000, bytes, reason: 'replay-chunk' });
  }
  process.send?.({ id, count, size });
  // Parent kills this process before any graceful finish/cleanup can run.
  setInterval(() => undefined, 1000);
}
void main().catch(error => { console.error(error); process.exit(1); });
