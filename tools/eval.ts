import { readFile } from 'node:fs/promises';

async function main(): Promise<void> {
  const fixture = JSON.parse(await readFile('fixtures/synthetic-session.json', 'utf8'));
  console.log(JSON.stringify({
    suite: 'phase-0', syntheticChunks: fixture.chunks.length,
    goldenSet: 'not-ready',
    visualRecall: null, visualPrecision: null, transcriptionError: null,
    notesQuality: null, reason: 'Permitted labelled class fixtures and S2–S6 adapters are not available yet.',
  }, null, 2));
}
void main().catch(error => { console.error(error); process.exitCode = 1; });
