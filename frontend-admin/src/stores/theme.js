import { defineStore } from 'pinia'

const STORAGE_KEY = 'admin_theme_mode'
export const MODES = ['auto', 'light', 'dark']

export function computeIsDarkEffective(mode) {
  if (mode === 'dark') return true
  if (mode === 'light') return false
  if (typeof window === 'undefined' || !window.matchMedia) return false
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function applyThemeToDom(mode) {
  if (typeof document === 'undefined') return
  const isDark = computeIsDarkEffective(mode)
  const root = document.documentElement
  const body = document.body
  if (isDark) {
    root.setAttribute('data-theme', 'dark')
    root.classList.add('dark')
    root.classList.remove('light')
    body.classList.add('dark')
    body.classList.remove('light')
  } else {
    root.setAttribute('data-theme', 'light')
    root.classList.add('light')
    root.classList.remove('dark')
    body.classList.add('light')
    body.classList.remove('dark')
  }
}

function readStoredMode() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY) || localStorage.getItem('app_theme_mode')
    if (MODES.includes(raw)) return raw
  } catch { /* ignore */ }
  return 'auto'
}

function writeStoredMode(mode) {
  try {
    localStorage.setItem(STORAGE_KEY, mode)
    localStorage.setItem('app_theme_mode', mode)
  } catch { /* ignore */ }
}

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

export const useAdminThemeStore = defineStore('adminTheme', {
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
    toggleNext() {
      const idx = MODES.indexOf(this.mode)
      const nextMode = MODES[(idx + 1) % MODES.length]
      this.setMode(nextMode)
      return nextMode
    },
    bootstrap() {
      applyThemeToDom(this.mode)
      ensureMediaListener(() => this.mode)
    }
  }
})
