import { BrowserWindow, desktopCapturer } from 'electron';
import type { WindowChoice } from '../ports/desktop';

export function installCaptureAccess(window: BrowserWindow) {
  let selected: { id: string; name: string; expires: number } | undefined;
  let embedded: BrowserWindow | undefined;
  let granted: { id: string; name: string } | undefined;
  const session = window.webContents.session;
  const trusted = (contents: Electron.WebContents | null) => contents === window.webContents;
  session.setPermissionRequestHandler((contents, permission, callback) => callback(trusted(contents) && permission === 'display-capture'));
  session.setPermissionCheckHandler((contents, permission) => trusted(contents) && permission === 'display-capture');
  session.setDisplayMediaRequestHandler(async (request, callback) => {
    const choice = selected;
    selected = undefined;
    if (!choice || Date.now() > choice.expires || request.frame !== window.webContents.mainFrame || !request.userGesture) {
      callback(null); return;
    }
    try {
      if (choice.id === 'embedded' && embedded && !embedded.isDestroyed()) {
        callback({ video: embedded.webContents.mainFrame, audio: embedded.webContents.mainFrame, enableLocalEcho: true });
      } else {
        const sources = await desktopCapturer.getSources({ types: ['window'], thumbnailSize: { width: 0, height: 0 } });
        const source = sources.find(item => item.id === choice.id);
        if (!source) { callback(null); return; }
        callback({ video: source, audio: 'loopback' });
      }
      granted = choice;
    } catch { callback(null); }
  });
  return {
    hasGrant: () => Boolean(granted),
    release: () => { granted = undefined; selected = undefined; },
    async checkSource(): Promise<'same' | 'changed' | 'ended'> {
      if (!granted) return 'same';
      if (granted.id === 'embedded') return embedded && !embedded.isDestroyed() ? 'same' : 'ended';
      const sources = await desktopCapturer.getSources({ types: ['window'], thumbnailSize: { width: 0, height: 0 } });
      const source = sources.find(item => item.id === granted?.id);
      if (!source) return 'ended';
      if (source.name !== granted.name) { granted.name = source.name; return 'changed'; }
      return 'same';
    },
    async list(): Promise<WindowChoice[]> {
      const sources = await desktopCapturer.getSources({ types: ['window'], thumbnailSize: { width: 240, height: 135 }, fetchWindowIcons: false });
      const choices = sources.filter(source => source.name !== window.getTitle()).map(source => ({ id: source.id, name: source.name, thumbnail: source.thumbnail.toDataURL() }));
      if (embedded && !embedded.isDestroyed()) choices.unshift({ id: 'embedded', name: embedded.getTitle(), thumbnail: '' });
      return choices;
    },
    async select(id: string, consent: boolean): Promise<void> {
      if (consent !== true) throw new Error('Consent is required before capture');
      const choices = await this.list();
      const choice = choices.find(item => item.id === id);
      if (!choice) throw new Error('Selected window is no longer available');
      selected = { ...choice, expires: Date.now() + 30_000 };
    },
    async openEmbedded(value: string): Promise<void> {
      const url = new URL(value);
      if (url.protocol !== 'https:' || url.username || url.password) throw new Error('An HTTPS class URL is required');
      if (embedded && !embedded.isDestroyed()) { embedded.focus(); return; }
      embedded = new BrowserWindow({
        width: 1100, height: 780, title: 'Embedded class experiment',
        webPreferences: { partition: `embedded-${Date.now()}`, sandbox: true, contextIsolation: true, nodeIntegration: false, backgroundThrottling: false },
      });
      const isolated = embedded.webContents.session;
      isolated.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
      isolated.setPermissionCheckHandler(() => false);
      isolated.setDisplayMediaRequestHandler((_request, callback) => callback(null));
      embedded.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
      embedded.webContents.on('will-navigate', (event, destination) => { if (!destination.startsWith('https://')) event.preventDefault(); });
      embedded.webContents.on('will-redirect', (event, destination) => { if (!destination.startsWith('https://')) event.preventDefault(); });
      await embedded.loadURL(url.toString());
    },
    dispose() { if (embedded && !embedded.isDestroyed()) embedded.close(); },
  };
}
