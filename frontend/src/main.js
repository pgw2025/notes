import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Vant from 'vant'
import { showToast, showConfirmDialog } from 'vant'
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

// ---- PWA：注册 Service Worker ----
// vite.config.js 为 registerType: 'prompt'：
// 新版 SW 安装完成后不会自动激活，而是触发 onNeedRefresh；
// 这里弹确认对话框，用户点「立即更新」才 skipWaiting 激活并刷新页面，
// 点「稍后」则下次打开页面时旧 SW 仍在运行、新版继续挂起等待。
if ('serviceWorker' in navigator) {
  try {
    const updateSW = registerSW({
      immediate: true,
      onNeedRefresh() {
        showConfirmDialog({
          title: '发现新版本',
          message: '新版本已就绪，是否立即更新？更新后页面将自动刷新。',
          confirmButtonText: '立即更新',
          cancelButtonText: '稍后'
        })
          .then(() => {
            // 传 true：等新 SW 激活接管后自动 reload，加载到的就是新 bundle
            updateSW(true)
          })
          .catch(() => {
            // 用户选择「稍后」：本次会话继续用旧版本，不强制打断
            showToast({ message: '已跳过本次更新，下次打开时可再次选择', position: 'bottom' })
          })
      },
      onOfflineReady() {
        // 首次资源预缓存完成，可离线访问
        showToast({ message: '已支持离线访问', position: 'bottom' })
      },
      onRegisterError(err) {
        console.warn('PWA 注册失败', err)
      }
    })
  } catch (e) {
    console.warn('PWA 注册失败', e)
  }
}
