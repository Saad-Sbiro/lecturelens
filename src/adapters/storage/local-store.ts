import { mkdir, open, readFile, readdir, rename, rm, statfs } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';
import path from 'node:path';
import { CapabilityError, type MediaChunk, type AdapterMetadata } from '../../ports/capture';
import type { Storage } from '../../ports/storage';
import type { SessionSummary } from '../../ports/desktop';
import { decodePacket, encodePacket } from './packet';

const validId = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
export const allowedMimeTypes = ['video/webm;codecs=vp8,opus', 'video/webm;codecs=vp9,opus', 'video/webm'];

async function durableWrite(filename: string, bytes: Uint8Array): Promise<void> {
  const temporary = `${filename}.partial`;
  const handle = await open(temporary, 'wx');
  try { await handle.writeFile(bytes); await handle.sync(); } finally { await handle.close(); }
  await rename(temporary, filename);
}

export class LocalStore implements Storage {
  readonly metadata: AdapterMetadata = {
    capability: 'Storage', runsOn: 'device', costTier: 'free', dataUse: 'private', requiresKey: false,
    status: 'experimental', limits: { maxChunkBytes: 32 * 1024 * 1024 }, supportedLanguages: [], mode: 'streaming',
  };
  private queues = new Map<string, Promise<unknown>>();
  private next = new Map<string, { sequence: number; atMs: number }>();
  constructor(readonly root: string) {}

  private directory(id: string): string {
    if (!validId.test(id)) throw new CapabilityError('BadInput', 'Invalid session identifier');
    return path.join(this.root, id);
  }
  private serial<T>(id: string, operation: () => Promise<T>): Promise<T> {
    const result = (this.queues.get(id) ?? Promise.resolve()).catch(() => undefined).then(operation);
    this.queues.set(id, result);
    void result.finally(() => { if (this.queues.get(id) === result) this.queues.delete(id); }).catch(() => undefined);
    return result;
  }
  async requestPersistence(): Promise<boolean> { await mkdir(this.root, { recursive: true }); return true; }
  abandon(id: string): void { this.next.delete(id); }
  async create(mimeType: string): Promise<string> {
    if (!allowedMimeTypes.includes(mimeType)) throw new CapabilityError('Unsupported', 'Unsupported media container');
    await this.requestPersistence();
    const id = randomUUID();
    const directory = this.directory(id);
    await mkdir(directory);
    await durableWrite(path.join(directory, 'manifest.json'), Buffer.from(JSON.stringify({
      schemaVersion: 1, ownerId: 'local', id, mimeType, createdAt: new Date().toISOString(), state: 'recording',
    })));
    this.next.set(id, { sequence: 0, atMs: 0 });
    return id;
  }
  async append(id: string, chunk: MediaChunk): Promise<void> {
    return this.serial(id, async () => {
      const directory = this.directory(id);
      const expected = this.next.get(id);
      // Crash recovery never resumes the old encoder/container. Start a new session instead.
      if (!expected) throw new CapabilityError('BadInput', 'Session is not active in this process');
      if (chunk.sequence !== expected.sequence || !Number.isFinite(chunk.atMs) || chunk.atMs < expected.atMs ||
          !chunk.bytes.byteLength || chunk.bytes.byteLength > Number(this.metadata.limits.maxChunkBytes)) {
        throw new CapabilityError('BadInput', 'Out-of-order, empty or oversized chunk');
      }
      const filename = `${String(chunk.sequence).padStart(8, '0')}.chunk`;
      await durableWrite(path.join(directory, filename), encodePacket(chunk));
      this.next.set(id, { sequence: chunk.sequence + 1, atMs: chunk.atMs });
    });
  }
  async event(id: string, type: string, atMs: number, reason: string): Promise<void> {
    return this.serial(id, async () => {
      if (!this.next.has(id)) throw new CapabilityError('BadInput', 'Session is not active');
      if (!['started', 'paused', 'resumed', 'sourceChanged', 'ended', 'diagnostic'].includes(type) ||
          !Number.isFinite(atMs) || atMs < 0 || typeof reason !== 'string' || reason.length > 1000) {
        throw new CapabilityError('BadInput', 'Invalid timeline event');
      }
      const handle = await open(path.join(this.directory(id), 'events.ndjson'), 'a');
      try {
        await handle.writeFile(JSON.stringify({ schemaVersion: 1, ownerId: 'local', type, atMs, reason }) + '\n');
        await handle.sync();
      } finally { await handle.close(); }
    });
  }
  async finish(id: string, reason: string): Promise<void> {
    return this.serial(id, async () => {
      if (!this.next.has(id)) throw new CapabilityError('BadInput', 'Session is not active');
      await durableWrite(path.join(this.directory(id), 'finished.json'), Buffer.from(JSON.stringify({ reason, finishedAt: new Date().toISOString() })));
      this.next.delete(id);
    });
  }
  private async scan(id: string, onChunk?: (chunk: MediaChunk) => Promise<void>): Promise<SessionSummary> {
    const directory = this.directory(id);
    const manifest = JSON.parse(await readFile(path.join(directory, 'manifest.json'), 'utf8'));
    const files = (await readdir(directory)).sort();
    const problems = files.filter(f => f.endsWith('.partial')).map(f => `uncommitted:${f}`);
    let chunks = 0;
    let bytes = 0;
    let atMs = 0;
    for (const filename of files.filter(f => /^\d{8}\.chunk$/.test(f))) {
      try {
        const chunk = decodePacket(await readFile(path.join(directory, filename)));
        if (chunk.sequence !== chunks || Number(filename.slice(0, 8)) !== chunks || chunk.atMs < atMs) {
          problems.push(`gap-or-order:${filename}`); break;
        }
        await onChunk?.(chunk);
        chunks++;
        bytes += chunk.bytes.byteLength;
        atMs = chunk.atMs;
      } catch (error) {
        if (onChunk && !(error instanceof CapabilityError) && !(error instanceof SyntaxError)) throw error;
        problems.push(`corrupt:${filename}`); break;
      }
    }
    const completion = files.includes('finished.json') ? JSON.parse(await readFile(path.join(directory, 'finished.json'), 'utf8')) : undefined;
    if (completion?.reason === 'capture-or-storage-error' || completion?.reason === 'start-failed') problems.push(completion.reason);
    return { id, state: problems.length ? 'needs-review' : completion ? 'finished' : 'interrupted', chunks, bytes, createdAt: manifest.createdAt, problems };
  }
  async list(): Promise<SessionSummary[]> {
    await this.requestPersistence();
    const results: SessionSummary[] = [];
    for (const entry of await readdir(this.root, { withFileTypes: true })) {
      if (!entry.isDirectory() || !validId.test(entry.name)) continue;
      try { results.push(await this.scan(entry.name)); }
      catch { results.push({ id: entry.name, state: 'needs-review', chunks: 0, bytes: 0, createdAt: '', problems: ['unreadable-manifest'] }); }
    }
    return results.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }
  async recover(id: string): Promise<string> {
    return this.serial(id, async () => {
      if (this.next.has(id)) throw new CapabilityError('BadInput', 'End recording before export');
      const directory = this.directory(id);
      const target = path.join(directory, 'recording.webm');
      const temporary = `${target}.partial`;
      const handle = await open(temporary, 'w');
      let summary: SessionSummary;
      try {
        summary = await this.scan(id, async chunk => { await handle.writeFile(chunk.bytes); });
        await handle.sync();
      } finally { await handle.close(); }
      if (!summary.chunks || summary.problems.some(p => p.startsWith('corrupt:') || p.startsWith('gap-or-order:'))) {
        throw new CapabilityError('BadInput', 'Integrity problem: original chunks preserved; partial export needs manual review');
      }
      await rename(temporary, target);
      return target;
    });
  }
  async delete(id: string): Promise<void> {
    return this.serial(id, async () => {
      if (this.next.has(id)) throw new CapabilityError('BadInput', 'Cannot delete an active recording');
      const directory = this.directory(id);
      // A validated UUID is the sole path component accepted from the renderer.
      if (path.dirname(path.resolve(directory)) !== path.resolve(this.root)) throw new CapabilityError('BadInput', 'Unsafe path');
      await rm(directory, { recursive: true, force: false });
    });
  }
  async usage(): Promise<number> {
    await this.requestPersistence();
    const status = await statfs(this.root);
    return status.bavail * status.bsize;
  }
}
