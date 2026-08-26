import { createApp } from 'vue'
import { createPinia } from 'pinia'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import App from './App.vue'
import router from './router'
import './style.css'
import { applyThemeToDom, useAdminThemeStore } from './stores/theme'

// 处理并过滤浏览器标准的 ResizeObserver loop 提示
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

// 在首屏渲染前立刻应用暗色/浅色模式，避免闪烁
(function bootstrapAdminTheme() {
  try {
    const raw = localStorage.getItem('admin_theme_mode') || localStorage.getItem('app_theme_mode')
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

// 全局注册 Element Plus 图标
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(pinia)
app.use(router)
app.use(ElementPlus, { locale: zhCn })

const theme = useAdminThemeStore()
theme.bootstrap()

app.mount('#app')
