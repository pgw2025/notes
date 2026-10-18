import { defineStore } from 'pinia'
import http from '../api/http'
import { compressImage } from '../utils/compressImage'
import { clearAllData, getUser, putUser } from '../utils/db'

// 清空离线缓存中的用户数据（与 vite.config.js 中 runtimeCaching 的 cacheName 保持一致）。
// 仅清数据型缓存，保留 app shell 预缓存以加速下次冷启动；防止不同账号在同一设备上串数据。
async function clearUserDataCaches() {
  if (typeof caches === 'undefined') return
  const names = ['api-notes', 'api-attachments', 'api-avatars', 'api-auth-me']
  try {
    await Promise.all(names.map((n) => caches.delete(n)))
  } catch { /* ignore */ }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    refreshToken: localStorage.getItem('refreshToken') || '',
    user: null
  }),

  getters: {
    isLoggedIn: (state) => !!state.token
  },

  actions: {
    setToken(token, refreshToken) {
      this.token = token
      if (refreshToken !== undefined) {
        this.refreshToken = refreshToken
        localStorage.setItem('refreshToken', refreshToken)
      }
      localStorage.setItem('token', token)
    },

    async login(account, password) {
      const res = await http.post('/auth/login', { account, password })
      this.setToken(res.token, res.refreshToken)
      await this.fetchUser()
    },

    async register(userName, email, password, displayName) {
      const payload = { userName, password }
      if (email) payload.email = email
      if (displayName) payload.displayName = displayName
      const res = await http.post('/auth/register', payload)
      this.setToken(res.token, res.refreshToken)
      await this.fetchUser()
    },

    /**
     * 先从 IndexedDB 读取用户资料 → 立即渲染（头像/昵称立即可见），
     * 再后台请求 /auth/me → 更新 IndexedDB + 同步最新资料。
     * 网络返回 401/403 → 登录态真失效，登出；
     * 网络返回 5xx 或超时 → 保留本地 user，不清登录态。
     */
    async fetchUser() {
      // Phase 1: 本地优先
      try {
        const local = await getUser()
        if (local) {
          this.user = local
        }
      } catch { /* IndexedDB 读取失败忽略 */ }

      // Phase 2: 网络同步
      try {
        const remote = await http.get('/auth/me')
        this.user = remote
        await putUser(remote)
      } catch (err) {
        const status = err?.response?.status
        if (status === 401 || status === 403) {
          this.logout()
        }
        // 其余错误：保留 Phase 1 已设置的 user（如果有的话），静默忽略
      }
    },

    async uploadAvatar(file) {
      if (!file) return ''
      // 头像显示本就很小，压缩到最长边 256px 以减小上传体积
      const compressed = await compressImage(file, { maxEdge: 256 })
      const fd = new FormData()
      fd.append('file', compressed || file)
      const res = await http.post('/auth/avatar', fd)
      return res.avatarUrl
    },

    async updateProfile({ displayName, avatarUrl, defaultNoteColor, customColors }) {
      const payload = {}
      if (displayName !== undefined) payload.displayName = displayName
      if (avatarUrl !== undefined) payload.avatarUrl = avatarUrl
      if (defaultNoteColor !== undefined) payload.defaultNoteColor = defaultNoteColor
      if (customColors !== undefined) payload.customColors = customColors
      const updated = await http.put('/auth/profile', payload)
      this.user = updated
      // 更新本地缓存
      try { await putUser(updated) } catch { /* ignore */ }
      return updated
    },

    /** 添加一个颜色到用户色板（去重 + 上传到服务器） */
    async addCustomColor(hex) {
      const normalized = (hex || '').trim().toUpperCase()
      if (!normalized) return false
      const current = Array.isArray(this.user?.customColors) ? [...this.user.customColors] : []
      if (current.some(c => c.toUpperCase() === normalized)) return false // 已存在
      current.push(normalized)
      await this.updateProfile({ customColors: current })
      return true
    },

    /** 从用户色板中删除一个颜色 */
    async removeCustomColor(hex) {
      const normalized = (hex || '').trim().toUpperCase()
      const current = Array.isArray(this.user?.customColors) ? [...this.user.customColors] : []
      const next = current.filter(c => c.toUpperCase() !== normalized)
      if (next.length === current.length) return false // 没找到
      await this.updateProfile({ customColors: next })
      return true
    },

    async logout() {
      this.token = ''
      this.refreshToken = ''
      this.user = null
      localStorage.removeItem('token')
      localStorage.removeItem('refreshToken')
      // 登出必须清所有本地数据：IndexedDB + Service Worker 缓存，
      // 防止下一位登录用户看到前一位的缓存数据
      try { await clearAllData() } catch { /* ignore */ }
      try { await clearUserDataCaches() } catch { /* ignore */ }
    }
  }
})
