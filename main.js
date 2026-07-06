const {
  app, BrowserWindow, Tray, Menu, ipcMain,
  screen, nativeImage, Notification
} = require('electron')
const path = require('path')
const fs   = require('fs')
let autoUpdater = null
try {
  autoUpdater = require('electron-updater').autoUpdater
} catch (e) {}

// ── 数据路径 ────────────────────────────────────
const DATA_PATH = path.join(app.getPath('userData'), 'tasks.json')
function loadData() {
  try { if (fs.existsSync(DATA_PATH)) return JSON.parse(fs.readFileSync(DATA_PATH, 'utf8')) }
  catch (e) {}
  return null
}
function saveData(data) {
  try { fs.writeFileSync(DATA_PATH, JSON.stringify(data, null, 2), 'utf8') } catch (e) {}
}

// ── 全局状态 ────────────────────────────────────
let mainWindow  = null
let tray        = null
let isPinned    = true
let isExpanded  = true
let alwaysOnTop = false
let edgeTopActive = false
let edgeHoverStartedAt = 0
let updateCheckTimer = null

const PANEL_WIDTH     = 360
const COLLAPSED_WIDTH = 8
const EDGE_DETECT_WIDTH = 4
const EDGE_DETECT_INTERVAL = 80
const EDGE_DETECT_DWELL_MS = 500
const ICON_PATH = path.join(__dirname, 'assets', 'icon.png')

// ── 内嵌托盘图标（32×32 绿色，无需外部文件）─────
function buildTrayIcon() {
  if (fs.existsSync(ICON_PATH)) {
    const icon = nativeImage.createFromPath(ICON_PATH)
    if (!icon.isEmpty()) return icon.resize({ width: 32, height: 32 })
  }

  // 一个合法的 32×32 绿色 PNG，base64 编码
  const b64 =
    'iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAA' +
    '7EAAAOxAGVKw4bAAAApElEQVRYhe2WsQqDMBCGP6EvkJWuhU5dunTp0qWL' +
    'Q1+ki4NOEkjfwMEnEAIZsnXNv8HBQSH/l+RyucRxHMdxHMf5b3LOkVKSpA' +
    'kAkiQpWmuMMYQQqLUGwFpLjJE5Z+aceefcPQDgnHPOHRERERERERGRl5Sk' +
    'lFJKKaWUUkoppZRSSqn7A0opnXMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA' +
    'AAAAAAAAAAAAAAAAAAAAAAAAAAAA2D8BrjUGqwAAAABJRU5ErkJggg=='
  try {
    return nativeImage.createFromDataURL('data:image/png;base64,' + b64)
  } catch (e) {
    return nativeImage.createEmpty()
  }
}

// ── 窗口安全访问 ─────────────────────────────────
function safeWin() {
  return mainWindow && !mainWindow.isDestroyed() ? mainWindow : null
}

function applyTopState(w) {
  if (!w) return
  const shouldTop = alwaysOnTop || edgeTopActive
  w.setAlwaysOnTop(shouldTop, shouldTop ? 'screen-saver' : 'normal')
  if (shouldTop) w.moveTop()
}

// ── 面板控制 ─────────────────────────────────────
function snapToRight() {
  const w = safeWin(); if (!w) return
  const { width: sw, height: sh } = screen.getPrimaryDisplay().workAreaSize
  if (isExpanded) {
    w.setBounds({ x: sw - PANEL_WIDTH, y: 0, width: PANEL_WIDTH, height: sh })
  } else {
    w.setBounds({ x: sw - COLLAPSED_WIDTH, y: 0, width: PANEL_WIDTH, height: sh })
  }
}

function expandPanel(options = {}) {
  const w = safeWin(); if (!w || isExpanded) return
  if (options.edgeTriggered) edgeTopActive = true
  isExpanded = true
  const { width: sw, height: sh } = screen.getPrimaryDisplay().workAreaSize
  w.setBounds({ x: sw - PANEL_WIDTH, y: 0, width: PANEL_WIDTH, height: sh }, true)
  if (!w.isVisible()) w.show()
  applyTopState(w)
  w.focus()
  w.webContents.send('panel-state', { expanded: true })
  updateTrayMenu()
}

function collapsePanel() {
  const w = safeWin(); if (!w || !isExpanded) return
  isExpanded = false
  edgeTopActive = false
  const { width: sw, height: sh } = screen.getPrimaryDisplay().workAreaSize
  w.setBounds({ x: sw - COLLAPSED_WIDTH, y: 0, width: PANEL_WIDTH, height: sh }, true)
  applyTopState(w)
  w.webContents.send('panel-state', { expanded: false })
  updateTrayMenu()
}

function showWindow(options = {}) {
  const w = safeWin(); if (!w) return
  if (!w.isVisible()) w.show()
  if (options.edgeTriggered) edgeTopActive = true
  if (isExpanded) applyTopState(w)
  expandPanel(options)
  w.focus()
}

// ── 鼠标触边自动展开 ─────────────────────────────
function startEdgeDetect() {
  setInterval(() => {
    if (!safeWin() || isPinned) return
    const { width: sw } = screen.getPrimaryDisplay().workAreaSize
    const cur = screen.getCursorScreenPoint()
    if (cur.x < sw - EDGE_DETECT_WIDTH || isExpanded) {
      edgeHoverStartedAt = 0
      return
    }
    if (!edgeHoverStartedAt) edgeHoverStartedAt = Date.now()
    if (Date.now() - edgeHoverStartedAt >= EDGE_DETECT_DWELL_MS) {
      showWindow({ edgeTriggered: true })
      edgeHoverStartedAt = 0
    }
  }, EDGE_DETECT_INTERVAL)
}

function getUpdateFeedUrl() {
  if (process.env.STICKY_TODO_UPDATE_URL) return process.env.STICKY_TODO_UPDATE_URL
  const configPath = path.join(process.resourcesPath || __dirname, 'update-config.json')
  try {
    if (!fs.existsSync(configPath)) return null
    const config = JSON.parse(fs.readFileSync(configPath, 'utf8'))
    return typeof config.url === 'string' && config.url.trim() ? config.url.trim() : null
  } catch (e) {
    return null
  }
}

function notifyUpdate(title, body) {
  if (Notification.isSupported()) new Notification({ title, body, silent: false }).show()
}

function configureAutoUpdate() {
  if (!autoUpdater) return
  const feedUrl = getUpdateFeedUrl()
  autoUpdater.autoDownload = true
  autoUpdater.autoInstallOnAppQuit = true
  if (feedUrl) autoUpdater.setFeedURL({ provider: 'generic', url: feedUrl })
  autoUpdater.on('update-available', () => notifyUpdate('便签更新', '发现新版本，正在后台下载。'))
  autoUpdater.on('update-downloaded', () => notifyUpdate('便签更新', '新版本已下载，退出应用后自动安装。'))
  autoUpdater.on('error', () => {})
}

function checkForUpdates() {
  if (!autoUpdater) return
  const feedUrl = getUpdateFeedUrl()
  const appUpdateYml = path.join(process.resourcesPath || __dirname, 'app-update.yml')
  if (!feedUrl && !fs.existsSync(appUpdateYml)) return
  autoUpdater.checkForUpdates().catch(() => {})
}

// ── 托盘菜单 ─────────────────────────────────────
function updateTrayMenu() {
  if (!tray || tray.isDestroyed()) return
  const menu = Menu.buildFromTemplate([
    { label: '便签待办', enabled: false },
    { type: 'separator' },
    {
      label: isExpanded ? '收起面板' : '展开面板',
      click: () => isExpanded ? collapsePanel() : showWindow()
    },
    {
      label: isPinned ? '取消固定（触边展开）' : '固定在侧边',
      click: () => {
        isPinned = !isPinned
        if (isPinned) showWindow()
        const w = safeWin()
        if (w) w.webContents.send('pin-state', { pinned: isPinned })
        updateTrayMenu()
      }
    },
    {
      label: alwaysOnTop ? '取消置顶' : '窗口置顶',
      click: () => {
        alwaysOnTop = !alwaysOnTop
        const w = safeWin()
        if (w) applyTopState(w)
        updateTrayMenu()
      }
    },
    {
      label: '检查更新',
      click: () => checkForUpdates()
    },
    { type: 'separator' },
    {
      label: '开机自启动',
      type: 'checkbox',
      checked: app.getLoginItemSettings().openAtLogin,
      click: (item) => app.setLoginItemSettings({ openAtLogin: item.checked })
    },
    { type: 'separator' },
    { label: '退出程序', click: () => app.quit() }
  ])
  tray.setContextMenu(menu)
}

// ── 创建托盘 ─────────────────────────────────────
function createTray() {
  tray = new Tray(buildTrayIcon())
  tray.setToolTip('便签待办 — 点击展开/收起')
  updateTrayMenu()

  // ★ 修复：用 safeWin() 避免 mainWindow 为 null 时报错
  tray.on('click', () => {
    const w = safeWin()
    if (!w) {
      createWindow()
      return
    }
    if (!w.isVisible()) {
      showWindow()
    } else if (isExpanded) {
      collapsePanel()
    } else {
      showWindow()
    }
  })
}

// ── 创建主窗口 ───────────────────────────────────
function createWindow() {
  if (safeWin()) return

  const { width: sw, height: sh } = screen.getPrimaryDisplay().workAreaSize

  mainWindow = new BrowserWindow({
    width:       PANEL_WIDTH,
    height:      sh,
    x:           sw - PANEL_WIDTH,
    y:           0,
    frame:       false,
    transparent: false,
    resizable:   false,
    skipTaskbar: true,          // 不在任务栏显示（只在托盘）
    alwaysOnTop: alwaysOnTop,
    webPreferences: {
      nodeIntegration:  false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    },
    backgroundColor: '#f0ede6',
    icon: ICON_PATH,
    show: false
  })

  mainWindow.loadFile(path.join(__dirname, 'src', 'index.html'))

  mainWindow.once('ready-to-show', () => {
    mainWindow.show()
    snapToRight()
  })

  // ★ 修复：拦截关闭，改为收起，不销毁窗口
  mainWindow.on('close', (e) => {
    if (!app.isQuiting) {
      e.preventDefault()
      collapsePanel()
    }
  })

  mainWindow.on('blur', () => {
    if (!isPinned && isExpanded) collapsePanel()
  })

  mainWindow.on('closed', () => { mainWindow = null })
}

// ── IPC 处理 ─────────────────────────────────────
ipcMain.handle('load-data',  ()      => loadData())
ipcMain.handle('save-data',  (_, d)  => { saveData(d); return true })
ipcMain.handle('get-state',  ()      => ({ pinned: isPinned, expanded: isExpanded, alwaysOnTop }))
ipcMain.handle('collapse',   ()      => { collapsePanel(); return true })
ipcMain.handle('expand',     ()      => { showWindow();    return true })
ipcMain.handle('minimize',   ()      => { collapsePanel(); return true })

ipcMain.handle('toggle-pin', () => {
  isPinned = !isPinned
  const w = safeWin()
  if (w) w.webContents.send('pin-state', { pinned: isPinned })
  updateTrayMenu()
  return { pinned: isPinned }
})

ipcMain.handle('toggle-top', () => {
  alwaysOnTop = !alwaysOnTop
  const w = safeWin()
  if (w) applyTopState(w)
  updateTrayMenu()
  return { alwaysOnTop }
})

ipcMain.handle('notify', (_, { title, body }) => {
  if (Notification.isSupported()) {
    new Notification({ title, body, silent: false }).show()
  }
})

// ── App 生命周期 ─────────────────────────────────
app.whenReady().then(() => {
  createWindow()
  createTray()
  startEdgeDetect()
  configureAutoUpdate()
  setTimeout(checkForUpdates, 5000)
  updateCheckTimer = setInterval(checkForUpdates, 6 * 60 * 60 * 1000)
})

// ★ 修复：Windows 关闭所有窗口时不退出 app，保留托盘
app.on('window-all-closed', () => {
  // 故意留空，不调用 app.quit()
})

app.on('before-quit', () => {
  if (updateCheckTimer) clearInterval(updateCheckTimer)
  app.isQuiting = true
})
