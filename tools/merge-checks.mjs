import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
const dotnet = existsSync('.tools/dotnet/dotnet.exe') ? '.tools/dotnet/dotnet.exe' : 'dotnet';
const result = spawnSync(dotnet, ['run', '--project', 'tests/MergeChecks/MergeChecks.csproj'], { stdio: 'inherit' });
if (result.error) console.error(result.error.message);
process.exitCode = result.status ?? 1;
