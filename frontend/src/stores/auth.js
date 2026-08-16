import { defineStore } from 'pinia'
import http from '../api/http'

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

    async login(email, password) {
      const res = await http.post('/auth/login', { email, password })
      this.setToken(res.token)
      await this.fetchUser()
    },

    async register(email, password, displayName) {
      const res = await http.post('/auth/register', { email, password, displayName })
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
    }
  }
})
