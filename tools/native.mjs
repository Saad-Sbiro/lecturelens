import { spawnSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, copyFileSync, cpSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import electron from 'electron';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
process.chdir(root);
const local = resolve('.tools/dotnet/dotnet.exe');
const dotnet = existsSync(local) ? local : 'dotnet';
const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', ...(existsSync(local) ? { DOTNET_ROOT: dirname(local) } : {}) };
const project = 'src/native/Desktop/Desktop.csproj';
const mode = process.argv[2] ?? 'dev';
function run(args) {
  const result = spawnSync(dotnet, args, { stdio: 'inherit', env });
  if (result.error) console.error('Install the .NET 10 SDK, then try again.', result.error.message);
  if (result.status !== 0) process.exit(result.status ?? 1);
}
if (mode === 'package') {
  for (const script of ['tools/browser-setup.mjs', 'tools/desktop-capture-build.mjs']) {
    const built = spawnSync(process.execPath, [script, '--no-register'], { stdio: 'inherit', env });
    if (built.status !== 0) process.exit(built.status ?? 1);
  }
  // App-relative runtime lookup avoids any dependency on the user's installed runtime.
  const installed = spawnSync(dotnet, ['--list-runtimes'], { encoding: 'utf8', env });
  const entries = [...(installed.stdout ?? '').matchAll(/^(Microsoft\.(?:NETCore|WindowsDesktop)\.App) (10\.\d+\.\d+) \[(.+)\]/gm)];
  if (!entries.some(e => e[1] === 'Microsoft.WindowsDesktop.App') || !entries.some(e => e[1] === 'Microsoft.NETCore.App')) throw new Error('Install the .NET 10 Windows SDK before packaging.');
  run(['publish', project, '-c', 'Release', '--self-contained', 'false', '-p:AppHostRelativeDotNet=runtime', '-o', 'release/native-win-x64']);
  const output = resolve('release/native-win-x64/runtime');
  for (const [, name, version, base] of entries) cpSync(resolve(base, version), resolve(output, 'shared', name, version), { recursive: true });
  const runtimeRoot = resolve(entries[0][3], '../..');
  cpSync(resolve(runtimeRoot, 'host'), resolve(output, 'host'), { recursive: true });
  cpSync(resolve('dist/desktop-capture'), resolve('release/native-win-x64/desktop-capture'), { recursive: true });
  cpSync(dirname(electron), resolve('release/native-win-x64/capture-runtime'), { recursive: true });
  run(['publish', 'src/native/NativeHost/NativeHost.csproj', '-c', 'Release', '--self-contained', 'false', '-p:AppHostRelativeDotNet=../runtime', '-o', 'release/native-win-x64/native-host']);
  console.log('Ready: release/native-win-x64/LectureLens.exe (keep the entire folder together).');
} else {
  if (mode === 'dev') {
    const setup = spawnSync(process.execPath, ['tools/browser-setup.mjs'], { stdio: 'inherit', env });
    if (setup.status !== 0) process.exit(setup.status ?? 1);
    const capture = spawnSync(process.execPath, ['tools/desktop-capture-build.mjs'], { stdio: 'inherit', env });
    if (capture.status !== 0) process.exit(capture.status ?? 1);
  }
  run(['build', project, '-c', 'Debug']);
  const smoke = mode === 'smoke' || process.argv.includes('--smoke');
  let xaml = resolve('src/native/Desktop/MainWindow.xaml');
  if (smoke) {
    mkdirSync('artifacts', { recursive: true });
    xaml = resolve('artifacts/smoke-window.xaml');
    copyFileSync('src/native/Desktop/MainWindow.xaml', xaml);
  }
  const child = spawn(dotnet, ['src/native/Desktop/bin/Debug/net10.0-windows/LectureLens.dll', ...(smoke ? ['--smoke'] : [])], { stdio: 'inherit', env: { ...env, LECTURELENS_XAML: xaml, LECTURELENS_EXTENSION: resolve('dist/extension'), LECTURELENS_CAPTURE_RUNTIME: electron, LECTURELENS_CAPTURE_ENTRY: resolve('dist/desktop-capture/main.cjs') } });
  const timeout = smoke ? setTimeout(() => { child.kill(); process.exitCode = 1; }, 30000) : undefined;
  child.on('error', e => { console.error(e); process.exitCode = 1; if (timeout) clearTimeout(timeout); });
  child.on('exit', code => { if (timeout) clearTimeout(timeout); process.exitCode = code ?? 1; });
  process.on('SIGINT', () => child.kill());
}
