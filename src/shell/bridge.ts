import { app, BrowserWindow, dialog, globalShortcut, ipcMain, powerSaveBlocker, shell } from 'electron';
import path from 'node:path';
import { LocalStore } from '../adapters/storage/local-store';
import { captureConfig } from '../../config/capture';
import { installCaptureAccess } from './capture-access';
import strings from '../../config/ui.json';
import { defaultLanguages } from '../../config/languages';

export function installBridge(window: BrowserWindow): void {
  const store = new LocalStore(path.join(app.getPath('userData'), 'recordings'));
  const access = installCaptureAccess(window);
  const text = strings[defaultLanguages.ui as keyof typeof strings];
  let active: string | undefined;
  let blocker: number | undefined;
  let origin = 0;
  let creating = false;
  let writing = false;
  let polling = false;
  const channels: string[] = [];
  const handle = (channel: string, fn: (...args: any[]) => unknown) => {
    channels.push(channel);
    ipcMain.handle(channel, (event, ...args) => {
      if (event.sender !== window.webContents || event.senderFrame !== window.webContents.mainFrame) throw new Error('Untrusted IPC sender');
      return fn(...args);
    });
  };
  function endActive(): void {
    if (blocker !== undefined && powerSaveBlocker.isStarted(blocker)) powerSaveBlocker.stop(blocker);
    blocker = undefined;
    access.release();
    active = undefined;
    if (!window.isDestroyed()) window.setTitle('LectureLens');
  }
  const assertActive = (id: unknown) => { if (typeof id !== 'string' || id !== active) throw new Error('Session is not active'); };
  handle('windows:list', () => access.list());
  handle('capture:select', (id, consent) => { if (active) throw new Error('End the recording first'); return access.select(id, consent); });
  handle('session:create', async mime => {
    if (active || creating || !access.hasGrant()) throw new Error('A consented preview is required, with no other recording active');
    creating = true;
    try {
      const estimated = (captureConfig.videoBitsPerSecond + captureConfig.audioBitsPerSecond) / 8 * captureConfig.estimatedMinutes * 60;
      if (await store.usage() < estimated + captureConfig.reserveBytes) throw new Error('Not enough free disk space for the estimated session');
      active = await store.create(mime);
      origin = performance.now();
      blocker = powerSaveBlocker.start('prevent-display-sleep');
      return { id: active, clockEpoch: performance.timeOrigin + origin };
    } finally { creating = false; }
  });
  handle('session:append', async (id, sequence, atMs, bytes) => {
    assertActive(id);
    if (writing || !(bytes instanceof ArrayBuffer)) throw new Error('Invalid or overlapping append');
    writing = true;
    try { await store.append(id, { sequence, atMs, bytes: new Uint8Array(bytes), reason: 'capture-chunk' }); }
    finally { writing = false; }
  });
  handle('session:event', (id, type, atMs, reason) => {
    assertActive(id);
    if (type === 'started' || type === 'resumed') window.setTitle(`${text.title} · ${text.recording}`);
    if (type === 'paused') window.setTitle(`${text.title} · ${text.paused}`);
    return store.event(id, type, atMs, reason);
  });
  handle('session:finish', async (id, reason) => {
    assertActive(id);
    if (typeof reason !== 'string' || reason.length > 200) throw new Error('Invalid end reason');
    try { await store.finish(id, reason); }
    finally { store.abandon(id); endActive(); }
  });
  handle('sessions:list', () => store.list());
  handle('session:recover', id => store.recover(id));
  handle('session:delete', async id => {
    const result = await dialog.showMessageBox(window, { type: 'warning', buttons: [text.cancel, text.delete], defaultId: 0, cancelId: 0, message: text.deleteConfirm });
    if (result.response === 1) await store.delete(id);
  });
  handle('disk:usage', async () => ({ free: await store.usage(), estimated: (captureConfig.videoBitsPerSecond + captureConfig.audioBitsPerSecond) / 8 * captureConfig.estimatedMinutes * 60 }));
  handle('folder:open', async () => { await store.requestPersistence(); const error = await shell.openPath(store.root); if (error) throw new Error(error); });
  handle('embedded:open', (url: string) => access.openEmbedded(url));
  const shortcut = globalShortcut.register('CommandOrControl+Shift+Space', () => { if (active) window.webContents.send('capture:pause'); });
  if (!shortcut) console.warn('Pause shortcut unavailable; use the pause button.');
  const timer = setInterval(async () => {
    if (!active || polling) return;
    polling = true;
    const id = active;
    try {
      const metrics = app.getAppMetrics().map(item => ({ type: item.type, cpuPercent: item.cpu.percentCPUUsage, workingSetKB: item.memory.workingSetSize }));
      await store.event(id, 'diagnostic', performance.now() - origin, JSON.stringify({ processes: metrics }));
      if (await store.usage() < captureConfig.reserveBytes) window.webContents.send('capture:end', 'low-disk');
      const status = await access.checkSource();
      if (status === 'changed') window.webContents.send('capture:sourceChanged');
      if (status === 'ended') window.webContents.send('capture:end', 'source-ended');
    } catch (error) { console.warn('Capture diagnostics unavailable:', String(error)); }
    finally { polling = false; }
  }, 5000);
  window.on('close', event => {
    if (active) { event.preventDefault(); window.webContents.send('capture:end', 'application-close'); }
  });
  const interrupted = () => { if (active) store.abandon(active); endActive(); };
  window.webContents.on('render-process-gone', interrupted);
  window.webContents.on('did-start-loading', interrupted);
  window.on('closed', () => {
    clearInterval(timer); interrupted(); access.dispose();
    if (shortcut) globalShortcut.unregister('CommandOrControl+Shift+Space');
    for (const channel of channels) ipcMain.removeHandler(channel);
  });
}
