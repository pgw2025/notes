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

    async updateProfile({ displayName, avatarUrl }) {
      const payload = {}
      if (displayName !== undefined) payload.displayName = displayName
      if (avatarUrl !== undefined) payload.avatarUrl = avatarUrl
      const updated = await http.put('/auth/profile', payload)
      this.user = updated
      return updated
    },

    logout() {
      this.token = ''
      this.user = null
      localStorage.removeItem('token')
    }
  }
})
