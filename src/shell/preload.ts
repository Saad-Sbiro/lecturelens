import { contextBridge, ipcRenderer } from 'electron';
import type { DesktopBridge } from '../ports/desktop';

const subscribe = (channel: string, callback: () => void) => {
  ipcRenderer.on(channel, callback);
  return () => { ipcRenderer.removeListener(channel, callback); };
};
const bridge: DesktopBridge = {
  listWindows: () => ipcRenderer.invoke('windows:list'),
  selectSource: (id, consent) => ipcRenderer.invoke('capture:select', id, consent),
  createSession: mime => ipcRenderer.invoke('session:create', mime),
  append: (id, sequence, atMs, bytes) => ipcRenderer.invoke('session:append', id, sequence, atMs, bytes),
  event: (id, type, atMs, reason) => ipcRenderer.invoke('session:event', id, type, atMs, reason),
  finish: (id, reason) => ipcRenderer.invoke('session:finish', id, reason),
  listSessions: () => ipcRenderer.invoke('sessions:list'),
  recover: id => ipcRenderer.invoke('session:recover', id),
  deleteSession: id => ipcRenderer.invoke('session:delete', id),
  disk: () => ipcRenderer.invoke('disk:usage'),
  openFolder: () => ipcRenderer.invoke('folder:open'),
  openEmbedded: url => ipcRenderer.invoke('embedded:open', url),
  onPause: cb => subscribe('capture:pause', cb),
  onSourceChanged: cb => subscribe('capture:sourceChanged', cb),
  onEnd: cb => {
    const listener = (_event: Electron.IpcRendererEvent, reason: string) => cb(reason);
    ipcRenderer.on('capture:end', listener);
    return () => { ipcRenderer.removeListener('capture:end', listener); };
  },
};
contextBridge.exposeInMainWorld('desktop', bridge);
