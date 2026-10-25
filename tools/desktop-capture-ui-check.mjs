import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import electron from 'electron';

const built = spawnSync(process.execPath, ['tools/desktop-capture-build.mjs'], { stdio: 'inherit' });
if (built.status !== 0) process.exit(built.status ?? 1);
const dotnet = existsSync('.tools/dotnet/dotnet.exe') ? '.tools/dotnet/dotnet.exe' : 'dotnet';
const env = { ...process.env, LECTURELENS_CAPTURE_RUNTIME: electron, LECTURELENS_CAPTURE_ENTRY: resolve('dist/desktop-capture/main.cjs') };
delete env.ELECTRON_RUN_AS_NODE;
const result = spawnSync(dotnet, ['run', '--project', 'tests/DesktopCaptureUiChecks/DesktopCaptureUiChecks.csproj'], { stdio: 'inherit', env });
if (result.error) console.error(result.error.message);
process.exitCode = result.status ?? 1;
