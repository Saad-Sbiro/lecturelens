const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('captureBridge', {
  call: (op, data) => ipcRenderer.invoke('capture', op, data),
  onStop: callback => ipcRenderer.on('stop-capture', () => callback()),
});
