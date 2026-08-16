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

    logout() {
      this.token = ''
      this.user = null
      localStorage.removeItem('token')
    }
  }
})
