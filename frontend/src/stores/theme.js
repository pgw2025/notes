import { defineStore } from 'pinia'

const STORAGE_KEY = 'app_theme_mode'
export const MODES = ['auto', 'light', 'dark']

// 在 store 初始化之前就能运行（给 main.js 防首屏白闪用，不依赖 Pinia）
export function applyThemeToDom(mode) {
  if (typeof document === 'undefined') return
  const isDark = computeIsDarkEffective(mode)
  const root = document.documentElement
  const body = document.body
  if (isDark) {
    root.setAttribute('data-theme', 'dark')
    body.classList.add('dark')
    body.classList.remove('light')
  } else {
    root.setAttribute('data-theme', 'light')
    body.classList.add('light')
    body.classList.remove('dark')
  }
}

export function computeIsDarkEffective(mode) {
  if (mode === 'dark') return true
  if (mode === 'light') return false
  // auto → follow OS
  if (typeof window === 'undefined' || !window.matchMedia) return false
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

function readStoredMode() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (MODES.includes(raw)) return raw
  } catch { /* ignore */ }
  return 'auto'
}

function writeStoredMode(mode) {
  try { localStorage.setItem(STORAGE_KEY, mode) } catch { /* ignore */ }
}

// 监听 prefers-color-scheme 变化（当 mode === auto 时实时切换）
let _mediaQuery = null
let _mediaHandler = null
let _boundModeGetter = null
function ensureMediaListener(getMode) {
  if (typeof window === 'undefined' || !window.matchMedia) return
  if (_mediaQuery) return
  _mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
  _boundModeGetter = getMode
  _mediaHandler = () => {
    if (_boundModeGetter && _boundModeGetter() === 'auto') {
      applyThemeToDom('auto')
    }
  }
  try {
    _mediaQuery.addEventListener('change', _mediaHandler)
  } catch {
    try { _mediaQuery.addListener(_mediaHandler) } catch { /* ignore */ }
  }
}

export const useThemeStore = defineStore('theme', {
  state: () => ({
    mode: readStoredMode()
  }),
  getters: {
    isDarkEffective() {
      return computeIsDarkEffective(this.mode)
    }
  },
  actions: {
    setMode(mode) {
      if (!MODES.includes(mode)) return
      this.mode = mode
      writeStoredMode(mode)
      applyThemeToDom(mode)
    },
    /** 在 app 初始化时调用一次：应用 DOM 类 + 注册媒体查询监听 */
    bootstrap() {
      applyThemeToDom(this.mode)
      ensureMediaListener(() => this.mode)
    }
  }
})
