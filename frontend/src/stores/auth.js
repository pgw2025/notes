import { defineStore } from 'pinia'
import http from '../api/http'

// 清空离线缓存中的用户数据（与 vite.config.js 中 runtimeCaching 的 cacheName 保持一致）。
// 仅清数据型缓存，保留 app shell 预缓存以加速下次冷启动；防止不同账号在同一设备上串数据。
async function clearUserDataCaches() {
  if (typeof caches === 'undefined') return
  const names = ['api-notes', 'api-attachments', 'api-avatars']
  try {
    await Promise.all(names.map((n) => caches.delete(n)))
  } catch { /* ignore */ }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    user: null
  }),

  getters: {
    isLoggedIn: (state) => !!state.token
  },

  actions: {
    setToken(token) {
      this.token = token
      localStorage.setItem('token', token)
    },

    async login(account, password) {
      const res = await http.post('/auth/login', { account, password })
      this.setToken(res.token)
      await this.fetchUser()
    },

    async register(userName, email, password, displayName) {
      const payload = { userName, password }
      if (email) payload.email = email
      if (displayName) payload.displayName = displayName
      const res = await http.post('/auth/register', payload)
      this.setToken(res.token)
      await this.fetchUser()
    },

    async fetchUser() {
      try {
        this.user = await http.get('/auth/me')
      } catch {
        this.logout()
      }
    },

    async uploadAvatar(file) {
      const fd = new FormData()
      fd.append('file', file)
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

    logout() {
      this.token = ''
      this.user = null
      localStorage.removeItem('token')
      // 登出即清除该账号的离线缓存，避免下一位登录用户看到旧数据
      clearUserDataCaches()
    }
  }
})
