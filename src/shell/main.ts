import { app, BrowserWindow, Menu } from 'electron';
import path from 'node:path';
import { installBridge } from './bridge';

export async function createWindow(): Promise<BrowserWindow> {
  const window = new BrowserWindow({
    width: 1180, height: 880, minWidth: 720, minHeight: 620,
    title: 'LectureLens', backgroundColor: '#101916',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true, sandbox: true, nodeIntegration: false,
      backgroundThrottling: false,
    },
  });
  Menu.setApplicationMenu(null);
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', event => event.preventDefault());
  installBridge(window);
  const devUrl = !app.isPackaged && process.env.LECTURELENS_DEV_URL;
  if (devUrl) {
    if (devUrl !== 'http://127.0.0.1:5173') throw new Error('Unexpected development URL');
    await window.loadURL(devUrl);
  } else {
    await window.loadFile(path.join(__dirname, '../../../ui/index.html'));
  }
  return window;
}

if (require.main === module) {
  void app.whenReady().then(createWindow);
  app.on('window-all-closed', () => app.quit());
}
