import axios from 'axios'
import { showToast } from 'vant'
import router from '../router'

const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

http.interceptors.request.use(
  (config) => {
    const method = (config.method || 'get').toUpperCase()

    // 离线时拦截一切写操作：不发起请求，直接提示。
    // 读操作(GET)交给 Service Worker 的离线缓存处理（见 vite.config.js runtimeCaching）。
    if (
      ['POST', 'PUT', 'DELETE', 'PATCH'].includes(method) &&
      typeof navigator !== 'undefined' &&
      navigator.onLine === false
    ) {
      showToast({ message: '当前处于离线状态，无法保存，请恢复网络后重试', type: 'fail' })
      const err = new Error('离线状态，写操作被拦截')
      err._offlineBlocked = true
      return Promise.reject(err)
    }

    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// 单飞锁：并发 401 共享同一次续期请求。
// Token 轮换（每次续期换发新 Refresh Token）后，若多个 401 各自并发续期，
// 会互相覆盖 localStorage 中的新凭证，导致偶发登出，故必须收敛为一次请求。
let refreshing = null

async function refreshTokens() {
  if (!refreshing) {
    refreshing = (async () => {
      const refreshToken = localStorage.getItem('refreshToken')
      const res = await axios.post('/api/auth/refresh', { refreshToken }, { timeout: 10000 })
      const newToken = res.data.token
      const newRefreshToken = res.data.refreshToken
      localStorage.setItem('token', newToken)
      if (newRefreshToken) localStorage.setItem('refreshToken', newRefreshToken)

      // 更新 Pinia 状态（若已初始化）
      try {
        const { useAuthStore } = await import('../stores/auth')
        const auth = useAuthStore()
        auth.token = newToken
        if (newRefreshToken) auth.refreshToken = newRefreshToken
      } catch { /* ignore */ }

      return newToken
    })().finally(() => {
      // 无论成败都解锁：失败后下一次请求可重新尝试续期
      refreshing = null
    })
  }
  return refreshing
}

http.interceptors.response.use(
  (response) => response.data,
  async (error) => {
    const status = error.response?.status
    const message =
      error.response?.data?.message || error.message || '请求失败，请稍后重试'

    // 401：先尝试用 refreshToken 无感续期并重放一次，凭证确已失效才真正登出
    if (status === 401) {
      const original = error.config
      // 只重放一次，且排除登录/刷新接口自身，避免死循环或无谓续期
      const isAuthCall = original?.url?.includes('/auth/login') || original?.url?.includes('/auth/refresh')
      if (localStorage.getItem('refreshToken') && !isAuthCall && !original?._retried) {
        try {
          const newToken = await refreshTokens()
          original._retried = true
          original.headers.Authorization = `Bearer ${newToken}`
          return http(original)
        } catch (refreshErr) {
          // 区分续期失败的性质，避免把暂时性故障误判成「登录失效」：
          // - 无响应（网络抖动/超时）或 5xx：暂时性失败。保留 token、不登出、不跳转，
          //   本次请求以续期错误向外抛出（不带 401，防止调用方误触发登出），
          //   待网络恢复后下一次请求会再次尝试续期。
          // - 4xx（后端明确判定 Refresh Token 无效/过期）：走下方登出流程。
          const refreshStatus = refreshErr?.response?.status
          if (!refreshStatus || refreshStatus >= 500) {
            return Promise.reject(refreshErr)
          }
        }
      }

      // 续期被拒或无可续期 token：清 token + 跳登录
      localStorage.removeItem('token')
      localStorage.removeItem('refreshToken')
      if (router.currentRoute.value.name !== 'login') {
        router.replace({
          name: 'login',
          query: { redirect: router.currentRoute.value.fullPath }
        })
      }
      return Promise.reject(error)
    }

    if (status === 403) {
      // 已登录但无权限：提示但不登出、不跳转
      try { showToast({ message: '没有权限执行此操作', type: 'fail' }) } catch { /* ignore */ }
      return Promise.reject(error)
    }

    // 网络错误/超时/离线/5xx：仅提示，不清 token、不跳转
    if (!error.response) {
      try { showToast({ message: '网络异常，请稍后重试', type: 'fail' }) } catch { /* ignore */ }
    } else {
      try { showToast(message) } catch { /* ignore */ }
    }

    return Promise.reject(error)
  }
)

export default http
