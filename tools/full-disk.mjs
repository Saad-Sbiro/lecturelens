import { spawnSync } from 'node:child_process';
import { statfs } from 'node:fs/promises';
import os from 'node:os';

const disk = await statfs(os.tmpdir());
if (disk.bavail * disk.bsize < 4_000_000_000) throw new Error('The full synthetic disk test needs at least 4 GB free in the temporary directory.');
const compile = spawnSync(process.execPath, ['node_modules/typescript/bin/tsc', '-p', 'tsconfig.main.json'], { stdio: 'inherit' });
if (compile.status !== 0) process.exit(compile.status ?? 1);
const result = spawnSync(process.execPath, ['--test', 'dist/main/tests/crash.test.js'], { stdio: 'inherit', env: { ...process.env, LECTURELENS_FULL_DISK_TEST: '1' } });
process.exit(result.status ?? 1);
