import { createApp } from 'vue'
import { createPinia } from 'pinia'
import Vant from 'vant'
import 'vant/lib/index.css'

import App from './App.vue'
import router from './router'
import './style.css'

import { applyThemeToDom } from './stores/theme'
import { useThemeStore } from './stores/theme'

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
app.use(pinia)
app.use(router)
app.use(Vant)

router.isReady().then(() => {
  // 挂载前先初始化一次 Pinia 状态 + 注册 prefers-color-scheme 监听
  const theme = useThemeStore()
  theme.bootstrap()
  app.mount('#app')
})
