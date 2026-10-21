import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { decodePacket } from '../src/adapters/storage/packet';

async function main(): Promise<void> {
  const directory = process.argv[2];
  if (!directory) throw new Error('Usage: npm run session:report -- <session-directory>');
  const filenames = (await readdir(directory)).filter(name => /^\d{8}\.chunk$/.test(name)).sort();
  const times: number[] = []; let bytes = 0;
  for (const filename of filenames) {
    const chunk = decodePacket(await readFile(path.join(directory, filename)));
    times.push(chunk.atMs); bytes += chunk.bytes.byteLength;
  }
  let truncatedLines = 0;
  let timeline = '';
  try { timeline = await readFile(path.join(directory, 'events.ndjson'), 'utf8'); }
  catch (error) { if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error; }
  const events = timeline.split('\n').filter(Boolean).flatMap(line => {
    try { return [JSON.parse(line)]; } catch { truncatedLines++; return []; }
  });
  const resources = events.filter(item => item.type === 'diagnostic').map(item => {
    const processes = JSON.parse(item.reason).processes as { cpuPercent: number; workingSetKB: number }[];
    return { atMs: item.atMs, cpuPercentSum: processes.reduce((sum, p) => sum + p.cpuPercent, 0), workingSetMB: processes.reduce((sum, p) => sum + p.workingSetKB, 0) / 1024 };
  });
  const gaps = times.slice(1).map((time, index) => time - times[index]);
  console.log(JSON.stringify({
    containerChunks: times.length, bytes, lastChunkMs: times.at(-1), largestDeliveryGapMs: gaps.length ? Math.max(...gaps) : null,
    timeline: events.filter(item => item.type !== 'diagnostic'), truncatedTimelineLines: truncatedLines,
    resources: { samples: resources.length, first: resources[0], last: resources.at(-1), peakWorkingSetMB: resources.length ? Math.max(...resources.map(item => item.workingSetMB)) : null },
    limits: 'Container delivery is not decoded video/audio cadence. Pause periods are not excluded from the largest delivery gap. Live S1 checks and media inspection are still required.',
  }, null, 2));
}
void main().catch(error => { console.error(error); process.exitCode = 1; });
