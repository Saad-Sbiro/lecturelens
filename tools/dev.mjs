import { createServer } from 'vite';
import { spawn, spawnSync } from 'node:child_process';
import { watch } from 'node:fs';
import electron from 'electron';

function compile() {
  return spawnSync(process.execPath, ['node_modules/typescript/bin/tsc', '-p', 'tsconfig.main.json'], { stdio: 'inherit' }).status === 0;
}
if (!compile()) process.exit(1);
const server = await createServer();
await server.listen();
const env = { ...process.env, LECTURELENS_DEV_URL: 'http://127.0.0.1:5173' };
delete env.ELECTRON_RUN_AS_NODE;
const smoke = process.argv.includes('--smoke');
let closing = false;
let restarting = false;
let child;
function start() {
  child = spawn(electron, [smoke ? 'tools/smoke.cjs' : '.'], { env, stdio: 'inherit' });
  child.on('error', async error => { console.error(error); await close(1); });
  child.on('exit', async code => { if (!restarting) await close(code ?? 0); });
}
async function close(code) {
  if (closing) return;
  closing = true;
  watcher?.close();
  child?.kill();
  await server.close();
  process.exit(code);
}
let timer;
const watcher = smoke ? undefined : watch('src', { recursive: true }, (_, filename) => {
  if (!filename || !/^(shell|ports|adapters[\\/]storage)[\\/]/.test(filename)) return;
  clearTimeout(timer);
  timer = setTimeout(() => {
    if (!compile()) return;
    restarting = true;
    child.once('exit', () => { restarting = false; start(); });
    child.kill();
  }, 250);
});
process.on('SIGINT', () => { void close(0); });
process.on('SIGTERM', () => { void close(0); });
start();
