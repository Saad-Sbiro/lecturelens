import { build } from 'vite';
import { copyFileSync } from 'node:fs';
await build({ configFile: false, root: 'src/desktop-capture', base: './', build: { target: 'chrome134', outDir: '../../dist/desktop-capture', emptyOutDir: true } });
for (const name of ['main.cjs', 'preload.cjs', 'host.cjs']) copyFileSync(`src/desktop-capture/${name}`, `dist/desktop-capture/${name}`);
