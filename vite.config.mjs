import { defineConfig } from 'vite';

export default defineConfig({
  root: 'src/app',
  base: './',
  server: { host: '127.0.0.1', port: 5173, strictPort: true },
  build: { outDir: '../../dist/ui', emptyOutDir: true },
});
