const { app } = require('electron');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');
const { createWindow } = require('../dist/main/src/shell/main.js');

const timeout = setTimeout(() => { console.error('Smoke timeout'); app.exit(1); }, 35000);
app.whenReady().then(async () => {
  const window = await createWindow();
  const result = await window.webContents.executeJavaScript(`({title: document.title, heading: document.querySelector('h1')?.textContent, node: typeof require, bridge: typeof window.desktop})`);
  assert.equal(result.title, 'LectureLens');
  assert.ok(result.heading);
  assert.equal(result.node, 'undefined');
  assert.equal(result.bridge, 'object');
  assert.equal(await window.webContents.executeJavaScript(`document.querySelector('#start').disabled`), true);
  const unauthorized = await window.webContents.executeJavaScript(`window.desktop.createSession('video/webm').then(() => 'allowed', () => 'denied')`);
  assert.equal(unauthorized, 'denied');
  const noConsent = await window.webContents.executeJavaScript(`window.desktop.selectSource('invalid', false).then(() => 'allowed', () => 'denied')`);
  assert.equal(noConsent, 'denied');
  console.log(JSON.stringify({ visible: window.isVisible(), minimized: window.isMinimized(), destroyed: window.isDestroyed(), bounds: window.getBounds() }));
  window.show(); window.restore();
  await fs.mkdir('artifacts', { recursive: true });
  let screenshot;
  for (let attempt = 0; attempt < 20; attempt++) {
    try { screenshot = await window.webContents.capturePage(); if (!screenshot.isEmpty()) break; } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  assert.ok(screenshot && !screenshot.isEmpty(), 'Desktop surface must render');
  await fs.writeFile('artifacts/shell.png', screenshot.toPNG());
  if (process.env.LECTURELENS_DEV_URL) {
    const css = path.resolve('src/app/style.css');
    const original = await fs.readFile(css, 'utf8');
    try {
      await fs.writeFile(css, original + '\n:root { --smoke-reload: verified; }\n');
      const deadline = Date.now() + 8000;
      let value;
      do {
        value = await window.webContents.executeJavaScript(`getComputedStyle(document.documentElement).getPropertyValue('--smoke-reload').trim()`);
        if (value === 'verified') break;
        await new Promise(resolve => setTimeout(resolve, 100));
      } while (Date.now() < deadline);
      assert.equal(value, 'verified');
    } finally { await fs.writeFile(css, original); }
    const modulePath = '/@fs/' + path.resolve('tools/browser-probe.ts').replaceAll('\\', '/');
    const media = await window.webContents.executeJavaScript(`import(${JSON.stringify(modulePath)}).then(module => module.runSyntheticMediaProbe())`, true);
    assert.equal(media.decoded.width, 320);
    assert.equal(media.decoded.height, 180);
    assert.ok(media.chunks.length >= 2);
    assert.deepEqual(media.events.map(event => event.type), ['started', 'paused', 'resumed', 'ended']);
    const { LocalStore } = require('../dist/main/src/adapters/storage/local-store.js');
    const store = new LocalStore(path.resolve('artifacts/synthetic-media'));
    const id = await store.create(media.mime);
    for (const [sequence, part] of media.chunks.entries()) await store.append(id, { sequence, atMs: sequence * 2000, bytes: Uint8Array.from(part), reason: 'replay-chunk' });
    await store.finish(id, 'synthetic-media-smoke');
    const output = await store.recover(id);
    assert.deepEqual(await fs.readFile(output), Buffer.concat(media.chunks.map(part => Buffer.from(part))));
    console.log(JSON.stringify({ syntheticMedia: 'pass', decoded: media.decoded, parts: media.chunks.length, bytes: media.chunks.reduce((sum, part) => sum + part.length, 0), recovered: output }));
  }
  console.log(JSON.stringify({ shell: 'pass', sandbox: 'pass', liveReload: process.env.LECTURELENS_DEV_URL ? 'pass' : 'not-run', capture: 'not-run' }));
  clearTimeout(timeout);
  app.exit(0);
}).catch(error => { console.error(error); app.exit(1); });
