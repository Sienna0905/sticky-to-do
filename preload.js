const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  loadData:  ()            => ipcRenderer.invoke('load-data'),
  saveData:  (data)        => ipcRenderer.invoke('save-data', data),
  getState:  ()            => ipcRenderer.invoke('get-state'),
  togglePin: ()            => ipcRenderer.invoke('toggle-pin'),
  toggleTop: ()            => ipcRenderer.invoke('toggle-top'),
  collapse:  ()            => ipcRenderer.invoke('collapse'),
  expand:    ()            => ipcRenderer.invoke('expand'),
  minimize:  ()            => ipcRenderer.invoke('minimize'),
  notify:    (title, body) => ipcRenderer.invoke('notify', { title, body }),

  onPanelState: (cb) => ipcRenderer.on('panel-state', (_, v) => cb(v)),
  onPinState:   (cb) => ipcRenderer.on('pin-state',   (_, v) => cb(v)),
})
