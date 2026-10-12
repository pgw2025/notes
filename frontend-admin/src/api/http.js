import axios from 'axios'
import { ElMessage } from 'element-plus'
import router from '../router'

const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

/** 解析 JWT payload（不含验签，仅取声明用） */
export function decodeJwt(token) {
  try {
    const payload = token.split('.')[1]
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    )
    return JSON.parse(json)
  } catch {
    return null
  }
}

/** 从 JWT payload 中提取角色（后端使用 ClaimTypes.Role 标准声明 URI） */
export function getRolesFromToken(token) {
  const claims = decodeJwt(token)
  if (!claims) return []
  const roleClaim =
    claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  if (Array.isArray(roleClaim)) return roleClaim
  if (typeof roleClaim === 'string') return [roleClaim]
  return []
}

export function isAdminToken(token) {
  return getRolesFromToken(token).includes('Admin')
}

http.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('admin_token')
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
      const refreshToken = localStorage.getItem('admin_refreshToken')
      const res = await axios.post('/api/auth/refresh', { refreshToken }, { timeout: 10000 })
      const newToken = res.data.token
      const newRefreshToken = res.data.refreshToken
      localStorage.setItem('admin_token', newToken)
      if (newRefreshToken) localStorage.setItem('admin_refreshToken', newRefreshToken)

      // 同步 Pinia 状态（动态导入避免与 auth store 的循环依赖）
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
      if (localStorage.getItem('admin_refreshToken') && !isAuthCall && !original?._retried) {
        try {
          const newToken = await refreshTokens()
          original._retried = true
          original.headers.Authorization = `Bearer ${newToken}`
          return http(original)
        } catch (refreshErr) {
          // 区分续期失败的性质，避免把暂时性故障误判成「登录失效」：
          // - 无响应（网络抖动/超时）或 5xx：暂时性失败。保留凭证、不登出、不跳转，
          //   本次请求以续期错误向外抛出，网络恢复后下一次请求会再次尝试续期。
          // - 4xx（后端明确判定 Refresh Token 无效/过期）：走下方登出流程。
          const refreshStatus = refreshErr?.response?.status
          if (!refreshStatus || refreshStatus >= 500) {
            return Promise.reject(refreshErr)
          }
        }
      }

      // 续期被拒或无可续期 token：清凭证（须同步 Pinia 状态，路由守卫读的是 state）+ 跳登录
      try {
        const { useAuthStore } = await import('../stores/auth')
        useAuthStore().logout()
      } catch { /* ignore */ }
      if (router.currentRoute.value.name !== 'login') {
        router.replace({ name: 'login' })
      }
      return Promise.reject(error)
    }

    ElMessage.error(message)
    return Promise.reject(error)
  }
)

export default http