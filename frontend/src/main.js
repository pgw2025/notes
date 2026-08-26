import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Vant from 'vant'
import { showDialog, showToast } from 'vant'
import 'vant/lib/index.css'
import { registerSW } from 'virtual:pwa-register'

import App from './App.vue'
import router from './router'
import './style.css'

import { applyThemeToDom } from './stores/theme'
import { useThemeStore } from './stores/theme'

// 处理并过滤浏览器标准的 ResizeObserver loop 提示，避免冒泡为中断性错误
if (typeof window !== 'undefined') {
  const isResizeObserverMsg = (msg) => {
    return typeof msg === 'string' && (
      msg.includes('ResizeObserver loop completed with undelivered notifications') ||
      msg.includes('ResizeObserver loop limit exceeded')
    )
  }

  window.addEventListener('error', (event) => {
    if (isResizeObserverMsg(event?.message) || isResizeObserverMsg(event?.error?.message)) {
      event.stopImmediatePropagation()
      event.preventDefault()
      return true
    }
  })

  const NativeResizeObserver = window.ResizeObserver
  if (NativeResizeObserver) {
    window.ResizeObserver = class extends NativeResizeObserver {
      constructor(callback) {
        super((entries, observer) => {
          window.requestAnimationFrame(() => {
            callback(entries, observer)
          })
        })
      }
    }
  }
}

// 在 Vite 注入初始 body 样式之前、甚至 Vue 渲染之前，先把 dark class 挂上，
// 彻底避免首屏 1 帧白闪。
(function bootstrapThemeBeforeMount() {
  try {
    const raw = localStorage.getItem('app_theme_mode')
    applyThemeToDom((raw === 'auto' || raw === 'light' || raw === 'dark') ? raw : 'auto')
  } catch { /* ignore */ }
})()

const app = createApp(App)
const pinia = createPinia()

app.config.errorHandler = (err, instance, info) => {
  const msg = err?.message || String(err)
  if (msg.includes('ResizeObserver loop completed') || msg.includes('ResizeObserver loop limit exceeded')) {
    return
  }
  console.error(err, info)
}

app.use(pinia)
app.use(router)
app.use(Vant)

router.isReady().then(() => {
  // 挂载前先初始化一次 Pinia 状态 + 注册 prefers-color-scheme 监听
  const theme = useThemeStore()
  theme.bootstrap()
  app.mount('#app')
})

// ---- PWA：注册 Service Worker 并做更新提示 ----
if ('serviceWorker' in navigator) {
  let updateSW = null
  try {
    updateSW = registerSW({
      immediate: true,
      onOfflineReady() {
        // 首次资源预缓存完成，可离线访问
        showToast({ message: '已支持离线访问', position: 'bottom' })
      },
      async onNeedRefresh() {
        // 新版本 SW 已准备好，询问用户是否刷新
        try {
          await showDialog({
            title: '发现新版本',
            message: '有新版本可用，是否立即刷新加载？',
            showCancelButton: true,
            confirmButtonText: '立即刷新',
            cancelButtonText: '稍后'
          })
          // 用户确认：触发 skipWaiting → 新版接管 → 页面刷新
          updateSW && updateSW(true)
        } catch {
          // 用户选择“稍后”，忽略
        }
      }
    })
  } catch (e) {
    console.warn('PWA 注册失败', e)
  }
}

