import { defineConfig } from 'vite';
import { resolve } from 'node:path';
export default defineConfig({ root: 'src/extension', base: './', build: { target: 'chrome116', outDir: '../../dist/extension', emptyOutDir: true, rollupOptions: { input: { background: resolve('src/extension/background.ts'), popup: resolve('src/extension/popup.html'), offscreen: resolve('src/extension/offscreen.html') }, output: { entryFileNames: '[name].js' } } } });
