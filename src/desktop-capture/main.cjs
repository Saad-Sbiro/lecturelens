const { app, BrowserWindow, desktopCapturer, ipcMain, dialog } = require('electron');
const { join, resolve } = require('node:path');
const { StorageHost } = require('./host.cjs');

app.setName('LectureLens Desktop Capture');
let window, host, selected, granted, recording = false, allowClose = false;
const smoke = process.argv.includes('--smoke');
if (!app.requestSingleInstanceLock()) app.quit();
else {
  app.on('second-instance', () => { if (window) { window.restore(); window.focus(); } });
  app.whenReady().then(async () => {
    window = new BrowserWindow({ width: 1000, height: 850, minWidth: 750, minHeight: 650, title: 'LectureLens · Desktop capture', backgroundColor: '#15171b',
      webPreferences: { preload: join(__dirname, 'preload.cjs'), sandbox: true, contextIsolation: true, nodeIntegration: false, backgroundThrottling: false } });
    const trusted = event => event.sender === window.webContents && event.senderFrame === window.webContents.mainFrame;
    const sources = () => desktopCapturer.getSources({ types: ['window'], thumbnailSize: { width: 0, height: 0 } });
    const session = window.webContents.session;
    session.setPermissionRequestHandler((wc, permission, callback) => callback(wc === window.webContents && permission === 'display-capture'));
    session.setPermissionCheckHandler((wc, permission) => wc === window.webContents && permission === 'display-capture');
    session.setDisplayMediaRequestHandler(async (request, callback) => {
      const choice = selected; selected = undefined;
      if (!choice || choice.expires < Date.now() || !request.userGesture || request.frame !== window.webContents.mainFrame) return callback(null);
      try {
        const source = (await sources()).find(item => item.id === choice.id);
        if (!source) return callback(null);
        granted = { id: source.id, name: source.name };
        callback({ video: source, audio: 'loopback' });
      } catch { callback(null); }
    });
    window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
    window.webContents.on('will-navigate', event => event.preventDefault());
    ipcMain.handle('capture', async (event, op, data = {}) => {
      if (!trusted(event)) throw new Error('Untrusted capture request');
      if (op === 'list') return (await sources()).filter(item => item.name !== window.getTitle()).map(({ id, name }) => ({ id, name }));
      if (op === 'select') {
        if (recording || granted || data.consent !== true) throw new Error('Stop the preview first and confirm recording consent');
        if (!(await sources()).some(item => item.id === data.id)) throw new Error('Window is no longer available');
        selected = { id: data.id, expires: Date.now() + 30000 }; return;
      }
      if (op === 'release') { host?.close(); host = undefined; recording = false; granted = selected = undefined; return; }
      if (op === 'source') {
        if (!granted) return 'ended';
        const source = (await sources()).find(item => item.id === granted.id);
        if (!source) return 'ended';
        if (source.name !== granted.name) { granted.name = source.name; return 'changed'; }
        return 'same';
      }
      if (!['hello', 'create', 'append', 'event', 'slide', 'visualEvent', 'heartbeat', 'finish'].includes(op)) throw new Error('Unsupported operation');
      if (!granted) throw new Error('Preview a selected window first');
      host ??= new StorageHost(process.env.LECTURELENS_CAPTURE_HOST || resolve(__dirname, '../native-host/LectureLens.NativeHost.exe'));
      const result = await host.call(op, data);
      if (op === 'create') recording = true;
      if (op === 'finish') recording = false;
      return result;
    });
    window.on('close', event => {
      if (!recording || allowClose) return;
      event.preventDefault();
      const answer = dialog.showMessageBoxSync(window, { type: 'question', buttons: ['Keep recording', 'Stop and save'], defaultId: 0, cancelId: 0, message: 'Stop recording before closing?' });
      if (answer === 1) window.webContents.send('stop-capture');
    });
    window.webContents.on('render-process-gone', () => { host?.close(); recording = false; });
    window.on('closed', () => { host?.close(); app.quit(); });
    await window.loadFile(join(__dirname, 'index.html'));
    if (smoke) {
      const ok = await window.webContents.executeJavaScript("Boolean(document.querySelector('#preview') && document.querySelector('#start') && window.captureBridge)");
      const { mkdirSync, writeFileSync } = require('node:fs');
      mkdirSync('artifacts', { recursive: true });
      writeFileSync('artifacts/desktop-capture-smoke.png', (await window.webContents.capturePage()).toPNG());
      console.log(ok ? 'Desktop capture shell loaded; no capture started.' : 'Desktop capture shell failed');
      allowClose = true; app.exit(ok ? 0 : 1);
    }
  }).catch(error => { console.error(error); app.exit(1); });
}
